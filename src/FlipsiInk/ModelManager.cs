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
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlipsiInk;

/// <summary>
/// Central model management: download, delete, update-check, and path resolution.
/// Replaces the old ModelDownloader with a richer, config-driven approach.
/// </summary>
public class ModelManager
{
    private static readonly HttpClient _http = new();
    private static readonly string DefaultModelsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FlipsiInk", "Models");

    private readonly string _modelsDir;
    private readonly string _registryPath;

    /// <summary>Currently active model ID (from config or auto-detected).</summary>
    public string? ActiveModelId { get; private set; }

    /// <summary>Fired when the active model changes (download/delete/switch).</summary>
    public event Action? ActiveModelChanged;

    /// <summary>Registry of installed models (persisted as JSON).</summary>
    private Dictionary<string, InstalledModelEntry> _installed = new();

    static ModelManager()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FlipsiInk-ModelManager/1.0");
        _http.Timeout = TimeSpan.FromMinutes(30);
    }

    public ModelManager()
    {
        // Use config path if set, otherwise default
        _modelsDir = !string.IsNullOrWhiteSpace(App.Config.ModelPath)
            ? Path.GetDirectoryName(App.Config.ModelPath) ?? DefaultModelsDir
            : DefaultModelsDir;
        Directory.CreateDirectory(_modelsDir);

        _registryPath = Path.Combine(_modelsDir, "installed.json");
        LoadRegistry();
        DetectActiveModel();
    }

    /// <summary>Models directory path.</summary>
    public string ModelsDirectory => _modelsDir;

    /// <summary>Get the file path of the currently active ONNX model (or null).</summary>
    public string? GetActiveModelPath()
    {
        if (ActiveModelId != null && _installed.TryGetValue(ActiveModelId, out var entry))
            return entry.FilePath;
        return null;
    }

    // ── Catalog ──────────────────────────────────────────────

    /// <summary>Available model catalog (hardcoded for now; can be fetched from GitHub later).</summary>
    public List<ModelCatalogEntry> GetCatalog() => new()
    {
        new()
        {
            Id = "qwen2.5-vl-3b-q4",
            Name = "Qwen2.5-VL 3B Q4",
            Description = "Texterkennung + Mathe – EMPFOHLEN für die meisten Nutzer",
            DownloadUrl = "https://github.com/TechFlipsi/FlipsiInk/releases/download/models/qwen2.5-vl-3b-q4.onnx",
            EstimatedSizeBytes = 1_800_000_000,
            Size = "~1.8 GB",
            Quantization = "Q4",
            IsRecommended = true,
            MinRamGb = 8,
            Tier = "stark",
            Version = "1.0.0"
        },
        new()
        {
            Id = "qwen2.5-vl-7b-q4",
            Name = "Qwen2.5-VL 7B Q4",
            Description = "Beste Erkennungsqualität – benötigt 16 GB RAM, GPU empfohlen",
            DownloadUrl = "https://github.com/TechFlipsi/FlipsiInk/releases/download/models/qwen2.5-vl-7b-q4.onnx",
            EstimatedSizeBytes = 4_500_000_000,
            Size = "~4.5 GB",
            Quantization = "Q4",
            IsRecommended = false,
            MinRamGb = 16,
            Tier = "bester",
            Version = "1.0.0"
        },
        new()
        {
            Id = "trocr-large",
            Name = "TrOCR large",
            Description = "Nur Texterkennung – leichtgewichtig, für ältere PCs",
            DownloadUrl = "https://github.com/TechFlipsi/FlipsiInk/releases/download/models/trocr-large.onnx",
            EstimatedSizeBytes = 1_200_000_000,
            Size = "~1.2 GB",
            Quantization = "FP32",
            IsRecommended = false,
            MinRamGb = 4,
            Tier = "schwach",
            Version = "1.0.0"
        }
    };

    // ── Download ─────────────────────────────────────────────

    /// <summary>Download a model from the catalog. Reports progress 0.0–1.0.</summary>
    public async Task DownloadModelAsync(ModelCatalogEntry catalog, IProgress<double>? progress)
    {
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

            // Atomic rename
            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Move(tmpPath, targetPath);

            // Register
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

            // Auto-activate first installed model if none active
            if (ActiveModelId == null)
                SetActiveModel(catalog.Id);
        }
        catch
        {
            // Clean up partial download
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
            throw;
        }
    }

    // ── Delete ───────────────────────────────────────────────

    /// <summary>Delete an installed model.</summary>
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

    /// <summary>Set the active model used by OcrEngine.</summary>
    public void SetActiveModel(string modelId)
    {
        if (!_installed.ContainsKey(modelId)) return;
        ActiveModelId = modelId;
        App.Config.ModelPath = _installed[modelId].FilePath;
        App.Config.Save();
        ActiveModelChanged?.Invoke();
    }

    /// <summary>Get installed model entry (or null).</summary>
    public InstalledModelEntry? GetInstalled(string modelId) =>
        _installed.TryGetValue(modelId, out var e) ? e : null;

    /// <summary>All installed models.</summary>
    public IReadOnlyDictionary<string, InstalledModelEntry> Installed => _installed;

    // ── Update check ────────────────────────────────────────

    /// <summary>Check if a newer version of a model is available.</summary>
    public async Task<bool> CheckForUpdateAsync(ModelCatalogEntry catalog)
    {
        if (!_installed.TryGetValue(catalog.Id, out var entry)) return false;
        // Compare versions
        if (catalog.Version != entry.Version) return true;
        // Also check if remote file size differs significantly
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
        // Priority: config path > first installed model
        if (!string.IsNullOrWhiteSpace(App.Config.ModelPath) && File.Exists(App.Config.ModelPath))
        {
            // Find matching installed entry
            foreach (var (id, entry) in _installed)
            {
                if (entry.FilePath == App.Config.ModelPath)
                {
                    ActiveModelId = id;
                    return;
                }
            }
            // Config points to a file we don't have in registry – scan it in
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

        // Fall back to first installed
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

            // Prune entries whose files no longer exist
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

/// <summary>Catalog entry for a model available for download.</summary>
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
    public int MinRamGb { get; set; } = 8;
    public string Tier { get; set; } = "stark";
    public string Version { get; set; } = "1.0.0";
}

/// <summary>Persisted info about an installed model.</summary>
public class InstalledModelEntry
{
    public string Id { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime InstalledAt { get; set; }
    public long SizeBytes { get; set; }
}