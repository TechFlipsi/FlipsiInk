// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlipsiInk;

/// <summary>
/// Dashboard window showing all notebooks with sidebar navigation.
/// </summary>
public partial class NotebookListView : Window
{
    private readonly NoteManager _noteManager;
    private readonly NotebookMetadataManager _metaManager;
    private bool _isGridView = true;
    private string _currentFilter = "all"; // all, favorites, recent

    /// <summary>Fired when user opens a notebook.</summary>
    public event Action<Notebook>? NotebookOpened;

    /// <summary>Fired when user creates a new notebook.</summary>
    public event Action? NewNotebookRequested;

    public NotebookListView(NoteManager noteManager, NotebookMetadataManager metaManager)
    {
        InitializeComponent();
        _noteManager = noteManager;
        _metaManager = metaManager;

        SearchBox.TextChanged += SearchBox_TextChanged;
        BtnNewNotebook.Click += (s, e) => NewNotebookRequested?.Invoke();

        RefreshList();
    }

    /// <summary>Refreshes the notebook list.</summary>
    public void RefreshList(string? searchFilter = null)
    {
        if (NotebookGrid == null) return;
        NotebookGrid.Items.Clear();

        var notebooks = _noteManager.GetNotebooksInFolder(null);
        var folders = _noteManager.GetRootFolders();
        foreach (var folder in folders)
        {
            notebooks.AddRange(_noteManager.GetNotebooksInFolder(folder.Id));
            foreach (var child in folder.Children)
                notebooks.AddRange(_noteManager.GetNotebooksInFolder(child.Id));
        }

        var distinct = notebooks.DistinctBy(n => n.Id).ToList();

        // Apply navigation filter
        if (_currentFilter == "favorites")
            distinct = distinct.Where(n => n.IsFavorite).ToList();
        else if (_currentFilter == "recent")
            distinct = distinct.OrderByDescending(n => n.ModifiedAt).Take(10).ToList();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            var q = searchFilter.Trim();
            distinct = distinct.Where(n =>
            {
                var meta = _metaManager.GetMetadata(n.Id);
                return n.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (meta.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (meta.Author?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
            }).ToList();
        }

        // Sort by last modified
        if (_currentFilter != "recent")
            distinct = distinct.OrderByDescending(n => n.ModifiedAt).ToList();

        // Update UI counts
        SidebarNoteCount.Text = $"{distinct.Count} Notizb\u00FCcher";
        FilterCount.Text = distinct.Count > 0 ? $"({distinct.Count})" : "";

        foreach (var nb in distinct)
        {
            var meta = _metaManager.GetMetadata(nb.Id);
            var card = BuildCard(nb, meta);
            NotebookGrid.Items.Add(card);
        }
    }

    private Border BuildCard(Notebook nb, NotebookMetadata meta)
    {
        var card = new Border
        {
            Width = 180,
            Height = 240,
            Margin = new Thickness(8),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
            Cursor = Cursors.Hand,
            Tag = nb
        };

        // Hover effect
        card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
        card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));

        // Double-click to open
        card.MouseLeftButtonUp += (s, e) =>
        {
            NotebookOpened?.Invoke(nb);
            DialogResult = true;
            Close();
        };

        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };

        // Cover preview area
        var coverArea = new Border
        {
            Height = 140,
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Background = NotebookCover.CreateCoverVisual(nb.Color, meta.Template, nb.Name)
        };

        // Favorite indicator
        if (nb.IsFavorite)
        {
            var star = new TextBlock
            {
                Text = "\u2605",
                FontSize = 16,
                Foreground = new SolidColorBrush(Colors.Gold),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, 8, 0)
            };
            var grid = new Grid();
            grid.Children.Add(coverArea);
            grid.Children.Add(star);
            stack.Children.Add(grid);
        }
        else
        {
            stack.Children.Add(coverArea);
        }

        // Info area
        var info = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };
        info.Children.Add(new TextBlock
        {
            Text = nb.Name,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        info.Children.Add(new TextBlock
        {
            Text = $"{meta.PageCount} Seiten \u00B7 {nb.ModifiedAt.ToLocalTime():dd.MM.yyyy}",
            Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0)
        });

        stack.Children.Add(info);
        card.Child = stack;

        // Context menu
        var menu = new ContextMenu();
        var openItem = new MenuItem { Header = "\u00D6ffnen" };
        openItem.Click += (s, e) => { NotebookOpened?.Invoke(nb); DialogResult = true; Close(); };
        var propItem = new MenuItem { Header = "Eigenschaften" };
        propItem.Click += (s, e) => OpenProperties(nb);
        var favItem = new MenuItem { Header = nb.IsFavorite ? "\u2605 Favorit entfernen" : "\u2606 Favorit setzen" };
        favItem.Click += (s, e) => ToggleFavorite(nb);
        var deleteItem = new MenuItem { Header = "L\u00F6schen" };
        deleteItem.Click += (s, e) => DeleteNotebook(nb);

        menu.Items.Add(openItem);
        menu.Items.Add(propItem);
        menu.Items.Add(favItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(deleteItem);
        card.ContextMenu = menu;

        return card;
    }

    private void OpenProperties(Notebook nb)
    {
        var meta = _metaManager.GetMetadata(nb.Id);
        var dialog = new NotebookCoverDialog(meta);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            nb.Name = dialog.Result.Title;
            nb.Color = dialog.Result.Color;
            nb.ModifiedAt = DateTime.UtcNow;
            _metaManager.UpdateMetadata(nb.Id, m =>
            {
                m.Title = dialog.Result.Title;
                m.Description = dialog.Result.Description;
                m.Color = dialog.Result.Color;
                m.Template = dialog.Result.Template;
            });
            _noteManager.SaveNotebook(nb);
            RefreshList(SearchBox.Text);
        }
    }

    private void ToggleFavorite(Notebook nb)
    {
        nb.IsFavorite = !nb.IsFavorite;
        nb.ModifiedAt = DateTime.UtcNow;
        _noteManager.SaveNotebook(nb);
        RefreshList(SearchBox.Text);
    }

    private void DeleteNotebook(Notebook nb)
    {
        var result = MessageBox.Show(
            $"Notizbuch \"{nb.Name}\" wirklich l\u00F6schen?",
            "L\u00F6schen best\u00E4tigen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _noteManager.DeleteNotebook(nb.Id);
            RefreshList(SearchBox.Text);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshList(SearchBox.Text);
    }

    // Navigation sidebar handlers
    private void NavAll_Click(object sender, RoutedEventArgs e)
    {
        _currentFilter = "all";
        SectionTitle.Text = "Alle Notizb\u00FCcher";
        RefreshList(SearchBox.Text);
    }

    private void NavFavorites_Click(object sender, RoutedEventArgs e)
    {
        _currentFilter = "favorites";
        SectionTitle.Text = "Favoriten";
        RefreshList(SearchBox.Text);
    }

    private void NavRecent_Click(object sender, RoutedEventArgs e)
    {
        _currentFilter = "recent";
        SectionTitle.Text = "K\u00FCrzlich ge\u00F6ffnet";
        RefreshList(SearchBox.Text);
    }

    private void NavFolderRoot_Click(object sender, RoutedEventArgs e)
    {
        _currentFilter = "all";
        SectionTitle.Text = "Alle Dateien";
        RefreshList(SearchBox.Text);
    }

    private void BtnToggleView_Click(object sender, RoutedEventArgs e)
    {
        _isGridView = !_isGridView;
        BtnToggleView.Content = _isGridView ? "\u25A6" : "\u2630";
        RefreshList(SearchBox.Text);
    }

    private void BtnSortBy_Click(object sender, RoutedEventArgs e)
    {
        // Simple sort toggle - could expand to popup menu
        // Sort toggle: name ↔ date
        var currentOrder = App.Config.Setting_NotebookSortOrder;
        App.Config.Setting_NotebookSortOrder = currentOrder == "name" ? "date" : "name";
        App.Config.Save();
        RefreshList(SearchBox.Text);
    }

    private void BtnNewNotebook_Click(object sender, RoutedEventArgs e)
    {
        NewNotebookRequested?.Invoke();
    }

    private void NotebookList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Legacy - kept for compatibility
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e) { }
    private void MenuProperties_Click(object sender, RoutedEventArgs e) { }
    private void MenuFavorite_Click(object sender, RoutedEventArgs e) { }
    private void MenuDelete_Click(object sender, RoutedEventArgs e) { }
}