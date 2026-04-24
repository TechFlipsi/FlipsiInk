// FlipsiInk - .fink Note Format Handler
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Ink;

namespace FlipsiInk;

/// <summary>
/// The .fink format is a ZIP container holding:
///   - manifest.json  (metadata, version, page list)
///   - pages/page_001/strokes.isf  (Ink Serialized Format - binary stroke data)
///   - pages/page_001/meta.json     (page template, zoom, theme)
///   - pages/page_001/recognized.json (OCR/math results per page)
///   - pages/page_001/background.png  (PDF background, if any)
///
/// This preserves strokes as VECTOR data, not rasterized images.
/// </summary>

public class FinkManifest
{
    public const string CurrentVersion = "2";
    public const string Magic = "flipsiink-fink";

    public string Version { get; set; } = CurrentVersion;
    public string Format { get; set; } = Magic;
    public string Title { get; set; } = "Untitled";
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string Color { get; set; } = "#007AFF";
    public string? CoverTemplate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public List<FinkPageEntry> Pages { get; set; } = new();
    /// <summary>True if this notebook was auto-titled by AI.</summary>
    public bool AutoTitled { get; set; }
}

public class FinkPageEntry
{
    public int PageNumber { get; set; }
    public string Template { get; set; } = "Blank";
    public double Zoom { get; set; } = 1.0;
    public string Theme { get; set; } = "system";
    /// <summary>Relative path to strokes.isf inside the zip.</summary>
    public string StrokesFile { get; set; } = "";
    /// <summary>Relative path to background PNG (PDF import), or null.</summary>
    public string? BackgroundFile { get; set; }
    /// <summary>Relative path to recognized text/math results, or null.</summary>
    public string? RecognizedFile { get; set; }
}

public class FinkRecognizedData
{
    public List<RecognizedBlock> Blocks { get; set; } = new();
}

public class RecognizedBlock
{
    public string Type { get; set; } = "text"; // "text" or "math"
    public string Content { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public DateTime RecognizedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Reads and writes .fink note files (ZIP container with ISF stroke data).
/// </summary>
public static class FinkFormat
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Saves a notebook to a .fink file.
    /// Empty pages (no strokes) are skipped unless forceSaveEmpty is true.
    /// </summary>
    public static void Save(string filePath, Notebook notebook, bool forceSaveEmpty = false)
    {
        using var zip = ZipFile.Open(filePath, ZipArchiveMode.Create);

        var manifest = new FinkManifest
        {
            Title = notebook.Name,
            Color = notebook.Color,
            CreatedAt = notebook.CreatedAt,
            ModifiedAt = DateTime.UtcNow,
            AutoTitled = notebook.Name.StartsWith("Untitled") || notebook.AutoTitled
        };

        int pageIndex = 0;
        foreach (var page in notebook.Pages)
        {
            pageIndex++;
            string pageDir = $"pages/page_{pageIndex:D3}";

            // Check if page has content
            bool hasStrokes = page.Strokes != null && page.Strokes.Count > 0;
            bool hasBase64 = !string.IsNullOrWhiteSpace(page.StrokesJson);

            if (!forceSaveEmpty && !hasStrokes && !hasBase64)
                continue; // Skip empty pages

            var entry = new FinkPageEntry
            {
                PageNumber = pageIndex,
                Template = page.Template.ToString(),
                Zoom = page.Zoom,
                Theme = page.Theme ?? "system",
                StrokesFile = $"{pageDir}/strokes.isf"
            };

            // Save strokes as ISF (Ink Serialized Format) - binary vector format
            if (hasStrokes)
            {
                var strokesEntry = zip.CreateEntry(entry.StrokesFile, CompressionLevel.Optimal);
                using var stream = strokesEntry.Open();
                page.Strokes.Save(stream);
            }
            else if (hasBase64)
            {
                // Migrate from base64 JSON to ISF
                try
                {
                    var data = Convert.FromBase64String(page.StrokesJson);
                    var strokesEntry = zip.CreateEntry(entry.StrokesFile, CompressionLevel.Optimal);
                    using var stream = strokesEntry.Open();
                    stream.Write(data, 0, data.Length);
                }
                catch
                {
                    // If base64 decode fails, skip this page's strokes
                }
            }

            // Save page metadata
            var metaJson = JsonSerializer.Serialize(new
            {
                pageNumber = pageIndex,
                template = page.Template.ToString(),
                zoom = page.Zoom,
                theme = page.Theme ?? "system"
            }, JsonOpts);
            var metaEntry = zip.CreateEntry($"{pageDir}/meta.json", CompressionLevel.Optimal);
            using (var metaStream = metaEntry.Open())
            using (var writer = new StreamWriter(metaStream))
                writer.Write(metaJson);

            manifest.Pages.Add(entry);
        }

        // Save manifest
        var manifestJson = JsonSerializer.Serialize(manifest, JsonOpts);
        var manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
        using (var mStream = manifestEntry.Open())
        using (var mWriter = new StreamWriter(mStream))
            mWriter.Write(manifestJson);
    }

    /// <summary>
    /// Loads a .fink file and returns a Notebook with strokes loaded into NotePages.
    /// </summary>
    public static Notebook Load(string filePath)
    {
        using var zip = ZipFile.OpenRead(filePath);

        // Read manifest
        var manifestEntry = zip.GetEntry("manifest.json")
            ?? throw new InvalidDataException("Ungueltige .fink Datei: manifest.json fehlt.");

        FinkManifest manifest;
        using (var stream = manifestEntry.Open())
        using (var reader = new StreamReader(stream))
        {
            var json = reader.ReadToEnd();
            manifest = JsonSerializer.Deserialize<FinkManifest>(json, JsonOpts)
                ?? throw new InvalidDataException("Ungueltige .fink Datei: manifest.json kann nicht gelesen werden.");
        }

        var notebook = new Notebook
        {
            Name = manifest.Title ?? "Untitled",
            Color = manifest.Color ?? "#007AFF",
            CreatedAt = manifest.CreatedAt,
            ModifiedAt = manifest.ModifiedAt,
            AutoTitled = manifest.AutoTitled
        };

        foreach (var pageEntry in manifest.Pages)
        {
            var page = new NotePage
            {
                PageNumber = pageEntry.PageNumber,
                Template = Enum.TryParse<PageTemplateType>(pageEntry.Template, out var t) ? t : PageTemplateType.Blank,
                Zoom = pageEntry.Zoom,
                Theme = pageEntry.Theme ?? "system"
            };

            // Load strokes from ISF
            if (!string.IsNullOrEmpty(pageEntry.StrokesFile))
            {
                var strokeEntry = zip.GetEntry(pageEntry.StrokesFile);
                if (strokeEntry != null)
                {
                    using var strokeStream = strokeEntry.Open();
                    page.Strokes = new StrokeCollection(strokeStream);
                }
            }

            // Store strokes JSON path for backward compat (empty since we loaded from ISF)
            page.StrokesJson = "";

            notebook.Pages.Add(page);
        }

        notebook.PageCount = notebook.Pages.Count;
        return notebook;
    }

    /// <summary>
    /// Exports recognized text to a .txt file.
    /// </summary>
    public static void ExportRecognizedText(string filePath, string text)
    {
        File.WriteAllText(filePath, text);
    }

    /// <summary>
    /// Saves recognized text/math blocks for a page into the .fink archive.
    /// </summary>
    public static void SaveRecognizedData(string finkPath, int pageNumber, FinkRecognizedData data)
    {
        var entryPath = $"pages/page_{pageNumber:D3}/recognized.json";
        var json = JsonSerializer.Serialize(data, JsonOpts);

        using var zip = ZipFile.Open(finkPath, ZipArchiveMode.Update);
        // Remove existing entry if present
        var existing = zip.GetEntry(entryPath);
        existing?.Delete();

        var entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(json);
    }

    /// <summary>
    /// Loads recognized data for a page from a .fink archive.
    /// </summary>
    public static FinkRecognizedData? LoadRecognizedData(string finkPath, int pageNumber)
    {
        var entryPath = $"pages/page_{pageNumber:D3}/recognized.json";
        using var zip = ZipFile.OpenRead(finkPath);
        var entry = zip.GetEntry(entryPath);
        if (entry == null) return null;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<FinkRecognizedData>(json, JsonOpts);
    }

    /// <summary>
    /// Checks if a .fink file exists and is valid.
    /// </summary>
    public static bool IsValidFinkFile(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            return zip.GetEntry("manifest.json") != null;
        }
        catch { return false; }
    }

    /// <summary>
    /// Saves a PDF background image into the .fink archive for a page.
    /// </summary>
    public static void SavePdfBackground(string finkPath, int pageNumber, string imageFilePath)
    {
        var entryPath = $"pages/page_{pageNumber:D3}/background.png";
        using var zip = ZipFile.Open(finkPath, ZipArchiveMode.Update);
        var existing = zip.GetEntry(entryPath);
        existing?.Delete();

        var entry = zip.CreateEntryFromFile(imageFilePath, entryPath, CompressionLevel.Optimal);
    }
}