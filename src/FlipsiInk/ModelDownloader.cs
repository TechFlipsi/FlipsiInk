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

namespace FlipsiInk;

/// <summary>
/// KI-Modell-Download & Management.
/// Verwaltet das Herunterladen, Prüfen und Löschen von ONNX-Modellen.
/// </summary>
public class ModelDownloader
{
    private static readonly HttpClient _http = new();
    private static readonly string ModelsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FlipsiInk", "Models");

    static ModelDownloader()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FlipsiInk-ModelDownloader");
        Directory.CreateDirectory(ModelsDir);
    }

    /// <summary>
    /// Lädt ein Modell herunter und speichert es am Zielpfad.
    /// </summary>
    public async System.Threading.Tasks.Task DownloadModelAsync(string targetPath, IProgress<double>? progress)
    {
        // TODO: Echte Download-URLs wenn Modelle verfügbar
        var url = "https://placeholder.example.com/model.onnx";

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
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
                progress?.Report((double)bytesRead / totalBytes);
        }
    }

    /// <summary>
    /// Prüft ob ein Modell am angegebenen Pfad existiert.
    /// </summary>
    public bool IsModelInstalled(string modelPath) => File.Exists(modelPath);

    /// <summary>
    /// Gibt Modell-Info zurück (Name und formatierte Größe).
    /// </summary>
    public string GetModelInfo(string modelPath)
    {
        if (!File.Exists(modelPath)) return "Modell nicht gefunden";
        var info = new FileInfo(modelPath);
        return $"{info.Name} ({FormatFileSize(info.Length)})";
    }

    /// <summary>
    /// Dateigröße des Modells in Bytes.
    /// </summary>
    public long GetModelSize(string modelPath) =>
        File.Exists(modelPath) ? new FileInfo(modelPath).Length : 0;

    /// <summary>
    /// Formatiert eine Dateigröße für die Anzeige (z.B. "1.2 GB").
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.#} {units[unit]}";
    }

    /// <summary>
    /// Löscht ein Modell.
    /// </summary>
    public void DeleteModel(string modelPath)
    {
        if (File.Exists(modelPath)) File.Delete(modelPath);
    }

    /// <summary>
    /// Gibt die Liste der verfügbaren Modelle zurück.
    /// </summary>
    public List<ModelInfo> GetAvailableModels() =>
    [
        new ModelInfo
        {
            Id = "qwen2.5-vl-3b-q4",
            Name = "Qwen2.5-VL 3B Q4",
            Description = "Texterkennung + Mathe – EMPFOHLEN für die meisten Nutzer",
            DownloadUrl = "https://placeholder.example.com/qwen2.5-vl-3b-q4.onnx", // TODO: echte URL
            Size = "~1.8 GB",
            Quantization = "Q4",
            IsRecommended = true,
            MinRamGb = 8,
            Tier = "stark"
        },
        new ModelInfo
        {
            Id = "qwen2.5-vl-7b-q4",
            Name = "Qwen2.5-VL 7B Q4",
            Description = "Beste Erkennungsqualität – benötigt 16 GB RAM und GPU empfohlen",
            DownloadUrl = "https://placeholder.example.com/qwen2.5-vl-7b-q4.onnx", // TODO: echte URL
            Size = "~4.5 GB",
            Quantization = "Q4",
            IsRecommended = false,
            MinRamGb = 16,
            Tier = "bester"
        },
        new ModelInfo
        {
            Id = "trocr-large",
            Name = "TrOCR large",
            Description = "Nur Texterkennung – leichtgewichtig, für ältere PCs",
            DownloadUrl = "https://placeholder.example.com/trocr-large.onnx", // TODO: echte URL
            Size = "~1.2 GB",
            Quantization = "FP32",
            IsRecommended = false,
            MinRamGb = 4,
            Tier = "schwach"
        }
    ];
}

/// <summary>
/// Informationen über ein verfügbares KI-Modell.
/// </summary>
public class ModelInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Size { get; set; } = "";
    public string Quantization { get; set; } = "";
    public bool IsRecommended { get; set; } = false;
    public int MinRamGb { get; set; } = 8;
    public string Tier { get; set; } = "stark"; // "schwach", "stark", "bester"
}