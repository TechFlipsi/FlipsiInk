// FlipsiInk - AI-powered Handwriting & Math Notes App
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
using PDFiumSharp;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlipsiInk;

/// <summary>
/// Imports PDF files using PDFiumSharp. Each page becomes a note page background.
/// v0.4.0: Fully functional with PDFiumSharp NuGet package.
/// </summary>
public class PdfImporter : IDisposable
{
    private PdfDocument? _pdfDocument;
    private bool _disposed;
    private int _pageCount;

    /// <summary>Default DPI for PDF rendering.</summary>
    public double DefaultDpi { get; set; } = 150;

    /// <summary>Gets the number of pages in the loaded PDF.</summary>
    public int PageCount => _pageCount;

    /// <summary>
    /// Loads a PDF file. Returns the page count.
    /// </summary>
    public int LoadPdf(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"PDF file not found: {filePath}");

        _pdfDocument = PdfDocument.Load(filePath);
        _pageCount = _pdfDocument?.Pages.Count ?? 0;
        return _pageCount;
    }

    /// <summary>
    /// Renders a PDF page as a BitmapSource for WPF display.
    /// </summary>
    /// <param name="pageNumber">0-based page number.</param>
    /// <param name="dpi">Resolution in DPI.</param>
    /// <returns>BitmapSource of the rendered page.</returns>
    public BitmapSource RenderPageToImageSource(int pageNumber, double dpi = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_pdfDocument == null)
            throw new InvalidOperationException("No PDF loaded. Call LoadPdf() first.");

        if (pageNumber < 0 || pageNumber >= _pageCount)
            throw new ArgumentOutOfRangeException(nameof(pageNumber));

        dpi = dpi > 0 ? dpi : DefaultDpi;

        var page = _pdfDocument.Pages[pageNumber];
        double width = page.Width * dpi / 72.0;
        double height = page.Height * dpi / 72.0;

        using var bmp = new Bitmap((int)width, (int)height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.White);
            page.Render(g, 0, 0, (int)width, (int)height, PageRotate.Normal, RenderFlags.Normal);
        }

        var hBitmap = bmp.GetHbitmap();
        try
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight((int)width, (int)height));
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    /// <summary>
    /// Renders all pages as ImageSource list (for batch import).
    /// </summary>
    /// <param name="dpi">Resolution in DPI.</param>
    /// <returns>List of rendered pages.</returns>
    public List<BitmapSource> RenderAllPages(double dpi = 0)
    {
        var pages = new List<BitmapSource>();
        for (int i = 0; i < _pageCount; i++)
        {
            pages.Add(RenderPageToImageSource(i, dpi));
        }
        return pages;
    }

    /// <summary>
    /// Gets the page size in points (PDF units).
    /// </summary>
    public Size GetPageSize(int pageNumber)
    {
        if (_pdfDocument == null)
            throw new InvalidOperationException("No PDF loaded. Call LoadPdf() first.");

        if (pageNumber < 0 || pageNumber >= _pageCount)
            throw new ArgumentOutOfRangeException(nameof(pageNumber));

        var page = _pdfDocument.Pages[pageNumber];
        return new Size(page.Width, page.Height);
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
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