// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace FlipsiInk;

public partial class SearchWindow : Window
{
    private readonly NoteSearchIndex _searchIndex;

    public string? SelectedFilename { get; private set; }

    public SearchWindow(NoteSearchIndex searchIndex)
    {
        InitializeComponent();
        _searchIndex = searchIndex;
        SearchBox.Focus();
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        PerformSearch();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformSearch();
            e.Handled = true;
        }
    }

    private void PerformSearch()
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            StatusLabel.Text = "Bitte Suchbegriff eingeben";
            return;
        }

        StatusLabel.Text = "Suche...";
        ResultsList.ItemsSource = null;

        try
        {
            var results = _searchIndex.SearchNotes(query);

            // Display-Items mit formatiertem Datum erstellen
            var displayItems = results.Select(r => new
            {
                r.Id,
                r.Filename,
                r.Snippet,
                r.Text,
                DisplayDate = FormatDate(r.Timestamp)
            }).ToList();

            ResultsList.ItemsSource = displayItems;
            ResultCount.Text = results.Count > 0 ? $"({results.Count} Treffer)" : "";
            StatusLabel.Text = results.Count > 0
                ? $"Gefunden in {results.Count} Notizen"
                : "Keine Treffer";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Fehler bei der Suche: {ex.Message}";
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem == null) return;

        // Dynamisch den Filename aus dem anonymen Typ holen
        var item = ResultsList.SelectedItem;
        var prop = item.GetType().GetProperty("Filename");
        if (prop != null)
        {
            SelectedFilename = prop.GetValue(item)?.ToString();
            DialogResult = true;
            Close();
        }
    }

    private static string FormatDate(string timestamp)
    {
        if (DateTime.TryParse(timestamp, out var dt))
            return dt.ToString("dd.MM.yyyy HH:mm");
        return timestamp;
    }
}