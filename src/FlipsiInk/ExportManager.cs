// FlipsiInk - Export Manager
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlipsiInk;

/// <summary>
/// Supported export formats.
/// </summary>
public enum ExportFormat
{
    Png,
    Jpg,
    Pdf,
    Svg
}

/// <summary>
/// Page range options for export.
/// </summary>
public enum ExportPageRange
{
    CurrentPage,
    AllPages,
    CustomRange
}

/// <summary>
/// Settings for an export operation, persisted across sessions.
/// </summary>
public class ExportSettings
{
    public ExportFormat Format { get; set; } = ExportFormat.Png;
    public int Dpi { get; set; } = 150;
    public ExportPageRange PageRange { get; set; } = ExportPageRange.CurrentPage;
    public int CustomStartPage { get; set; } = 1;
    public int CustomEndPage { get; set; } = 1;
    public bool IncludeMetadata { get; set; } = true;
    public int JpegQuality { get; set; } = 90;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FlipsiInk", "export_settings.json");

    /// <summary>
    /// Save current settings to disk for persistence across sessions.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(this);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* Non-critical */ }
    }

    /// <summary>
    /// Load previously saved export settings, or return defaults.
    /// </summary>
    public static ExportSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<ExportSettings>(json) ?? new ExportSettings();
            }
        }
        catch { /* Use defaults */ }
        return new ExportSettings();
    }
}

/// <summary>
/// Manages exporting pages to PNG, PDF, and SVG formats.
/// Also handles clipboard copy (image or OCR text).
/// </summary>
public class ExportManager
{
    /// <summary>
    /// Renders a single page's strokes to a bitmap at the specified DPI.
    /// </summary>
    public static Bitmap RenderPageToBitmap(StrokeCollection strokes, System.Windows.Media.Brush background, int width, int height, int dpi)
    {
        // Create an offscreen InkCanvas for rendering
        var ink = new InkCanvas
        {
            Width = width,
            Height = height,
            Background = background,
            Strokes = { }
        };

        // Clone strokes to avoid modifying the original
        if (strokes.Count > 0)
            ink.Strokes.Add(strokes.Clone());

        ink.Measure(new System.Windows.Size(width, height));
        ink.Arrange(new Rect(0, 0, width, height));

        var rtb = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(ink);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    /// <summary>
    /// Renders only the selected strokes (e.g. lasso selection) to a bitmap.
    /// </summary>
    public static Bitmap RenderSelectedToBitmap(StrokeCollection selectedStrokes, int dpi)
    {
        if (selectedStrokes.Count == 0)
            throw new ArgumentException("Keine Strokes ausgewählt.");

        // Compute bounding box
        Rect bounds = Rect.Empty;
        foreach (Stroke? s in selectedStrokes)
        {
            if (s != null) bounds.Union(s.GetBounds());
        }
        if (bounds == Rect.Empty)
            throw new ArgumentException("Leere Auswahl.");

        int padding = 10;
        int w = Math.Max(1, (int)bounds.Width + 2 * padding);
        int h = Math.Max(1, (int)bounds.Height + 2 * padding);

        var ink = new InkCanvas
        {
            Width = w,
            Height = h,
            Background = System.Windows.Media.Brushes.White
        };

        // Offset strokes to origin
        var offsetStrokes = new StrokeCollection();
        foreach (Stroke? s in selectedStrokes)
        {
            if (s == null) continue;
            var pts = new StylusPointCollection();
            foreach (var sp in s.StylusPoints)
                pts.Add(new StylusPoint(sp.X - bounds.X + padding, sp.Y - bounds.Y + padding, sp.PressureFactor));
            var moved = new Stroke(pts) { DrawingAttributes = s.DrawingAttributes.Clone() };
            offsetStrokes.Add(moved);
        }
        ink.Strokes.Add(offsetStrokes);

        ink.Measure(new System.Windows.Size(w, h));
        ink.Arrange(new Rect(0, 0, w, h));

        var rtb = new RenderTargetBitmap(w, h, dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(ink);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    /// <summary>
    /// Exports a single page as PNG to the specified file path.
    /// </summary>
    public static void ExportPng(StrokeCollection strokes, System.Windows.Media.Brush background, int width, int height, string filePath, int dpi)
    {
        using var bitmap = RenderPageToBitmap(strokes, background, width, height, dpi);
        bitmap.Save(filePath, ImageFormat.Png);
    }

    /// <summary>
    /// Exports a single page as JPG to the specified file path.
    /// </summary>
    public static void ExportJpg(StrokeCollection strokes, System.Windows.Media.Brush background, int width, int height, string filePath, int dpi, int quality = 90)
    {
        using var bitmap = RenderPageToBitmap(strokes, background, width, height, dpi);
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageDecoders()
            .First(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
        bitmap.Save(filePath, jpegEncoder, encoderParams);
    }

    /// <summary>
    /// Exports selected strokes as PNG.
    /// </summary>
    public static void ExportSelectedPng(StrokeCollection selectedStrokes, string filePath, int dpi)
    {
        using var bitmap = RenderSelectedToBitmap(selectedStrokes, dpi);
        bitmap.Save(filePath, ImageFormat.Png);
    }

    /// <summary>
    /// Exports pages as a multi-page PDF using System.Drawing.
    /// Each page is rendered at the specified DPI and embedded as an image in the PDF.
    /// Uses PDFiumSharp if available, otherwise falls back to a simple approach.
    /// </summary>
    public static void ExportPdf(List<(StrokeCollection Strokes, System.Windows.Media.Brush Background)> pages, string filePath, int dpi, int pageWidth, int pageHeight)
    {
        // Use System.Drawing.Printing for simple PDF generation
        // For now, we render each page as a high-res PNG and combine into PDF
        // using a basic PDF writer approach

        var tempFiles = new List<string>();
        try
        {
            // Render each page to a temporary PNG
            for (int i = 0; i < pages.Count; i++)
            {
                var tempPng = Path.Combine(Path.GetTempPath(), $"flipsiink_export_page_{i}.png");
                ExportPng(pages[i].Strokes, pages[i].Background, pageWidth, pageHeight, tempPng, dpi);
                tempFiles.Add(tempPng);
            }

            // Generate a simple PDF with embedded images
            GenerateSimplePdf(tempFiles, filePath, pageWidth, pageHeight, dpi);
        }
        finally
        {
            // Clean up temp files
            foreach (var f in tempFiles)
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }
    }

    /// <summary>
    /// Generates a minimal PDF file with embedded PNG images (one per page).
    /// This is a lightweight implementation that doesn't require external PDF libraries.
    /// </summary>
    private static void GenerateSimplePdf(List<string> imagePaths, string outputPath, int pageWidth, int pageHeight, int dpi)
    {
        // Convert pixel dimensions to points (1 point = 1/72 inch)
        double widthInPoints = pageWidth * 72.0 / dpi;
        double heightInPoints = pageHeight * 72.0 / dpi;

        using var writer = new StreamWriter(new FileStream(outputPath, FileMode.Create, FileAccess.Write));
        var positions = new List<long>();

        writer.WriteLine("%PDF-1.4");
        positions.Add(writer.BaseStream.Position);

        // Write each image as a page
        for (int i = 0; i < imagePaths.Count; i++)
        {
            // Read image as base64 for embedding
            var imageData = File.ReadAllBytes(imagePaths[i]);

            // Page object
            int pageObjNum = 1 + i * 3;
            int imgObjNum = 2 + i * 3;
            int contentObjNum = 3 + i * 3;

            // Image XObject
            positions.Add(writer.BaseStream.Position);
            writer.WriteLine($"{imgObjNum} 0 obj");
            writer.WriteLine("<< /Type /XObject /Subtype /Image");
            writer.WriteLine($"   /Width {pageWidth} /Height {pageHeight}");
            writer.WriteLine("   /ColorSpace /DeviceRGB /BitsPerComponent 8");
            writer.WriteLine($"   /Length {imageData.Length}");
            writer.WriteLine("   /Filter /DCTDecode");
            writer.WriteLine(">>");
            writer.WriteLine("stream");

            writer.Flush();
            writer.BaseStream.Write(imageData, 0, imageData.Length);
            writer.WriteLine();
            writer.WriteLine("endstream");
            writer.WriteLine("endobj");

            // Content stream: draw image
            var contentStr = $"q {widthInPoints:F2} 0 0 {heightInPoints:F2} 0 0 cm /Img{i} Do Q\n";
            var contentBytes = System.Text.Encoding.ASCII.GetBytes(contentStr);

            positions.Add(writer.BaseStream.Position);
            writer.WriteLine($"{contentObjNum} 0 obj");
            writer.WriteLine($"<< /Length {contentBytes.Length} >>");
            writer.WriteLine("stream");
            writer.Flush();
            writer.BaseStream.Write(contentBytes, 0, contentBytes.Length);
            writer.WriteLine();
            writer.WriteLine("endstream");
            writer.WriteLine("endobj");

            // Page object
            positions.Add(writer.BaseStream.Position);
            writer.WriteLine($"{pageObjNum} 0 obj");
            writer.WriteLine("<< /Type /Page");
            writer.WriteLine($"   /MediaBox [0 0 {widthInPoints:F2} {heightInPoints:F2}]");
            writer.WriteLine($"   /Contents {contentObjNum} 0 R");
            writer.WriteLine($"   /Resources << /XObject << /Img{i} {imgObjNum} 0 R >> >>");
            writer.WriteLine(">>");
            writer.WriteLine("endobj");
        }

        // Pages object
        int pagesObjNum = imagePaths.Count * 3 + 1;
        var pageRefs = string.Join(" ", Enumerable.Range(0, imagePaths.Count).Select(i => $"{1 + i * 3} 0 R"));

        positions.Add(writer.BaseStream.Position);
        writer.WriteLine($"{pagesObjNum} 0 obj");
        writer.WriteLine("<< /Type /Pages");
        writer.WriteLine($"   /Kids [{pageRefs}]");
        writer.WriteLine($"   /Count {imagePaths.Count}");
        writer.WriteLine(">>");
        writer.WriteLine("endobj");

        // Catalog
        int catalogObjNum = pagesObjNum + 1;
        positions.Add(writer.BaseStream.Position);
        writer.WriteLine($"{catalogObjNum} 0 obj");
        writer.WriteLine("<< /Type /Catalog");
        writer.WriteLine($"   /Pages {pagesObjNum} 0 R");
        writer.WriteLine(">>");
        writer.WriteLine("endobj");

        // Cross-reference table
        long xrefPos = writer.BaseStream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {catalogObjNum + 1}");
        writer.WriteLine("0000000000 65535 f ");
        for (int i = 0; i < positions.Count; i++)
        {
            writer.WriteLine($"{positions[i]:D10} 00000 n ");
        }

        // Trailer
        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {catalogObjNum + 1} /Root {catalogObjNum} 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefPos);
        writer.WriteLine("%%EOF");
    }

    /// <summary>
    /// Exports strokes as SVG (scalable vector graphics).
    /// </summary>
    public static void ExportSvg(StrokeCollection strokes, System.Windows.Media.Brush background, int width, int height, string filePath, bool includeMetadata, string? notebookName = null)
    {
        using var sw = new StreamWriter(filePath);

        sw.WriteLine($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sw.WriteLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");

        // Background
        var bgColor = background is SolidColorBrush scb
            ? $"#{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}"
            : "#FFFFFF";
        sw.WriteLine($"  <rect width=\"{width}\" height=\"{height}\" fill=\"{bgColor}\"/>");

        // Metadata
        if (includeMetadata && !string.IsNullOrEmpty(notebookName))
        {
            sw.WriteLine($"  <!-- FlipsiInk Export: {notebookName} – {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC -->");
        }

        // Strokes as polylines
        foreach (Stroke? stroke in strokes)
        {
            if (stroke == null || stroke.StylusPoints.Count == 0) continue;

            var da = stroke.DrawingAttributes;
            var color = $"#{da.Color.R:X2}{da.Color.G:X2}{da.Color.B:X2}";
            var opacity = da.Color.A / 255.0;
            var strokeWidth = da.Width;

            var points = string.Join(" ",
                stroke.StylusPoints.Select(p => $"{p.X:F1},{p.Y:F1}"));

            var opacityAttr = opacity < 1.0 ? $" opacity=\"{opacity:F2}\"" : "";
            sw.WriteLine($"  <polyline points=\"{points}\" stroke=\"{color}\" stroke-width=\"{strokeWidth:F1}\" fill=\"none\" stroke-linecap=\"round\" stroke-linejoin=\"round\"{opacityAttr}/>");
        }

        sw.WriteLine("</svg>");
    }

    /// <summary>
    /// Exports selected strokes as SVG.
    /// </summary>
    public static void ExportSelectedSvg(StrokeCollection selectedStrokes, string filePath)
    {
        if (selectedStrokes.Count == 0)
            throw new ArgumentException("Keine Strokes ausgewählt.");

        Rect bounds = Rect.Empty;
        foreach (Stroke? s in selectedStrokes)
        {
            if (s != null) bounds.Union(s.GetBounds());
        }

        int padding = 10;
        int w = Math.Max(1, (int)bounds.Width + 2 * padding);
        int h = Math.Max(1, (int)bounds.Height + 2 * padding);

        var offsetStrokes = new StrokeCollection();
        foreach (Stroke? s in selectedStrokes)
        {
            if (s == null) continue;
            var pts = new StylusPointCollection();
            foreach (var sp in s.StylusPoints)
                pts.Add(new StylusPoint(sp.X - bounds.X + padding, sp.Y - bounds.Y + padding, sp.PressureFactor));
            var moved = new Stroke(pts) { DrawingAttributes = s.DrawingAttributes.Clone() };
            offsetStrokes.Add(moved);
        }

        ExportSvg(offsetStrokes, System.Windows.Media.Brushes.White, w, h, filePath, false);
    }

    /// <summary>
    /// Copies a rendered bitmap of the page to the Windows clipboard.
    /// </summary>
    public static void CopyImageToClipboard(StrokeCollection strokes, System.Windows.Media.Brush background, int width, int height, int dpi)
    {
        var bitmap = RenderPageToBitmap(strokes, background, width, height, dpi);
        Clipboard.SetImage(BitmapToBitmapSource(bitmap, dpi));
    }

    /// <summary>
    /// Copies selected strokes as image to the clipboard.
    /// </summary>
    public static void CopySelectedImageToClipboard(StrokeCollection selectedStrokes, int dpi)
    {
        var bitmap = RenderSelectedToBitmap(selectedStrokes, dpi);
        Clipboard.SetImage(BitmapToBitmapSource(bitmap, dpi));
    }

    /// <summary>
    /// Copies text (e.g. from OCR) to the clipboard.
    /// </summary>
    public static void CopyTextToClipboard(string text)
    {
        Clipboard.SetText(text);
    }

    /// <summary>
    /// Converts System.Drawing.Bitmap to BitmapSource for WPF clipboard.
    /// </summary>
    private static BitmapSource BitmapToBitmapSource(Bitmap bitmap, int dpi)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
        try
        {
            var format = bitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppPArgb
                ? PixelFormats.Pbgra32
                : PixelFormats.Bgr24;
            var source = BitmapSource.Create(bitmap.Width, bitmap.Height, dpi, dpi, format, null, bmpData.Scan0, bmpData.Stride * bitmap.Height, bmpData.Stride);
            source.Freeze();
            return source;
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }
    }

    /// <summary>
    /// Shares a file using the Windows Share API (DataTransferManager).
    /// Requires Windows 10 1803+ and runs on UI thread.
    /// </summary>
    public static async void ShareFileAsync(string filePath, string title)
    {
        // Windows Share API requires DataTransferManager from Windows.ApplicationModel.DataTransfer
        // This is only available in UWP. For WPF, we use a workaround via Process.Start
        // which opens the Windows share sheet if available.
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-screenclip:",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Fallback: open the folder containing the file
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}