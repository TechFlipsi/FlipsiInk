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

        try
        {
            if (!Directory.Exists(_modelsDir))
                Directory.CreateDirectory(_modelsDir);
        }
        catch (UnauthorizedAccessException)
        {
            // Fallback to user-local path if default is inaccessible
            _modelsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipsiInk", "Models");
            try { Directory.CreateDirectory(_modelsDir); } catch { /* best effort */ }
        }
        catch (IOException)
        {
            // Path might be on a drive that doesn't exist or is unavailable
        }
        catch { /* unknown error – continue without models directory */ }

        _registryPath = Path.Combine(_modelsDir, "installed.json");
        try { LoadRegistry(); } catch { _installed = new(); }
        try { DetectActiveModel(); } catch { /* no active model, that's fine */ }
    }

    public string ModelsDirectory => _modelsDir;

    public string? GetActiveModelPath()
    {
        if (ActiveModelId != null && _installed.TryGetValue(ActiveModelId, out var entry))
        {
            // For directory-based models (Florence-2), return the directory path
            // For legacy single-file models, return the file path
            if (entry.Files != null && entry.Files.Count > 0)
                return Path.GetDirectoryName(entry.FilePath) ?? entry.FilePath;
            return entry.FilePath;
        }
        return null;
    }

    // ── System RAM Detection ────────────────────────────────

    public static long GetTotalRamMb()
    {
        try
        {
            // Use Win32_ComputerSystem.TotalPhysicalMemory to get ACTUAL installed RAM.
            // Unlike Win32_OperatingSystem.TotalVisibleMemorySize (which reports AVAILABLE
            // RAM minus hardware-reserved memory), TotalPhysicalMemory reports the real
            // physical RAM sticks installed. This fixes the "Mittel" vs "Stark" tier
            // detection: on a 16GB system ~0.5-1GB is reserved by GPU/BIOS, so
            // TotalVisibleMemorySize would report ~15.5GB, causing HasEnoughRam(15) to fail
            // and the app to recommend the wrong tier.
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                var bytes = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                return bytes / (1024 * 1024); // Bytes to MB
            }

            // Fallback: try Win32_PhysicalMemory (sum of individual sticks)
            using var searcher2 = new System.Management.ManagementObjectSearcher(
                "SELECT Capacity FROM Win32_PhysicalMemory");
            long totalCap = 0;
            foreach (var obj in searcher2.Get())
            {
                totalCap += Convert.ToInt64(obj["Capacity"]);
            }
            if (totalCap > 0)
                return totalCap / (1024 * 1024);

            // Final fallback to GC memory info
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
    /// Florence-2 tiers: mittel (7GB RAM), stark (15GB RAM, recommended), premium (31GB RAM).
    /// 1GB Puffer-Regel: immer 1GB weniger als beworben wegen RAM-Anzeigedifferenzen.
    /// </summary>
    public List<ModelCatalogEntry> GetFallbackCatalog() => new()
    {
        new()
        {
            Id = "florence2-base-ft-int8",
            Name = "Florence-2 Base INT8",
            Description = "Gute Texterkennung – für Nutzer mit 8 GB RAM",
            BaseUrl = "https://huggingface.co/onnx-community/Florence-2-base-ft/resolve/main/onnx/",
            Files = new List<string>
            {
                "encoder_model_int8.onnx",
                "decoder_model_merged_int8.onnx",
                "vision_encoder_int8.onnx"
            },
            EstimatedSizeBytes = 235_929_600,
            Size = "~225 MB",
            Quantization = "INT8",
            IsRecommended = false,
            MinRamGb = 7,
            Tier = "mittel",
            Version = "1.0.0"
        },
        new()
        {
            Id = "florence2-large-ft-int8",
            Name = "Florence-2 Large INT8",
            Description = "Text + Mathe – EMPFOHLEN für die meisten Nutzer (16 GB RAM)",
            BaseUrl = "https://huggingface.co/onnx-community/Florence-2-large-ft/resolve/main/onnx/",
            Files = new List<string>
            {
                "encoder_model_int8.onnx",
                "decoder_model_merged_int8.onnx",
                "embed_tokens_int8.onnx",
                "vision_encoder_int8.onnx"
            },
            EstimatedSizeBytes = 829_440_000,
            Size = "~791 MB",
            Quantization = "INT8",
            IsRecommended = true,
            MinRamGb = 15,
            Tier = "stark",
            Version = "1.0.0"
        },
        new()
        {
            Id = "florence2-large-ft-fp16",
            Name = "Florence-2 Large FP16",
            Description = "Beste Erkennungsqualität – 32 GB RAM, GPU empfohlen",
            BaseUrl = "https://huggingface.co/onnx-community/Florence-2-large-ft/resolve/main/onnx/",
            Files = new List<string>
            {
                "encoder_model_fp16.onnx",
                "decoder_model_merged_fp16.onnx",
                "embed_tokens_fp16.onnx",
                "vision_encoder_fp16.onnx"
            },
            EstimatedSizeBytes = 1_652_500_000,
            Size = "~1.6 GB",
            Quantization = "FP16",
            IsRecommended = false,
            MinRamGb = 31,
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
                $"Nicht genug RAM für {catalog.Name}. Benötigt: {catalog.MinRamGb + 1} GB, " +
                $"Verfügbar: ca. {GetTotalRamMb() / 1024} GB (1 GB Puffer bereits berücksichtigt).");
        }

        // Create a subdirectory for this model tier
        var modelDir = Path.Combine(_modelsDir, catalog.Id);
        try { Directory.CreateDirectory(modelDir); } catch { /* best effort */ }

        var totalFiles = catalog.Files.Count;
        if (totalFiles == 0)
        {
            // Legacy single-file fallback (shouldn't happen with new catalog)
            totalFiles = 1;
        }

        long totalBytesDownloaded = 0;
        var completedFiles = 0;

        foreach (var fileName in catalog.Files)
        {
            var targetPath = Path.Combine(modelDir, fileName);
            var tmpPath = targetPath + ".downloading";
            var url = catalog.BaseUrl.TrimEnd('/') + "/" + fileName;

            try
            {
                // Skip already-downloaded files
                if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
                {
                    completedFiles++;
                    totalBytesDownloaded += new FileInfo(targetPath).Length;
                    continue;
                }

                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;

                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[81920];
                long bytesRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    bytesRead += read;
                    totalBytesDownloaded += read;

                    if (catalog.EstimatedSizeBytes > 0)
                    {
                        // Overall progress: completed files + current file progress
                        var overallProgress = (completedFiles + (double)bytesRead / Math.Max(totalBytes, 1)) / totalFiles;
                        progress?.Report(Math.Min(overallProgress, 0.99));
                    }
                }

                if (File.Exists(targetPath)) File.Delete(targetPath);
                File.Move(tmpPath, targetPath);

                completedFiles++;
            }
            catch
            {
                if (File.Exists(tmpPath))
                    try { File.Delete(tmpPath); } catch { /* best effort */ }
                throw;
            }
        }

        // Write manifest file
        var manifest = new ModelManifest
        {
            Id = catalog.Id,
            Name = catalog.Name,
            Version = catalog.Version,
            Tier = catalog.Tier,
            Quantization = catalog.Quantization,
            Files = catalog.Files,
            InstalledAt = DateTime.UtcNow
        };
        try
        {
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(modelDir, "model.json"), manifestJson);
        }
        catch { /* best effort */ }

        // Calculate total size of all downloaded files
        long totalSize = 0;
        foreach (var fileName in catalog.Files)
        {
            var filePath = Path.Combine(modelDir, fileName);
            if (File.Exists(filePath))
                totalSize += new FileInfo(filePath).Length;
        }

        var entry = new InstalledModelEntry
        {
            Id = catalog.Id,
            FilePath = modelDir,  // Directory path for multi-file models
            Version = catalog.Version,
            InstalledAt = DateTime.UtcNow,
            SizeBytes = totalSize,
            Files = new List<string>(catalog.Files)
        };
        _installed[catalog.Id] = entry;
        SaveRegistry();

        progress?.Report(1.0);

        if (ActiveModelId == null)
            SetActiveModel(catalog.Id);
    }

    // ── Delete ───────────────────────────────────────────────

    public void DeleteModel(string modelId)
    {
        if (!_installed.TryGetValue(modelId, out var entry)) return;

        // For directory-based models, delete the entire directory
        if (entry.Files != null && entry.Files.Count > 0)
        {
            var modelDir = entry.FilePath;
            if (Directory.Exists(modelDir))
            {
                try { Directory.Delete(modelDir, recursive: true); } catch { /* best effort */ }
            }
        }
        else
        {
            // Legacy single-file model
            if (File.Exists(entry.FilePath))
                File.Delete(entry.FilePath);
        }

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
            // For multi-file models, check the first file's size
            if (catalog.Files != null && catalog.Files.Count > 0 && !string.IsNullOrEmpty(catalog.BaseUrl))
            {
                var firstFileUrl = catalog.BaseUrl.TrimEnd('/') + "/" + catalog.Files[0];
                var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, firstFileUrl));
                if (response.IsSuccessStatusCode)
                {
                    var remoteSize = response.Content.Headers.ContentLength ?? 0;
                    if (remoteSize > 0 && Math.Abs(remoteSize - entry.SizeBytes) > 10_000_000)
                        return true;
                }
            }
            else if (!string.IsNullOrEmpty(catalog.DownloadUrl))
            {
                // Legacy single-file check
                var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, catalog.DownloadUrl));
                if (response.IsSuccessStatusCode)
                {
                    var remoteSize = response.Content.Headers.ContentLength ?? 0;
                    if (remoteSize > 0 && Math.Abs(remoteSize - entry.SizeBytes) > 1_000_000)
                        return true;
                }
            }
        }
        catch { /* ignore */ }
        return false;
    }

    // ── Internal ─────────────────────────────────────────────

    private void DetectActiveModel()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(App.Config.ModelPath))
            {
                // Check if ModelPath points to a directory (Florence-2 multi-file) or a file
                if (Directory.Exists(App.Config.ModelPath))
                {
                    // Directory-based model
                    var dirName = Path.GetFileName(App.Config.ModelPath);
                    if (_installed.TryGetValue(dirName, out var dirEntry))
                    {
                        ActiveModelId = dirName;
                        return;
                    }
                    // Register it if not yet tracked
                    _installed[dirName] = new InstalledModelEntry
                    {
                        Id = dirName,
                        FilePath = App.Config.ModelPath,
                        Version = "unknown",
                        InstalledAt = DateTime.MinValue,
                        SizeBytes = 0
                    };
                    ActiveModelId = dirName;
                    SaveRegistry();
                    return;
                }
                else if (File.Exists(App.Config.ModelPath))
                {
                    // Legacy single-file model
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
                        SizeBytes = TryGetFileSize(App.Config.ModelPath)
                    };
                    ActiveModelId = extId;
                    SaveRegistry();
                    return;
                }

                // Config ModelPath doesn't exist – clear it
                App.Config.ModelPath = "";
                App.Config.Save();
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
        catch (Exception)
        {
            // Graceful fallback – no active model available
            ActiveModelId = null;
        }
    }

    private static long TryGetFileSize(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
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
                // Directory-based models: check directory exists
                if (entry.Files != null && entry.Files.Count > 0)
                {
                    if (!Directory.Exists(entry.FilePath))
                    {
                        toRemove.Add(id);
                        continue;
                    }
                    // Check that at least the first file exists
                    foreach (var file in entry.Files)
                    {
                        if (!File.Exists(Path.Combine(entry.FilePath, file)))
                        {
                            toRemove.Add(id);
                            break;
                        }
                    }
                }
                else
                {
                    // Legacy single-file model
                    if (!File.Exists(entry.FilePath)) toRemove.Add(id);
                }
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

/// <summary>
/// Manifest file written to each model directory after successful download.
/// Contains metadata about the downloaded model tier.
/// </summary>
public class ModelManifest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Tier { get; set; } = "";
    public string Quantization { get; set; } = "";
    public List<string> Files { get; set; } = new();
    public DateTime InstalledAt { get; set; }
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

    /// <summary>
    /// ONNX file names to download from HuggingFace.
    /// Each file is downloaded from BaseUrl + filename.
    /// </summary>
    public List<string> Files { get; set; } = new();

    /// <summary>
    /// HuggingFace base URL for downloading model files.
    /// Each file in Files is appended to this URL.
    /// </summary>
    public string BaseUrl { get; set; } = "";
}

public class InstalledModelEntry
{
    public string Id { get; set; } = "";
    /// <summary>
    /// Path to the model directory (not a single file).
    /// For legacy single-file models, this points to the .onnx file.
    /// </summary>
    public string FilePath { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime InstalledAt { get; set; }
    public long SizeBytes { get; set; }
    /// <summary>
    /// List of ONNX files that should be present in the model directory.
    /// </summary>
    public List<string> Files { get; set; } = new();
}