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
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlipsiInk;

/// <summary>
/// Imports PDF files using PDFiumSharp. Each page becomes a note page background.
/// v0.4.0: Stub - PDFiumSharp integration needs NuGet package at build time.
/// </summary>
public class PdfImporter : IDisposable
{
    private bool _disposed;

    /// <summary>Default DPI for PDF rendering.</summary>
    public double DefaultDpi { get; set; } = 150;

    /// <summary>Gets the number of pages in the loaded PDF.</summary>
    public int PageCount { get; private set; }

    /// <summary>
    /// Loads a PDF file. Returns the page count.
    /// TODO: Implement with PDFiumSharp once NuGet restore is confirmed working.
    /// </summary>
    public int LoadPdf(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"PDF file not found: {filePath}");

        // TODO: Implement PDFiumSharp loading
        // _pdfDocument = PdfDocument.Load(filePath);
        // PageCount = _pdfDocument?.Pages.Count ?? 0;
        throw new NotImplementedException("PDF import requires PDFiumSharp NuGet package. Coming soon.");
    }

    /// <summary>
    /// Renders a PDF page as a BitmapSource for WPF display.
    /// </summary>
    public BitmapSource RenderPage(int pageNumber, double dpi = 0)
    {
        dpi = dpi > 0 ? dpi : DefaultDpi;
        throw new NotImplementedException("PDF import requires PDFiumSharp NuGet package. Coming soon.");
    }

    /// <summary>
    /// Renders all pages as BitmapSource list.
    /// </summary>
    public List<BitmapSource> RenderAllPages(double dpi = 0)
    {
        dpi = dpi > 0 ? dpi : DefaultDpi;
        throw new NotImplementedException("PDF import requires PDFiumSharp NuGet package. Coming soon.");
    }

    /// <summary>
    /// Gets the page size in points (PDF units).
    /// </summary>
    public Size GetPageSize(int pageNumber)
    {
        throw new NotImplementedException("PDF import requires PDFiumSharp NuGet package. Coming soon.");
    }

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
            // _pdfDocument?.Dispose();
        }
        _disposed = true;
    }
}