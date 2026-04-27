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
using DBitmap = System.Drawing.Bitmap;
using DSize = System.Drawing.Size;
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
<<<<<<< Updated upstream
    public Size GetPageSize(int pageNumber)
    {
        throw new NotImplementedException("PDF import requires PDFiumSharp NuGet package. Coming soon.");
=======
    /// <param name="pageNumber">0-basierte Seitennummer.</param>
    /// <param name="dpi">Auflösung in DPI (Standard: 150).</param>
    /// <returns>ImageSource für die WPF-Anzeige.</returns>
    public ImageSource RenderPageToImageSource(int pageNumber, double dpi = 150)
    {
        var bitmap = RenderPageToBitmap(pageNumber, dpi);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Gibt die Seitengröße in Punkt (PDF-Einheiten) zurück.
    /// </summary>
    /// <param name="pageNumber">0-basierte Seitennummer.</param>
    /// <returns>DSize mit Breite und Höhe der Seite.</returns>
    public DSize GetPageSize(int pageNumber)
    {
        if (_pdfDocument is null)
            throw new InvalidOperationException("Kein PDF geladen. Zuerst LoadPdf() aufrufen.");

        // TODO: NuGet Package – Seitengröße aus PDF auslesen
        // var size = _pdfDocument.Pages[pageNumber].DSize;
        // return new DSize(size.Width, size.Height);
        return new DSize(595, 842); // A4-Fallback in Punkt
    }

    /// <summary>
    /// Gibt alle Seiten als ImageSource-Liste zurück (für Multi-Page-Vorschau).
    /// </summary>
    /// <param name="dpi">Auflösung in DPI.</param>
    /// <returns>Liste der gerenderten Seiten.</returns>
    public List<ImageSource> RenderAllPages(double dpi = 150)
    {
        var pages = new List<ImageSource>();
        for (int i = 0; i < GetPageCount(); i++)
        {
            pages.Add(RenderPageToImageSource(i, dpi));
        }
        return pages;
>>>>>>> Stashed changes
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