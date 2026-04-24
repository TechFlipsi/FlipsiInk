// FlipsiInk - PDF Import via PDFiumSharp
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlipsiInk;

/// <summary>
/// Imports PDF files using PDFiumSharp and renders each page as a background image
/// for annotation. The PDF content becomes a non-editable background layer;
/// all user annotations remain as separate ink strokes on top.
/// </summary>
public class PdfImporter : IDisposable
{
    private PDFiumSharp.PdfDocument? _pdfDocument;
    private bool _disposed;

    /// <summary>
    /// Loads a PDF file and returns the page count.
    /// </summary>
    public int LoadPdf(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"PDF-Datei nicht gefunden: {filePath}");

        _pdfDocument = PDFiumSharp.PdfDocument.Load(filePath);
        return _pdfDocument.PageCount;
    }

    /// <summary>
    /// Gets the number of pages in the loaded PDF.
    /// </summary>
    public int GetPageCount()
    {
        return _pdfDocument?.PageCount ?? 0;
    }

    /// <summary>
    /// Renders a PDF page to a BitmapSource at the given DPI.
    /// </summary>
    /// <param name="pageNumber">0-based page index.</param>
    /// <param name="dpi">Render DPI (default 150).</param>
    /// <returns>BitmapSource of the rendered page.</returns>
    public BitmapSource RenderPage(int pageNumber, double dpi = 150)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pdfDocument == null)
            throw new InvalidOperationException("Kein PDF geladen. Zuerst LoadPdf() aufrufen.");
        if (pageNumber < 0 || pageNumber >= _pdfDocument.PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageNumber));

        var page = _pdfDocument[pageNumber];
        double scale = dpi / 72.0;
        int width = (int)(page.Width * scale);
        int height = (int)(page.Height * scale);

        using var bitmap = page.Render(width, height, dpi, dpi, PDFiumSharp.PdfRenderFlags.Printing);

        // Convert System.Drawing.Bitmap to WPF BitmapSource
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(width, height));
            bitmapSource.Freeze();
            return bitmapSource;
        }
        finally
        {
            // Clean up GDI handle
            DeleteObject(hBitmap);
        }
    }

    /// <summary>
    /// Renders all pages and returns them as a list of BitmapSources.
    /// </summary>
    public List<BitmapSource> RenderAllPages(double dpi = 150)
    {
        var pages = new List<BitmapSource>();
        int count = GetPageCount();
        for (int i = 0; i < count; i++)
        {
            pages.Add(RenderPage(i, dpi));
        }
        return pages;
    }

    /// <summary>
    /// Saves a rendered PDF page as a PNG file (for embedding as background in .fink).
    /// </summary>
    public void SavePageAsPng(int pageNumber, string outputPath, double dpi = 150)
    {
        var bitmap = RenderPage(pageNumber, dpi);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }

    /// <summary>
    /// Gets the page size in PDF points (1/72 inch).
    /// </summary>
    public Size GetPageSize(int pageNumber)
    {
        if (_pdfDocument == null || pageNumber < 0 || pageNumber >= _pdfDocument.PageCount)
            return new Size(595, 842); // A4 fallback

        var page = _pdfDocument[pageNumber];
        return new Size(page.Width, page.Height);
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _pdfDocument?.Dispose();
            _pdfDocument = null;
        }
        _disposed = true;
    }
}