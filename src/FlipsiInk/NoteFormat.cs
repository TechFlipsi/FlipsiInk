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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace FlipsiInk;

/// <summary>
/// Custom .fink note format – JSON-based, stores strokes as vector data (NOT images),
/// page template, metadata, page flags, and auto-title.
/// </summary>
public static class NoteFormat
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public const string FileExtension = ".fink";
    public const string FormatVersion = "0.4.0";

    // ─── Save ────────────────────────────────────────────────────────

    /// <summary>
    /// Saves a notebook to a .fink file (JSON-based, vector strokes, metadata included).
    /// </summary>
    public static void SaveNotebook(Notebook notebook, string filePath)
    {
        var doc = new FinkDocument
        {
            FormatVersion = FormatVersion,
            Id = notebook.Id,
            Name = notebook.Name,
            Color = notebook.Color,
            CreatedAt = notebook.CreatedAt,
            ModifiedAt = DateTime.UtcNow,
            Template = notebook.Template,
            IsFavorite = notebook.IsFavorite,
            AutoTitle = notebook.AutoTitle ?? string.Empty,
            Pages = new List<FinkPage>()
        };

        foreach (var page in notebook.Pages)
        {
            var finkPage = new FinkPage
            {
                Id = page.Id,
                PageNumber = page.PageNumber,
                Template = page.Template,
                Zoom = page.Zoom,
                Theme = page.Theme,
                FlagColor = page.FlagColor,
                IsFlagged = page.IsFlagged,
                Strokes = ConvertStrokesToFink(page.Strokes)
            };
            doc.Pages.Add(finkPage);
        }

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(doc, JsonOpts);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Loads a notebook from a .fink file.
    /// </summary>
    public static Notebook LoadNotebook(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var doc = JsonSerializer.Deserialize<FinkDocument>(json, JsonOpts)
            ?? throw new InvalidDataException($"Failed to parse .fink file: {filePath}");

        var notebook = new Notebook
        {
            Id = doc.Id,
            Name = doc.Name ?? "Unbenannt",
            Color = doc.Color ?? "#FF9500",
            CreatedAt = doc.CreatedAt,
            ModifiedAt = doc.ModifiedAt,
            Template = doc.Template,
            IsFavorite = doc.IsFavorite,
            AutoTitle = doc.AutoTitle ?? string.Empty,
            Pages = new List<NotePage>()
        };

        foreach (var finkPage in doc.Pages)
        {
            var page = new NotePage
            {
                Id = finkPage.Id,
                PageNumber = finkPage.PageNumber,
                Template = finkPage.Template,
                Zoom = finkPage.Zoom,
                Theme = finkPage.Theme ?? "system",
                FlagColor = finkPage.FlagColor,
                IsFlagged = finkPage.IsFlagged,
                Strokes = ConvertFinkToStrokes(finkPage.Strokes)
            };
            notebook.Pages.Add(page);
        }

        notebook.PageCount = notebook.Pages.Count;
        return notebook;
    }

    /// <summary>
    /// Checks if a file is a valid .fink file.
    /// </summary>
    public static bool IsFinkFile(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        if (!filePath.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var json = File.ReadAllText(filePath);
            var doc = JsonSerializer.Deserialize<FinkDocument>(json, JsonOpts);
            return doc?.FormatVersion != null;
        }
        catch { return false; }
    }

    // ─── Stroke Conversion ───────────────────────────────────────────

    private static List<FinkStroke> ConvertStrokesToFink(StrokeCollection strokes)
    {
        var result = new List<FinkStroke>();
        foreach (var stroke in strokes)
        {
            var finkStroke = new FinkStroke
            {
                Color = stroke.DrawingAttributes.Color.ToString(),
                Width = stroke.DrawingAttributes.Width,
                Height = stroke.DrawingAttributes.Height,
                IsHighlighter = stroke.DrawingAttributes.IsHighlighter,
                FitToCurve = stroke.DrawingAttributes.FitToCurve,
                StylusTip = stroke.DrawingAttributes.StylusTip == StylusTip.Ellipse ? "ellipse" : "rectangle",
                Points = new List<FinkPoint>()
            };

            foreach (var point in stroke.StylusPoints)
            {
                finkStroke.Points.Add(new FinkPoint
                {
                    X = point.X,
                    Y = point.Y,
                    PressureFactor = point.PressureFactor
                });
            }

            result.Add(finkStroke);
        }
        return result;
    }

    private static StrokeCollection ConvertFinkToStrokes(List<FinkStroke> finkStrokes)
    {
        var collection = new StrokeCollection();
        if (finkStrokes == null) return collection;

        foreach (var fs in finkStrokes)
        {
            if (fs.Points == null || fs.Points.Count == 0) continue;

            var points = new StylusPointCollection(fs.Points.Count);
            foreach (var p in fs.Points)
            {
                points.Add(new StylusPoint(p.X, p.Y, p.PressureFactor));
            }

            var stroke = new Stroke(points);
            try { stroke.DrawingAttributes.Color = (Color)ColorConverter.ConvertFromString(fs.Color ?? "#000000"); }
            catch { stroke.DrawingAttributes.Color = Colors.Black; }
            stroke.DrawingAttributes.Width = fs.Width > 0 ? fs.Width : 2;
            stroke.DrawingAttributes.Height = fs.Height > 0 ? fs.Height : 2;
            stroke.DrawingAttributes.IsHighlighter = fs.IsHighlighter;
            stroke.DrawingAttributes.FitToCurve = fs.FitToCurve;
            stroke.DrawingAttributes.StylusTip = fs.StylusTip == "rectangle" ? StylusTip.Rectangle : StylusTip.Ellipse;

            collection.Add(stroke);
        }
        return collection;
    }
}

// ─── Fink Document Model ─────────────────────────────────────────────

public class FinkDocument
{
    public string FormatVersion { get; set; } = NoteFormat.FormatVersion;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Unbenannt";
    public string Color { get; set; } = "#FF9500";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public PageTemplateType Template { get; set; } = PageTemplateType.LinedWide;
    public bool IsFavorite { get; set; }
    public string AutoTitle { get; set; } = string.Empty;
    public List<FinkPage> Pages { get; set; } = new();
}

public class FinkPage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PageNumber { get; set; }
    public PageTemplateType Template { get; set; } = PageTemplateType.Blank;
    public double Zoom { get; set; } = 1.0;
    public string Theme { get; set; } = "system";
    /// <summary>Whether this page is flagged/marked for quick navigation.</summary>
    public bool IsFlagged { get; set; }
    /// <summary>Flag color as hex string (e.g. "#FF0000" for red).</summary>
    public string FlagColor { get; set; } = string.Empty;
    public List<FinkStroke> Strokes { get; set; } = new();
}

public class FinkStroke
{
    public string Color { get; set; } = "#000000";
    public double Width { get; set; } = 2;
    public double Height { get; set; } = 2;
    public bool IsHighlighter { get; set; }
    public bool FitToCurve { get; set; } = true;
    public string StylusTip { get; set; } = "ellipse";
    public List<FinkPoint> Points { get; set; } = new();
}

public class FinkPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public float PressureFactor { get; set; } = 0.5f;
}