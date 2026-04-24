// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlipsiInk;

/// <summary>
/// Central model management: download, delete, update-check, and path resolution.
/// v0.4.0: Three tiers – Mittel (8GB RAM), Stark (16GB RAM, recommended), Premium (32GB RAM).
/// Supports remote catalog fetching for automatic model updates.
/// </summary>
public class ModelManager
{
    private static readonly HttpClient _http = new();
    private static readonly string DefaultModelsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FlipsiInk", "Models");

    private readonly string _modelsDir;
    private readonly string _registryPath;

    public string? ActiveModelId { get; private set; }
    public event Action? ActiveModelChanged;

    private Dictionary<string, InstalledModelEntry> _installed = new();

    // Cached remote catalog (fetched once per session)
    private List<ModelCatalogEntry>? _remoteCatalog;

    static ModelManager()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FlipsiInk-ModelManager/1.0");
        _http.Timeout = TimeSpan.FromMinutes(30);
    }

    public ModelManager()
    {
        _modelsDir = !string.IsNullOrWhiteSpace(App.Config.ModelPath)
            ? Path.GetDirectoryName(App.Config.ModelPath) ?? DefaultModelsDir
            : DefaultModelsDir;
        Directory.CreateDirectory(_modelsDir);

        _registryPath = Path.Combine(_modelsDir, "installed.json");
        LoadRegistry();
        DetectActiveModel();
    }

    public string ModelsDirectory => _modelsDir;

    public string? GetActiveModelPath()
    {
        if (ActiveModelId != null && _installed.TryGetValue(ActiveModelId, out var entry))
            return entry.FilePath;
        return null;
    }

    // ── System RAM Detection ────────────────────────────────

    public static long GetTotalRamMb()
    {
        try
        {
            var mem = GC.GetGCMemoryInfo();
            return mem.TotalAvailableMemoryBytes / (1024 * 1024);
        }
        catch
        {
            return 8192;
        }
    }

    public static bool HasEnoughRam(int minRamGb)
    {
        return GetTotalRamMb() >= minRamGb * 1024;
    }

    // ── Catalog ──────────────────────────────────────────────

    /// <summary>
    /// Hardcoded fallback catalog (used when remote fetch fails).
    /// v0.4.0 tiers: mittel (8GB RAM), stark (16GB RAM, recommended), premium (32GB RAM).
    /// </summary>
    public List<ModelCatalogEntry> GetFallbackCatalog() => new()
    {
        new()
        {
            Id = "florence2-base-q8",
            Name = "Florence-2 Base Q8",
            Description = "Gute Texterkennung - fuer Nutzer mit 8 GB RAM",
            DownloadUrl = "https://github.com/TechFlipsi/FlipsiInk/releases/download/models/florence2-base-q8.onnx",
            EstimatedSizeBytes = 1_500_000_000,
            Size = "~1.5 GB",
            Quantization = "Q8",
            IsRecommended = false,
            MinRamGb = 8,
            Tier = "mittel",
            Version = "1.0.0"
        },
        new()
        {
            Id = "qwen2.5-vl-3b-q4",
            Name = "Qwen2.5-VL 3B Q4",
            Description = "Text + Mathe - EMPFOHLEN fuer die meisten Nutzer (16 GB RAM)",
            DownloadUrl = "https://github.com/TechFlipsi/FlipsiInk/releases/download/models/qwen2.5-vl-3b-q4.onnx",
            EstimatedSizeBytes = 3_800_000_000,
            Size = "~3.8 GB",
            Quantization = "Q4",
            IsRecommended = true,
            MinRamGb = 16,
            Tier = "stark",
            Version = "1.1.0"
        },
        new()
        {
            Id = "qwen2.5-vl-7b-q4",
            Name = "Qwen2.5-VL 7B Q4",
            Description = "Beste Erkennungsqualitaet - 32 GB RAM, GPU empfohlen",
            DownloadUrl = "https://github.com/TechFlipsi/FlipsiInk/releases/download/models/qwen2.5-vl-7b-q4.onnx",
            EstimatedSizeBytes = 8_000_000_000,
            Size = "~8 GB",
            Quantization = "Q4",
            IsRecommended = false,
            MinRamGb = 32,
            Tier = "premium",
            Version = "1.0.0"
        }
    };

    /// <summary>
    /// Gets the model catalog, preferring remote if available, falling back to hardcoded.
    /// </summary>
    public List<ModelCatalogEntry> GetCatalog()
    {
        return _remoteCatalog ?? GetFallbackCatalog();
    }

    /// <summary>
    /// Fetches the remote model catalog from the configured URL.
    /// Returns true if remote catalog was loaded successfully.
    /// </summary>
    public async Task<bool> FetchRemoteCatalogAsync()
    {
        var url = App.Config.ModelCatalogUrl;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var catalog = JsonSerializer.Deserialize<List<ModelCatalogEntry>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (catalog != null && catalog.Count > 0)
            {
                _remoteCatalog = catalog;
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Remote catalog fetch failed: {ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// Checks all installed models against the (remote or fallback) catalog
    /// and returns a list of models that have updates available.
    /// </summary>
    public async Task<List<(ModelCatalogEntry Catalog, string Reason)>> CheckForModelUpdatesAsync()
    {
        var updates = new List<(ModelCatalogEntry, string)>();
        var catalog = GetCatalog();

        // Ensure we have the latest catalog
        if (_remoteCatalog == null)
            await FetchRemoteCatalogAsync();

        catalog = GetCatalog();

        foreach (var installed in _installed)
        {
            var catalogEntry = catalog.FirstOrDefault(c => c.Id == installed.Key);
            if (catalogEntry == null)
            {
                // Check if there's a newer model in the same tier
                var installedCatalogEntry = GetFallbackCatalog().FirstOrDefault(c => c.Id == installed.Key);
                if (installedCatalogEntry != null)
                {
                    var sameTierNewer = catalog.FirstOrDefault(c =>
                        c.Tier == installedCatalogEntry.Tier && c.Id != installed.Key);
                    if (sameTierNewer != null)
                    {
                        updates.Add((sameTierNewer, $"Neues Modell in Tier '{GetTierLabel(sameTierNewer.Tier)}' verfuegbar"));
                    }
                }
                continue;
            }

            // Compare versions
            if (catalogEntry.Version != installed.Value.Version)
            {
                updates.Add((catalogEntry, $"Update von v{installed.Value.Version} auf v{catalogEntry.Version}"));
            }
        }

        return updates;
    }

    /// <summary>
    /// Gets a human-readable label for a tier.
    /// </summary>
    public static string GetTierLabel(string tier) => tier switch
    {
        "mittel" => "Mittel",
        "stark" => "Stark",
        "premium" => "Premium",
        _ => tier
    };

    /// <summary>
    /// Gets a tier color as hex string for UI badges.
    /// </summary>
    public static string GetTierColor(string tier) => tier switch
    {
        "mittel" => "#2196F3",
        "stark" => "#0078D7",
        "premium" => "#9C27B0",
        _ => "#666"
    };

    // ── Download ─────────────────────────────────────────────

    public async Task DownloadModelAsync(ModelCatalogEntry catalog, IProgress<double>? progress)
    {
        // RAM warning check
        if (!HasEnoughRam(catalog.MinRamGb))
        {
            throw new InvalidOperationException(
                $"Nicht genug RAM fuer {catalog.Name}. Benoetigt: {catalog.MinRamGb} GB, " +
                $"Verfuegbar: ~{GetTotalRamMb() / 1024} GB.");
        }

        var targetPath = Path.Combine(_modelsDir, $"{catalog.Id}.onnx");
        var tmpPath = targetPath + ".downloading";

        try
        {
            using var response = await _http.GetAsync(catalog.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? catalog.EstimatedSizeBytes;

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long bytesRead = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                bytesRead += read;
                if (totalBytes > 0)
                    progress?.Report((double)bytesRead / totalBytes);
            }

            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Move(tmpPath, targetPath);

            var entry = new InstalledModelEntry
            {
                Id = catalog.Id,
                FilePath = targetPath,
                Version = catalog.Version,
                InstalledAt = DateTime.UtcNow,
                SizeBytes = new FileInfo(targetPath).Length
            };
            _installed[catalog.Id] = entry;
            SaveRegistry();

            if (ActiveModelId == null)
                SetActiveModel(catalog.Id);
        }
        catch
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
            throw;
        }
    }

    // ── Delete ───────────────────────────────────────────────

    public void DeleteModel(string modelId)
    {
        if (!_installed.TryGetValue(modelId, out var entry)) return;

        if (File.Exists(entry.FilePath))
            File.Delete(entry.FilePath);

        _installed.Remove(modelId);
        SaveRegistry();

        if (ActiveModelId == modelId)
        {
            ActiveModelId = null;
            DetectActiveModel();
            ActiveModelChanged?.Invoke();
        }
    }

    // ── Active model ────────────────────────────────────────

    public void SetActiveModel(string modelId)
    {
        if (!_installed.ContainsKey(modelId)) return;
        ActiveModelId = modelId;
        App.Config.ModelPath = _installed[modelId].FilePath;
        App.Config.Save();
        ActiveModelChanged?.Invoke();
    }

    public InstalledModelEntry? GetInstalled(string modelId) =>
        _installed.TryGetValue(modelId, out var e) ? e : null;

    public IReadOnlyDictionary<string, InstalledModelEntry> Installed => _installed;

    // ── Update check ────────────────────────────────────────

    public async Task<bool> CheckForUpdateAsync(ModelCatalogEntry catalog)
    {
        if (!_installed.TryGetValue(catalog.Id, out var entry)) return false;
        if (catalog.Version != entry.Version) return true;
        try
        {
            var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, catalog.DownloadUrl));
            if (response.IsSuccessStatusCode)
            {
                var remoteSize = response.Content.Headers.ContentLength ?? 0;
                if (remoteSize > 0 && Math.Abs(remoteSize - entry.SizeBytes) > 1_000_000)
                    return true;
            }
        }
        catch { /* ignore */ }
        return false;
    }

    // ── Internal ─────────────────────────────────────────────

    private void DetectActiveModel()
    {
        if (!string.IsNullOrWhiteSpace(App.Config.ModelPath) && File.Exists(App.Config.ModelPath))
        {
            foreach (var (id, entry) in _installed)
            {
                if (entry.FilePath == App.Config.ModelPath)
                {
                    ActiveModelId = id;
                    return;
                }
            }
            var extId = Path.GetFileNameWithoutExtension(App.Config.ModelPath);
            _installed[extId] = new InstalledModelEntry
            {
                Id = extId,
                FilePath = App.Config.ModelPath,
                Version = "unknown",
                InstalledAt = DateTime.MinValue,
                SizeBytes = new FileInfo(App.Config.ModelPath).Length
            };
            ActiveModelId = extId;
            SaveRegistry();
            return;
        }

        if (_installed.Count > 0)
        {
            foreach (var (id, _) in _installed)
            {
                ActiveModelId = id;
                App.Config.ModelPath = _installed[id].FilePath;
                App.Config.Save();
                return;
            }
        }
    }

    private void LoadRegistry()
    {
        if (!File.Exists(_registryPath)) return;
        try
        {
            var json = File.ReadAllText(_registryPath);
            _installed = JsonSerializer.Deserialize<Dictionary<string, InstalledModelEntry>>(json)
                         ?? new Dictionary<string, InstalledModelEntry>();

            var toRemove = new List<string>();
            foreach (var (id, entry) in _installed)
            {
                if (!File.Exists(entry.FilePath)) toRemove.Add(id);
            }
            foreach (var id in toRemove) _installed.Remove(id);
            if (toRemove.Count > 0) SaveRegistry();
        }
        catch { _installed = new(); }
    }

    private void SaveRegistry()
    {
        try
        {
            var json = JsonSerializer.Serialize(_installed, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_registryPath, json);
        }
        catch { /* best effort */ }
    }

    public static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.#} {units[unit]}";
    }
}

public class ModelCatalogEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public long EstimatedSizeBytes { get; set; }
    public string Size { get; set; } = "";
    public string Quantization { get; set; } = "";
    public bool IsRecommended { get; set; }
    public int MinRamGb { get; set; } = 16;
    public string Tier { get; set; } = "stark";
    public string Version { get; set; } = "1.0.0";
}

public class InstalledModelEntry
{
    public string Id { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime InstalledAt { get; set; }
    public long SizeBytes { get; set; }
}