// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlipsiInk;

/// <summary>
/// Auto-Updater – checks GitHub Releases for new versions and downloads/installs them.
/// </summary>
public class AutoUpdater
{
    private const string RepoApiUrl = "https://api.github.com/repos/TechFlipsi/FlipsiInk/releases";
    private static readonly HttpClient _http = new();

    static AutoUpdater()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FlipsiInk-AutoUpdater");
    }

    /// <summary>Current app version.</summary>
    public string CurrentVersion => App.Version;

    /// <summary>Latest available version from GitHub (null if not checked yet).</summary>
    public string? LatestVersion { get; private set; }

    /// <summary>Whether an update is available.</summary>
    public bool UpdateAvailable { get; private set; }

    /// <summary>Download URL of the latest release.</summary>
    public string? DownloadUrl { get; private set; }

    /// <summary>Release notes of the latest release.</summary>
    public string? ReleaseNotes { get; private set; }

    /// <summary>Update channel: "stable" or "prerelease".</summary>
    public string Channel { get; set; } = "prerelease"; // Default prerelease since app is in alpha

    /// <summary>Fired when update check completes.</summary>
    public event EventHandler<UpdateCheckEventArgs>? UpdateChecked;

    /// <summary>Fired when download progress changes.</summary>
    public event EventHandler<UpdateDownloadEventArgs>? DownloadProgress;

    /// <summary>
    /// Checks GitHub for new releases asynchronously.
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
                // Filter pre-releases for stable channel
                var isPrerelease = release.GetProperty("prerelease").GetBoolean();
                if (Channel == "stable" && isPrerelease)
                    continue;

                var tag = release.GetProperty("tag_name").GetString() ?? "";
                var body = release.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : "";

                // Find .exe asset (Inno Setup installer)
                string? assetUrl = null;
                if (release.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            assetUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }

                LatestVersion = tag.TrimStart('v');
                DownloadUrl = assetUrl;
                ReleaseNotes = body;

                // Version comparison: only allow upgrades
                var current = ParseVersion(CurrentVersion);
                var latest = ParseVersion(LatestVersion);
                UpdateAvailable = latest > current;

                UpdateChecked?.Invoke(this, new UpdateCheckEventArgs(
                    UpdateAvailable, CurrentVersion, LatestVersion, ReleaseNotes));
                return;
            }

            // No matching releases found
            UpdateAvailable = false;
            UpdateChecked?.Invoke(this, new UpdateCheckEventArgs(false, CurrentVersion, null, null));
        }
        catch (Exception ex)
        {
            UpdateChecked?.Invoke(this, new UpdateCheckEventArgs(false, CurrentVersion, null, null, ex.Message));
        }
    }

    /// <summary>
    /// Downloads the update installer and saves it to the target path.
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
    /// Runs the downloaded Inno Setup installer (silent) and shuts down the app.
    /// </summary>
    public void InstallUpdate(string installerPath)
    {
        if (!File.Exists(installerPath) || new FileInfo(installerPath).Length < 1_000_000)
            throw new InvalidOperationException("Installer file invalid or too small.");

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
            UseShellExecute = true,
            Verb = "runas" // Admin rights for installer
        };
        System.Diagnostics.Process.Start(psi);

        // Shut down so installer can replace files
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>
    /// Parses a version string into a comparable Version object.
    /// </summary>
    private static Version ParseVersion(string version)
    {
        var cleaned = version.TrimStart('v');
        return Version.TryParse(cleaned, out var v) ? v : new Version(0, 0);
    }
}

/// <summary>
/// Event args for update check.
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
/// Event args for download progress.
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