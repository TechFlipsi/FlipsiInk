// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlipsiInk;

/// <summary>
/// Smart Snapshot Panel (Issue #36): Shows detected tables and dates
/// from OCR text with action buttons for copying/exporting.
/// </summary>
public partial class SmartSnapshotPanel : UserControl
{
    private readonly List<Action> _cleanupActions = new();

    public SmartSnapshotPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the panel with detected tables and dates from OCR text.
    /// </summary>
    public void UpdateContent(
        List<TableDetector.DetectedTable> tables,
        List<DateDetector.DetectedDate> dates)
    {
        // Clear previous content
        TableContentPanel.Children.Clear();
        DateContentPanel.Children.Clear();
        _cleanupActions.ForEach(a => a());
        _cleanupActions.Clear();

        bool hasContent = false;

        // --- Tables ---
        if (tables.Count > 0)
        {
            TableExpander.Visibility = Visibility.Visible;
            hasContent = true;

            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                var border = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(6),
                    CornerRadius = new CornerRadius(4)
                };

                var stack = new StackPanel();

                // Table preview header
                var header = new TextBlock
                {
                    Text = $"📊 Tabelle {i + 1} ({table.Cells.Length}×{table.Cells[0].Length})",
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stack.Children.Add(header);

                // Table preview (grid)
                var preview = CreateTablePreview(table);
                stack.Children.Add(preview);

                // Action buttons
                var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

                var btnCopyTsv = new Button
                {
                    Content = "📋 Als Tabelle kopieren",
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 4, 0),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                btnCopyTsv.Click += (s, e) =>
                {
                    Clipboard.SetText(TableDetector.FormatAsTsv(table));
                    ShowCopiedFeedback(btnCopyTsv);
                };
                btnPanel.Children.Add(btnCopyTsv);

                var btnCopyCsv = new Button
                {
                    Content = "📄 Als CSV kopieren",
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 4, 0),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                btnCopyCsv.Click += (s, e) =>
                {
                    Clipboard.SetText(TableDetector.FormatAsCsv(table));
                    ShowCopiedFeedback(btnCopyCsv);
                };
                btnPanel.Children.Add(btnCopyCsv);

                var btnCopyMd = new Button
                {
                    Content = "📝 Als Markdown",
                    FontSize = 11,
                    Padding = new Thickness(6, 2, 6, 2)
                };
                btnCopyMd.Click += (s, e) =>
                {
                    Clipboard.SetText(TableDetector.FormatAsMarkdown(table));
                    ShowCopiedFeedback(btnCopyMd);
                };
                btnPanel.Children.Add(btnCopyMd);

                stack.Children.Add(btnPanel);
                border.Child = stack;
                TableContentPanel.Children.Add(border);
            }
        }
        else
        {
            TableExpander.Visibility = Visibility.Collapsed;
        }

        // --- Dates ---
        if (dates.Count > 0)
        {
            DateExpander.Visibility = Visibility.Visible;
            hasContent = true;

            foreach (var dateEvent in dates)
            {
                var border = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(6),
                    CornerRadius = new CornerRadius(4)
                };

                var stack = new StackPanel();

                // Date preview
                string dateLabel = dateEvent.DateTime?.ToString("dd.MM.yyyy") ?? dateEvent.RawText;
                if (dateEvent.HasTime && dateEvent.Time != null)
                    dateLabel += $" um {dateEvent.Time.Value:hh\\:mm}";

                var header = new TextBlock
                {
                    Text = $"📅 {dateLabel}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stack.Children.Add(header);

                // Raw text
                var raw = new TextBlock
                {
                    Text = $"Erkannt: \"{dateEvent.RawText}\"",
                    FontSize = 10,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stack.Children.Add(raw);

                // Action buttons
                var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };

                var btnIcs = new Button
                {
                    Content = "📅 Als Termin übernehmen",
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 4, 0),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                btnIcs.Click += (s, e) => ExportAsIcs(dateEvent);
                btnPanel.Children.Add(btnIcs);

                var btnCopyDate = new Button
                {
                    Content = "📋 Datum kopieren",
                    FontSize = 11,
                    Padding = new Thickness(6, 2, 6, 2)
                };
                btnCopyDate.Click += (s, e) =>
                {
                    Clipboard.SetText(dateLabel);
                    ShowCopiedFeedback(btnCopyDate);
                };
                btnPanel.Children.Add(btnCopyDate);

                stack.Children.Add(btnPanel);
                border.Child = stack;
                DateContentPanel.Children.Add(border);
            }
        }
        else
        {
            DateExpander.Visibility = Visibility.Collapsed;
        }

        // Show/hide empty message
        NoContentMessage.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Creates a simple grid preview for a detected table.
    /// </summary>
    private Grid CreateTablePreview(TableDetector.DetectedTable table)
    {
        var grid = new Grid();
        grid.Margin = new Thickness(0, 0, 0, 2);

        // Add columns
        int maxCols = table.Cells.Max(r => r.Length);
        for (int c = 0; c < maxCols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Add rows with cell content
        for (int r = 0; r < table.Cells.Length; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            for (int c = 0; c < table.Cells[r].Length; c++)
            {
                var cell = new TextBlock
                {
                    Text = table.Cells[r][c],
                    FontSize = 10,
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(1)
                };

                // Header row styling
                if (r == 0)
                {
                    cell.FontWeight = FontWeights.Bold;
                    cell.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF"));
                }

                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        return grid;
    }

    private void ExportAsIcs(DateDetector.DetectedDate dateEvent)
    {
        try
        {
            string ics = DateDetector.GenerateIcs(dateEvent, "Termin aus FlipsiInk", dateEvent.RawText);
            string tempPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"flipsi_termin_{DateTime.Now:yyyyMMdd_HHmmss}.ics");
            System.IO.File.WriteAllText(tempPath, ics);

            // Open the .ics file with the default calendar app
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            });

            StatusText.Text = "✓ Termin als .ics exportiert";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✗ Fehler beim Export: {ex.Message}";
        }
    }

    private void ShowCopiedFeedback(Button btn)
    {
        var original = btn.Content;
        btn.Content = "✓ Kopiert!";
        btn.Dispatcher.BeginInvoke(new Action(() =>
        {
            btn.Content = original;
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    // Status text accessor — set by MainWindow when wiring up
    public TextBlock? StatusText { get; set; }
}