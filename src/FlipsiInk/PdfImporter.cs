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
/// Importiert PDF-Dateien und stellt Rendering-Funktionalität bereit.
/// Annotationen werden als separater Stroke-Layer über dem PDF gerendert,
/// das PDF selbst bleibt unverändert.
/// </summary>
public class PdfImporter : IDisposable
{
    private object? _pdfDocument; // TODO: NuGet Package – PdfiumViewer/PdfiumSharp PdfDocument-Typ
    private bool _disposed;

    /// <summary>
    /// Lädt eine PDF-Datei und gibt das PdfDocument zurück.
    /// </summary>
    /// <param name="filePath">Pfad zur PDF-Datei.</param>
    /// <returns>Das geladene PdfDocument-Objekt.</returns>
    public object LoadPdf(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"PDF-Datei nicht gefunden: {filePath}");

        // TODO: NuGet Package – PdfiumViewer/PdfiumSharp laden
        // _pdfDocument = PdfDocument.Load(filePath);
        // return _pdfDocument;
        throw new NotImplementedException("PDF-Import benötigt PdfiumViewer oder PdfiumSharp NuGet-Package.");
    }

    /// <summary>
    /// Gibt die Anzahl der Seiten des geladenen PDFs zurück.
    /// </summary>
    public int GetPageCount()
    {
        // TODO: NuGet Package – _pdfDocument.PageCount
        return 0;
    }

    /// <summary>
    /// Rendert eine PDF-Seite als Bitmap.
    /// </summary>
    /// <param name="pageNumber">0-basierte Seitennummer.</param>
    /// <param name="dpi">Auflösung in DPI (Standard: 150).</param>
    /// <returns>BitmapSource der gerenderten Seite.</returns>
    public BitmapSource RenderPageToBitmap(int pageNumber, double dpi = 150)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_pdfDocument is null)
            throw new InvalidOperationException("Kein PDF geladen. Zuerst LoadPdf() aufrufen.");

        if (pageNumber < 0 || pageNumber >= GetPageCount())
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Seitennummer außerhalb des gültigen Bereichs.");

        // TODO: NuGet Package – PDF-Seite rendern
        // var size = GetPageSize(pageNumber);
        // var width = (int)(size.Width * dpi / 96.0);
        // var height = (int)(size.Height * dpi / 96.0);
        // return _pdfDocument.Render(pageNumber, width, height, dpi, dpi, PdfRenderFlags.ForPrinting);
        throw new NotImplementedException("PDF-Rendering benötigt PdfiumViewer oder PdfiumSharp NuGet-Package.");
    }

    /// <summary>
    /// Rendert eine PDF-Seite als ImageSource für WPF-Image-Elemente.
    /// </summary>
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
    /// <returns>Size mit Breite und Höhe der Seite.</returns>
    public Size GetPageSize(int pageNumber)
    {
        if (_pdfDocument is null)
            throw new InvalidOperationException("Kein PDF geladen. Zuerst LoadPdf() aufrufen.");

        // TODO: NuGet Package – Seitengröße aus PDF auslesen
        // var size = _pdfDocument.Pages[pageNumber].Size;
        // return new Size(size.Width, size.Height);
        return new Size(595, 842); // A4-Fallback in Punkt
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
            // TODO: NuGet Package – _pdfDocument?.Dispose();
            _pdfDocument = null;
        }

        _disposed = true;
    }
}