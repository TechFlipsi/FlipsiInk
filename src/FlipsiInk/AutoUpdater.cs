// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlipsiInk;

/// <summary>
/// Auto-Updater – prüft GitHub Releases auf neue Versionen und lädt diese herunter.
/// </summary>
public class AutoUpdater
{
    private const string RepoApiUrl = "https://api.github.com/repos/TechFlipsi/FlipsiInk/releases";
    private static readonly HttpClient _http = new();

    static AutoUpdater()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FlipsiInk-AutoUpdater");
    }

    /// <summary>Aktuelle Version der App.</summary>
    public string CurrentVersion => App.Version;

    /// <summary>Neueste verfügbare Version von GitHub (null wenn noch nicht geprüft).</summary>
    public string? LatestVersion { get; private set; }

    /// <summary>Ob ein Update verfügbar ist.</summary>
    public bool UpdateAvailable { get; private set; }

    /// <summary>Download-URL des neuesten Releases.</summary>
    public string? DownloadUrl { get; private set; }

    /// <summary>Release Notes des neuesten Releases.</summary>
    public string? ReleaseNotes { get; private set; }

    /// <summary>Update-Kanal: "stable" oder "prerelease".</summary>
    public string Channel { get; set; } = "stable";

    /// <summary>Wird ausgelöst wenn die Update-Prüfung abgeschlossen ist.</summary>
    public event EventHandler<UpdateCheckEventArgs>? UpdateChecked;

    /// <summary>Wird ausgelöst wenn der Download-Fortschritt sich ändert.</summary>
    public event EventHandler<UpdateDownloadEventArgs>? DownloadProgress;

    /// <summary>
    /// Prüft asynchron auf neue GitHub Releases.
    /// </summary>
    public async Task CheckForUpdatesAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(RepoApiUrl);
            using var doc = JsonDocument.Parse(json);
            var releases = doc.RootElement.EnumerateArray();

            foreach (var release in releases)
            {
                // Pre-Release filtern wenn Kanal "stable"
                var isPrerelease = release.GetProperty("prerelease").GetBoolean();
                if (Channel == "stable" && isPrerelease)
                    continue;

                var tag = release.GetProperty("tag_name").GetString() ?? "";
                var body = release.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : "";

                // Erste Asset-URL als Download-Link
                string? assetUrl = null;
                if (release.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            assetUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }

                LatestVersion = tag.TrimStart('v');
                DownloadUrl = assetUrl;
                ReleaseNotes = body;

                // Versionsvergleich: Nur Upgrades erlauben
                var current = ParseVersion(CurrentVersion);
                var latest = ParseVersion(LatestVersion);
                UpdateAvailable = latest > current;

                UpdateChecked?.Invoke(this, new UpdateCheckEventArgs(
                    UpdateAvailable, CurrentVersion, LatestVersion, ReleaseNotes));
                return;
            }

            // Keine passenden Releases gefunden
            UpdateAvailable = false;
            UpdateChecked?.Invoke(this, new UpdateCheckEventArgs(false, CurrentVersion, null, null));
        }
        catch (Exception ex)
        {
            UpdateChecked?.Invoke(this, new UpdateCheckEventArgs(false, CurrentVersion, null, null, ex.Message));
        }
    }

    /// <summary>
    /// Lädt das Update-ZIP herunter und speichert es am Zielpfad.
    /// </summary>
    public async Task DownloadUpdateAsync(string downloadUrl, string targetPath, IProgress<double>? progress)
    {
        using var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long bytesRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            bytesRead += read;

            if (totalBytes > 0)
            {
                var pct = (double)bytesRead / totalBytes;
                progress?.Report(pct);
                DownloadProgress?.Invoke(this, new UpdateDownloadEventArgs(pct, bytesRead, totalBytes));
            }
        }
    }

    /// <summary>
    /// Entpackt das Update-ZIP und ersetzt die alten Dateien.
    /// </summary>
    public void InstallUpdate(string zipPath)
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var extractDir = Path.Combine(Path.GetTempPath(), "FlipsiInk_Update");

        // Vorheriges Extrakt-Verzeichnis aufräumen
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        Directory.CreateDirectory(extractDir);

        // ZIP entpacken
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        // Batch-Datei für das Ersetzen erstellen (App muss sich selbst beenden)
        var batch = $@"@echo off
timeout /t 2 /nobreak >nul
xcopy /y /e ""{extractDir}\*"" ""{appDir}""
del ""{zipPath}""
rmdir /s /q ""{extractDir}""
start """" ""{Path.Combine(appDir, "FlipsiInk.exe")}""
del ""%~f0""
";
        var batchPath = Path.Combine(Path.GetTempPath(), "FlipsiInk_InstallUpdate.bat");
        File.WriteAllText(batchPath, batch);

        // Batch starten und App beenden
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = batchPath,
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    /// <summary>
    /// Parst eine Versionszeichenkette in ein vergleichbares Objekt.
    /// </summary>
    private static Version ParseVersion(string version)
    {
        var cleaned = version.TrimStart('v');
        return Version.TryParse(cleaned, out var v) ? v : new Version(0, 0);
    }
}

/// <summary>
/// Event-Args für Update-Prüfung.
/// </summary>
public class UpdateCheckEventArgs : EventArgs
{
    public bool UpdateAvailable { get; }
    public string CurrentVersion { get; }
    public string? LatestVersion { get; }
    public string? ReleaseNotes { get; }
    public string? Error { get; }

    public UpdateCheckEventArgs(bool updateAvailable, string currentVersion,
        string? latestVersion, string? releaseNotes, string? error = null)
    {
        UpdateAvailable = updateAvailable;
        CurrentVersion = currentVersion;
        LatestVersion = latestVersion;
        ReleaseNotes = releaseNotes;
        Error = error;
    }
}

/// <summary>
/// Event-Args für Download-Fortschritt.
/// </summary>
public class UpdateDownloadEventArgs : EventArgs
{
    public double Progress { get; }
    public long BytesDownloaded { get; }
    public long TotalBytes { get; }

    public UpdateDownloadEventArgs(double progress, long bytesDownloaded, long totalBytes)
    {
        Progress = progress;
        BytesDownloaded = bytesDownloaded;
        TotalBytes = totalBytes;
    }
}