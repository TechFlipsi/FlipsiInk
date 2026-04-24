// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlipsiInk;

/// <summary>
/// Vordefinierte Cover-Vorlagen für Notizbücher.
/// </summary>
public enum CoverTemplate
{
    SolidColor,
    Gradient,
    Striped,
    Dotted
}

/// <summary>
/// Metadaten eines Notizbuchs: Titel, Farbe, Cover, Seitenanzahl etc.
/// </summary>
public class NotebookMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Unbenannt";
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#007AFF";
    public CoverTemplate Template { get; set; } = CoverTemplate.SolidColor;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public int PageCount { get; set; }
    public string Author { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public string? CustomCoverPath { get; set; }
    public long FileSize { get; set; }
}

/// <summary>
/// Stellt visuelle Cover für Notizbücher bereit – Farben, Pinsel und
/// komplexe Cover-Visualisierungen (wie GoodNotes).
/// </summary>
public static class NotebookCover
{
    /// <summary>
    /// Vordefinierte Cover-Farben (GoodNotes-Stil).
    /// </summary>
    public static readonly Dictionary<string, string> CoverColors = new()
    {
        { "Rot", "#FF3B30" },
        { "Orange", "#FF9500" },
        { "Gelb", "#FFCC00" },
        { "Grün", "#34C759" },
        { "Blau", "#007AFF" },
        { "Violett", "#5856D6" },
        { "Rosa", "#FF2D55" },
        { "Grau", "#8E8E93" }
    };

    /// <summary>
    /// Gibt einen SolidColorBrush für die angegebene Cover-Farbe zurück.
    /// Akzeptiert sowohl vordefinierte Namen (z.B. "Rot") als auch Hex-Werte.
    /// </summary>
    public static Brush GetCoverBrush(string color)
    {
        // Vordefinierten Namen auflösen
        if (CoverColors.TryGetValue(color, out var hex))
            color = hex;

        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Erstellt ein visuelles Cover als DrawingBrush mit Farbe, Vorlage und Titel.
    /// Wird für die Cover-Ansicht im Regal-Modus verwendet.
    /// </summary>
    public static DrawingBrush CreateCoverVisual(string color, CoverTemplate template, string title)
    {
        // Vordefinierten Namen auflösen
        if (CoverColors.TryGetValue(color, out var hex))
            color = hex;

        var baseColor = (Color)ColorConverter.ConvertFromString(color);
        var darkerColor = Color.FromArgb(
            baseColor.A,
            (byte)Math.Max(0, baseColor.R - 40),
            (byte)Math.Max(0, baseColor.G - 40),
            (byte)Math.Max(0, baseColor.B - 40));
        var lighterColor = Color.FromArgb(
            baseColor.A,
            (byte)Math.Min(255, baseColor.R + 40),
            (byte)Math.Min(255, baseColor.G + 40),
            (byte)Math.Min(255, baseColor.B + 40));

        var drawing = new DrawingGroup();
        var rect = new Rect(0, 0, 200, 280);

        using (var ctx = drawing.Open())
        {
            switch (template)
            {
                case CoverTemplate.SolidColor:
                    ctx.DrawRectangle(new SolidColorBrush(baseColor), null, rect);
                    break;

                case CoverTemplate.Gradient:
                    var gradBrush = new LinearGradientBrush(
                        lighterColor, darkerColor, 45);
                    ctx.DrawRectangle(gradBrush, null, rect);
                    break;

                case CoverTemplate.Striped:
                    ctx.DrawRectangle(new SolidColorBrush(baseColor), null, rect);
                    // Horizontale Streifen
                    for (double y = 0; y < rect.Height; y += 20)
                    {
                        var stripeRect = new Rect(0, y, rect.Width, 10);
                        ctx.DrawRectangle(new SolidColorBrush(darkerColor) { Opacity = 0.15 }, null, stripeRect);
                    }
                    break;

                case CoverTemplate.Dotted:
                    ctx.DrawRectangle(new SolidColorBrush(baseColor), null, rect);
                    // Gepunktetes Muster
                    for (double x = 10; x < rect.Width; x += 20)
                    {
                        for (double y = 10; y < rect.Height; y += 20)
                        {
                            var dot = new EllipseGeometry(new Point(x, y), 2, 2);
                            ctx.DrawGeometry(new SolidColorBrush(darkerColor) { Opacity = 0.2 }, null, dot);
                        }
                    }
                    break;
            }

            // Titelleiste am unteren Rand
            var titleBarRect = new Rect(0, rect.Height - 60, rect.Width, 60);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), null, titleBarRect);

            // Titeltext
            if (!string.IsNullOrWhiteSpace(title))
            {
                var text = new FormattedText(
                    title,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    16,
                    Brushes.White,
                    1.0);
                text.MaxTextWidth = rect.Width - 20;
                text.MaxLineCount = 2;
                text.Trimming = TextTrimming.CharacterEllipsis;
                ctx.DrawText(text, new Point(10, rect.Height - 50 + (50 - text.Height) / 2));
            }

            // Dünne obere Linie als Akzent
            ctx.DrawRectangle(new SolidColorBrush(lighterColor) { Opacity = 0.5 }, null,
                new Rect(0, 0, rect.Width, 3));
        }

        var brush = new DrawingBrush(drawing)
        {
            Stretch = Stretch.Uniform,
            Viewbox = rect,
            ViewboxUnits = BrushMappingMode.Absolute
        };
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// Verwaltet Metadaten von Notizbüchern – Laden, Aktualisieren und Thumbnail-Verwaltung.
/// Persistiert als JSON-Dateien in %AppData%/FlipsiInk/metadata/.
/// </summary>
public class NotebookMetadataManager
{
    private readonly string _metadataDir;
    private readonly string _thumbnailDir;
    private readonly Dictionary<Guid, NotebookMetadata> _cache = new();

    public NotebookMetadataManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _metadataDir = Path.Combine(appData, "FlipsiInk", "metadata");
        _thumbnailDir = Path.Combine(appData, "FlipsiInk", "thumbnails");
        Directory.CreateDirectory(_metadataDir);
        Directory.CreateDirectory(_thumbnailDir);
    }

    /// <summary>
    /// Ruft die Metadaten eines Notizbuchs ab. Wird aus dem Cache oder von der
    /// JSON-Datei geladen.
    /// </summary>
    public NotebookMetadata GetMetadata(Guid notebookId)
    {
        if (_cache.TryGetValue(notebookId, out var cached))
            return cached;

        var filePath = Path.Combine(_metadataDir, $"{notebookId}.json");
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var meta = JsonSerializer.Deserialize<NotebookMetadata>(json);
                if (meta != null)
                {
                    _cache[notebookId] = meta;
                    return meta;
                }
            }
            catch { /* korrupte Datei ignorieren */ }
        }

        // Standard-Metadaten erstellen
        var defaultMeta = new NotebookMetadata { Id = notebookId };
        _cache[notebookId] = defaultMeta;
        return defaultMeta;
    }

    /// <summary>
    /// Aktualisiert die Metadaten eines Notizbuchs. Die update-Action erhält
    /// das bestehende Metadaten-Objekt und kann es verändern.
    /// </summary>
    public void UpdateMetadata(Guid notebookId, Action<NotebookMetadata> update)
    {
        var meta = GetMetadata(notebookId);
        update(meta);
        meta.ModifiedAt = DateTime.UtcNow;
        SaveMetadata(meta);
    }

    /// <summary>
    /// Formatiert eine Dateigröße menschenlesbar (z.B. "1.2 MB").
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    /// <summary>
    /// Lädt das Thumbnail-Bild eines Notizbuchs.
    /// Gibt null zurück, wenn kein Thumbnail existiert.
    /// </summary>
    public BitmapSource? LoadThumbnail(Guid notebookId)
    {
        var meta = GetMetadata(notebookId);

        // Eigenes Cover-Bild hat Vorrang
        if (meta.CustomCoverPath != null && File.Exists(meta.CustomCoverPath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(meta.CustomCoverPath, UriKind.RelativeOrAbsolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch { /* ungültiges Bild ignorieren */ }
        }

        // Thumbnail aus Thumbnail-Verzeichnis
        var thumbPath = Path.Combine(_thumbnailDir, $"{notebookId}.png");
        if (File.Exists(thumbPath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(thumbPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch { /* ungültiges Bild ignorieren */ }
        }

        return null;
    }

    /// <summary>
    /// Speichert ein Thumbnail-Bild für ein Notizbuch als PNG.
    /// </summary>
    public void SaveThumbnail(Guid notebookId, System.Drawing.Bitmap thumbnail)
    {
        var thumbPath = Path.Combine(_thumbnailDir, $"{notebookId}.png");
        thumbnail.Save(thumbPath, System.Drawing.Imaging.ImageFormat.Png);

        // Metadaten aktualisieren
        UpdateMetadata(notebookId, m =>
        {
            m.ThumbnailPath = thumbPath;
        });
    }

    private void SaveMetadata(NotebookMetadata meta)
    {
        var filePath = Path.Combine(_metadataDir, $"{meta.Id}.json");
        var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }
}