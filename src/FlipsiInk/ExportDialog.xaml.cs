// FlipsiInk - Export Dialog
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Media;

namespace FlipsiInk;

/// <summary>
/// Dialog for configuring export settings: format, DPI, page range, metadata.
/// Provides Export, Copy to Clipboard, and Share actions.
/// </summary>
public partial class ExportDialog : Window
{
    private readonly PageManager _pageManager;
    private readonly InkCanvas _canvas;
    private readonly OcrEngine? _ocrEngine;
    private readonly string? _notebookName;
    private ExportSettings _settings;

    /// <summary>Whether the user initiated an export (not cancelled).</summary>
    public bool Exported { get; private set; }

    /// <summary>Path of the exported file, if any.</summary>
    public string? ExportedFilePath { get; private set; }

    public ExportDialog(PageManager pageManager, InkCanvas canvas, OcrEngine? ocrEngine, string? notebookName)
    {
        InitializeComponent();
        _pageManager = pageManager;
        _canvas = canvas;
        _ocrEngine = ocrEngine;
        _notebookName = notebookName;

        // Load last used settings
        _settings = ExportSettings.Load();
        ApplySettingsToUi(_settings);

        // Wire up buttons
        BtnExport.Click += OnExport;
        BtnCopyClipboard.Click += OnCopyClipboard;
        BtnShare.Click += OnShare;
        BtnCancel.Click += (s, e) => DialogResult = false;

        // Page range validation
        TxtRangeStart.TextChanged += ValidateRangeInputs;
        TxtRangeEnd.TextChanged += ValidateRangeInputs;

        // Update range end default
        TxtRangeEnd.Text = _pageManager.PageCount.ToString();
    }

    /// <summary>
    /// Apply saved export settings to the dialog UI controls.
    /// </summary>
    private void ApplySettingsToUi(ExportSettings settings)
    {
        RbPng.IsChecked = settings.Format == ExportFormat.Png;
        RbJpg.IsChecked = settings.Format == ExportFormat.Jpg;
        RbPdf.IsChecked = settings.Format == ExportFormat.Pdf;
        RbSvg.IsChecked = settings.Format == ExportFormat.Svg;

        RbDpi72.IsChecked = settings.Dpi == 72;
        RbDpi150.IsChecked = settings.Dpi == 150;
        RbDpi300.IsChecked = settings.Dpi == 300;

        RbCurrentPage.IsChecked = settings.PageRange == ExportPageRange.CurrentPage;
        RbAllPages.IsChecked = settings.PageRange == ExportPageRange.AllPages;
        RbCustomRange.IsChecked = settings.PageRange == ExportPageRange.CustomRange;

        TxtRangeStart.Text = settings.CustomStartPage.ToString();
        TxtRangeEnd.Text = settings.CustomEndPage.ToString();

        ChkMetadata.IsChecked = settings.IncludeMetadata;
    }

    /// <summary>
    /// Read current UI state into an ExportSettings object.
    /// </summary>
    private ExportSettings ReadSettingsFromUi()
    {
        var format = RbPng.IsChecked == true ? ExportFormat.Png
                   : RbJpg.IsChecked == true ? ExportFormat.Jpg
                   : RbPdf.IsChecked == true ? ExportFormat.Pdf
                   : ExportFormat.Svg;

        var dpi = RbDpi72.IsChecked == true ? 72
                : RbDpi150.IsChecked == true ? 150
                : 300;

        var range = RbCurrentPage.IsChecked == true ? ExportPageRange.CurrentPage
                  : RbAllPages.IsChecked == true ? ExportPageRange.AllPages
                  : ExportPageRange.CustomRange;

        int startPage = int.TryParse(TxtRangeStart.Text, out var s) ? s : 1;
        int endPage = int.TryParse(TxtRangeEnd.Text, out var e) ? e : _pageManager.PageCount;

        return new ExportSettings
        {
            Format = format,
            Dpi = dpi,
            PageRange = range,
            CustomStartPage = startPage,
            CustomEndPage = endPage,
            IncludeMetadata = ChkMetadata.IsChecked == true
        };
    }

    /// <summary>
    /// Get the list of page numbers to export based on the selected range.
    /// </summary>
    private List<int> GetPageNumbers()
    {
        var settings = ReadSettingsFromUi();
        return settings.PageRange switch
        {
            ExportPageRange.CurrentPage => new List<int> { _pageManager.CurrentPageNumber },
            ExportPageRange.AllPages => Enumerable.Range(1, _pageManager.PageCount).ToList(),
            ExportPageRange.CustomRange => Enumerable.Range(
                Math.Max(1, settings.CustomStartPage),
                Math.Min(_pageManager.PageCount, settings.CustomEndPage) - Math.Max(1, settings.CustomStartPage) + 1
            ).ToList(),
            _ => new List<int> { _pageManager.CurrentPageNumber }
        };
    }

    /// <summary>
    /// Validate custom range text inputs.
    /// </summary>
    private void ValidateRangeInputs(object? sender, EventArgs e)
    {
        // Basic validation: ensure numeric input
        if (sender is System.Windows.Controls.TextBox tb)
        {
            if (!int.TryParse(tb.Text, out _))
                tb.BorderBrush = System.Windows.Media.Brushes.Red;
            else
                tb.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));
        }
    }

    /// <summary>
    /// Get the default file extension and filter for the selected format.
    /// </summary>
    private (string Extension, string Filter) GetFileDialogInfo(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Png => (".png", "PNG-Bilder|*.png"),
            ExportFormat.Jpg => (".jpg", "JPEG-Bilder|*.jpg"),
            ExportFormat.Pdf => (".pdf", "PDF-Dokumente|*.pdf"),
            ExportFormat.Svg => (".svg", "SVG-Dateien|*.svg"),
            _ => (".png", "PNG-Bilder|*.png")
        };
    }

    /// <summary>
    /// Handle the Export button click: save to file.
    /// </summary>
    private void OnExport(object sender, RoutedEventArgs e)
    {
        _settings = ReadSettingsFromUi();
        _settings.Save(); // Persist for next time

        var pageNumbers = GetPageNumbers();
        var (ext, filter) = GetFileDialogInfo(_settings.Format);
        var defaultName = !string.IsNullOrEmpty(_notebookName) ? _notebookName : "export";

        var dialog = new SaveFileDialog
        {
            FileName = $"{defaultName}{ext}",
            Filter = filter,
            Title = "Export speichern"
        };

        if (dialog.ShowDialog() != true) return;

        var filePath = dialog.FileName;

        try
        {
            switch (_settings.Format)
            {
                case ExportFormat.Png:
                    ExportAsPng(pageNumbers, filePath);
                    break;
                case ExportFormat.Jpg:
                    ExportAsJpg(pageNumbers, filePath);
                    break;
                case ExportFormat.Pdf:
                    ExportAsPdf(pageNumbers, filePath);
                    break;
                case ExportFormat.Svg:
                    ExportAsSvg(pageNumbers, filePath);
                    break;
            }

            Exported = true;
            ExportedFilePath = filePath;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export fehlgeschlagen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Export pages as PNG. For single page, save directly. For multiple, save to a folder.
    /// </summary>
    private void ExportAsPng(List<int> pageNumbers, string filePath)
    {
        int canvasW = (int)_canvas.ActualWidth;
        int canvasH = (int)_canvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) { canvasW = 1200; canvasH = 1600; }

        if (pageNumbers.Count == 1)
        {
            var strokes = _pageManager.LoadPage(pageNumbers[0]);
            ExportManager.ExportPng(strokes, _canvas.Background, canvasW, canvasH, filePath, _settings.Dpi);
        }
        else
        {
            // Batch export: save each page as separate PNG in a subfolder
            var dir = Path.GetDirectoryName(filePath)!;
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var subDir = Path.Combine(dir, baseName + "_pages");
            Directory.CreateDirectory(subDir);

            for (int i = 0; i < pageNumbers.Count; i++)
            {
                var strokes = _pageManager.LoadPage(pageNumbers[i]);
                var pageFile = Path.Combine(subDir, $"{baseName}_Seite_{pageNumbers[i]}.png");
                ExportManager.ExportPng(strokes, _canvas.Background, canvasW, canvasH, pageFile, _settings.Dpi);
            }
        }
    }

    /// <summary>
    /// Export pages as JPG. For single page, save directly. For multiple, save to a folder.
    /// </summary>
    private void ExportAsJpg(List<int> pageNumbers, string filePath)
    {
        int canvasW = (int)_canvas.ActualWidth;
        int canvasH = (int)_canvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) { canvasW = 1200; canvasH = 1600; }

        if (pageNumbers.Count == 1)
        {
            var strokes = _pageManager.LoadPage(pageNumbers[0]);
            ExportManager.ExportJpg(strokes, _canvas.Background, canvasW, canvasH, filePath, _settings.Dpi, _settings.JpegQuality);
        }
        else
        {
            var dir = Path.GetDirectoryName(filePath)!;
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var subDir = Path.Combine(dir, baseName + "_pages");
            Directory.CreateDirectory(subDir);

            for (int i = 0; i < pageNumbers.Count; i++)
            {
                var strokes = _pageManager.LoadPage(pageNumbers[i]);
                var pageFile = Path.Combine(subDir, $"{baseName}_Seite_{pageNumbers[i]}.jpg");
                ExportManager.ExportJpg(strokes, _canvas.Background, canvasW, canvasH, pageFile, _settings.Dpi, _settings.JpegQuality);
            }
        }
    }

    /// <summary>
    /// Export pages as a multi-page PDF.
    /// </summary>
    private void ExportAsPdf(List<int> pageNumbers, string filePath)
    {
        int canvasW = (int)_canvas.ActualWidth;
        int canvasH = (int)_canvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) { canvasW = 1200; canvasH = 1600; }

        var pages = pageNumbers.Select(pn =>
        {
            var strokes = _pageManager.LoadPage(pn);
            var page = _pageManager.GetPage(pn);
            var bg = page != null ? PageTemplate.GetBackgroundBrush(page.Template) : _canvas.Background;
            return (Strokes: strokes, Background: bg);
        }).ToList();

        ExportManager.ExportPdf(pages, filePath, _settings.Dpi, canvasW, canvasH);
    }

    /// <summary>
    /// Export pages as SVG. For single page, save directly. For multiple, save each separately.
    /// </summary>
    private void ExportAsSvg(List<int> pageNumbers, string filePath)
    {
        int canvasW = (int)_canvas.ActualWidth;
        int canvasH = (int)_canvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) { canvasW = 1200; canvasH = 1600; }

        if (pageNumbers.Count == 1)
        {
            var strokes = _pageManager.LoadPage(pageNumbers[0]);
            ExportManager.ExportSvg(strokes, _canvas.Background, canvasW, canvasH, filePath, _settings.IncludeMetadata, _notebookName);
        }
        else
        {
            // Batch: separate SVG per page
            var dir = Path.GetDirectoryName(filePath)!;
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var subDir = Path.Combine(dir, baseName + "_pages");
            Directory.CreateDirectory(subDir);

            for (int i = 0; i < pageNumbers.Count; i++)
            {
                var strokes = _pageManager.LoadPage(pageNumbers[i]);
                var pageFile = Path.Combine(subDir, $"{baseName}_Seite_{pageNumbers[i]}.svg");
                ExportManager.ExportSvg(strokes, _canvas.Background, canvasW, canvasH, pageFile, _settings.IncludeMetadata, _notebookName);
            }
        }
    }

    /// <summary>
    /// Handle Copy to Clipboard: copies as image, or OCR text if available.
    /// </summary>
    private void OnCopyClipboard(object sender, RoutedEventArgs e)
    {
        _settings = ReadSettingsFromUi();
        _settings.Save();

        try
        {
            // Check if there's a selection on the canvas
            var selected = _canvas.GetSelectedStrokes();
            if (selected.Count > 0)
            {
                // Copy selected area
                ExportManager.CopySelectedImageToClipboard(selected, _settings.Dpi);
                MessageBox.Show("Auswahl als Bild in die Zwischenablage kopiert.", "Kopiert", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Copy current page as image
            int canvasW = (int)_canvas.ActualWidth;
            int canvasH = (int)_canvas.ActualHeight;
            if (canvasW <= 0 || canvasH <= 0) { canvasW = 1200; canvasH = 1600; }

            var strokes = _pageManager.LoadPage(_pageManager.CurrentPageNumber);
            ExportManager.CopyImageToClipboard(strokes, _canvas.Background, canvasW, canvasH, _settings.Dpi);
            MessageBox.Show("Seite als Bild in die Zwischenablage kopiert.", "Kopiert", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kopieren fehlgeschlagen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handle Share button: export to temp file then invoke Windows Share.
    /// </summary>
    private void OnShare(object sender, RoutedEventArgs e)
    {
        _settings = ReadSettingsFromUi();
        _settings.Save();

        try
        {
            // Export to temp file first
            var tempDir = Path.Combine(Path.GetTempPath(), "FlipsiInk_Share");
            Directory.CreateDirectory(tempDir);

            var (ext, _) = GetFileDialogInfo(_settings.Format);
            var tempFile = Path.Combine(tempDir, $"share_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");

            int canvasW = (int)_canvas.ActualWidth;
            int canvasH = (int)_canvas.ActualHeight;
            if (canvasW <= 0 || canvasH <= 0) { canvasW = 1200; canvasH = 1600; }

            var strokes = _pageManager.LoadPage(_pageManager.CurrentPageNumber);

            switch (_settings.Format)
            {
                case ExportFormat.Png:
                    ExportManager.ExportPng(strokes, _canvas.Background, canvasW, canvasH, tempFile, _settings.Dpi);
                    break;
                case ExportFormat.Svg:
                    ExportManager.ExportSvg(strokes, _canvas.Background, canvasW, canvasH, tempFile, _settings.IncludeMetadata, _notebookName);
                    break;
                default:
                    // PDF share: export as PNG for sharing compatibility
                    var pngFile = Path.Combine(tempDir, $"share_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    ExportManager.ExportPng(strokes, _canvas.Background, canvasW, canvasH, pngFile, _settings.Dpi);
                    tempFile = pngFile;
                    break;
            }

            ExportManager.ShareFileAsync(tempFile, _notebookName ?? "FlipsiInk Export");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Teilen fehlgeschlagen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}