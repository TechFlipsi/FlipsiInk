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
/// Window showing all notebooks in a grid or list view with cover thumbnails.
/// Supports searching by title, author, description and right-click properties.
/// </summary>
public partial class NotebookListView : Window
{
    private readonly NoteManager _noteManager;
    private readonly NotebookMetadataManager _metaManager;
    private bool _isGridView = true;

    /// <summary>Fired when user opens a notebook. Passes the Notebook object.</summary>
    public event Action<Notebook>? NotebookOpened;

    /// <summary>Fired when user creates a new notebook (with cover dialog).</summary>
    public event Action? NewNotebookRequested;

    public NotebookListView(NoteManager noteManager, NotebookMetadataManager metaManager)
    {
        InitializeComponent();
        _noteManager = noteManager;
        _metaManager = metaManager;

        BtnNewNotebook.Click += (s, e) => NewNotebookRequested?.Invoke();
        BtnToggleView.Click += ToggleView;
        SearchBox.TextChanged += SearchBox_TextChanged;

        RefreshList();
    }

    /// <summary>Refreshes the notebook list from NoteManager and metadata.</summary>
    public void RefreshList(string? filter = null)
    {
        NotebookList.Items.Clear();

        var notebooks = _noteManager.GetNotebooksInFolder(null); // all root notebooks
        // Also get notebooks in folders
        var folders = _noteManager.GetRootFolders();
        foreach (var folder in folders)
        {
            notebooks.AddRange(_noteManager.GetNotebooksInFolder(folder.Id));
            foreach (var child in folder.Children)
                notebooks.AddRange(_noteManager.GetNotebooksInFolder(child.Id));
        }

        // Deduplicate
        var distinct = notebooks.DistinctBy(n => n.Id).ToList();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var q = filter.Trim();
            distinct = distinct.Where(n =>
            {
                var meta = _metaManager.GetMetadata(n.Id);
                return n.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || meta.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (meta.Author?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
            }).ToList();
        }

        // Sort by last modified
        distinct = distinct.OrderByDescending(n => n.ModifiedAt).ToList();

        if (_isGridView)
            SetGridTemplate();
        else
            SetListTemplate();

        foreach (var nb in distinct)
        {
            var meta = _metaManager.GetMetadata(nb.Id);
            var item = _isGridView ? BuildGridItem(nb, meta) : BuildListItem(nb, meta);
            NotebookList.Items.Add(item);
        }
    }

    private void SetGridTemplate()
    {
        var panel = NotebookList.ItemsPanel.LoadContent() as WrapPanel;
        if (panel != null)
        {
            panel.ItemWidth = 180;
            panel.ItemHeight = 280;
        }
    }

    private void SetListTemplate()
    {
        // For list view we use a StackPanel
    }

    private ListBoxItem BuildGridItem(Notebook nb, NotebookMetadata meta)
    {
        var border = new Border
        {
            Width = 170,
            Height = 260,
            Margin = new Thickness(6),
            CornerRadius = new CornerRadius(8),
            Background = NotebookCover.CreateCoverVisual(nb.Color, meta.Template, nb.Name),
            Cursor = Cursors.Hand,
            Tag = nb
        };

        // Title overlay at bottom
        var titlePanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var titleText = new TextBlock
        {
            Text = nb.Name,
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10, 0, 10, 2)
        };
        var infoText = new TextBlock
        {
            Text = $"{meta.PageCount} Seiten · {NotebookMetadataManager.FormatFileSize(meta.FileSize)}",
            Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            FontSize = 10,
            Margin = new Thickness(10, 0, 10, 0)
        };
        titlePanel.Children.Add(titleText);
        titlePanel.Children.Add(infoText);

        // Favorite star
        if (nb.IsFavorite)
        {
            var star = new TextBlock
            {
                Text = "⭐",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, 8, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            var grid = new Grid();
            grid.Children.Add(titlePanel);
            grid.Children.Add(star);
            border.Child = grid;
        }
        else
        {
            border.Child = titlePanel;
        }

        var item = new ListBoxItem { Content = border, Tag = nb };
        return item;
    }

    private ListBoxItem BuildListItem(Notebook nb, NotebookMetadata meta)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };

        // Small color swatch
        var swatch = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(6),
            Background = NotebookCover.GetCoverBrush(nb.Color),
            Margin = new Thickness(0, 0, 12, 0)
        };
        stack.Children.Add(swatch);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock
        {
            Text = nb.Name,
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 14
        });
        info.Children.Add(new TextBlock
        {
            Text = $"{meta.PageCount} Seiten · Geändert: {meta.ModifiedAt.ToLocalTime():dd.MM.yyyy HH:mm}" +
                   (nb.IsFavorite ? " · ⭐" : ""),
            Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
            FontSize = 11
        });
        if (!string.IsNullOrWhiteSpace(meta.Description))
        {
            info.Children.Add(new TextBlock
            {
                Text = meta.Description.Length > 80 ? meta.Description[..80] + "…" : meta.Description,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }
        stack.Children.Add(info);

        return new ListBoxItem { Content = stack, Tag = nb };
    }

    private void ToggleView(object sender, RoutedEventArgs e)
    {
        _isGridView = !_isGridView;
        BtnToggleView.Content = _isGridView ? "📊" : "📋";

        // Update ItemsPanel template
        if (_isGridView)
        {
            NotebookList.ItemsPanel = CreateItemsPanelTemplate(true);
        }
        else
        {
            NotebookList.ItemsPanel = CreateItemsPanelTemplate(false);
        }

        RefreshList(SearchBox.Text);

        // Persist view mode
        App.Config.Setting_NotebookViewMode = _isGridView ? "grid" : "list";
        App.Config.Save();
    }

    private static ItemsPanelTemplate CreateItemsPanelTemplate(bool isGrid)
    {
        var template = new ItemsPanelTemplate();
        var factory = new FrameworkElementFactory(isGrid ? typeof(WrapPanel) : typeof(StackPanel));
        if (!isGrid)
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        template.VisualTree = factory;
        return template;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshList(SearchBox.Text);
    }

    private void NotebookList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenSelected();
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        OpenSelected();
    }

    private void MenuProperties_Click(object sender, RoutedEventArgs e)
    {
        var nb = GetSelectedNotebook();
        if (nb == null) return;

        var meta = _metaManager.GetMetadata(nb.Id);
        var dialog = new NotebookCoverDialog(meta);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            // Update notebook color/name
            nb.Name = dialog.Result.Title;
            nb.Color = dialog.Result.Color;
            nb.ModifiedAt = DateTime.UtcNow;
            _metaManager.UpdateMetadata(nb.Id, m =>
            {
                m.Title = dialog.Result.Title;
                m.Description = dialog.Result.Description;
                m.Color = dialog.Result.Color;
                m.Template = dialog.Result.Template;
                if (!string.IsNullOrWhiteSpace(dialog.Author))
                    m.Author = dialog.Author;
            });
            _noteManager.SaveNotebook(nb);
            RefreshList(SearchBox.Text);
        }
    }

    private void MenuFavorite_Click(object sender, RoutedEventArgs e)
    {
        var nb = GetSelectedNotebook();
        if (nb == null) return;
        nb.IsFavorite = !nb.IsFavorite;
        nb.ModifiedAt = DateTime.UtcNow;
        _noteManager.SaveNotebook(nb);
        RefreshList(SearchBox.Text);
    }

    private void MenuDelete_Click(object sender, RoutedEventArgs e)
    {
        var nb = GetSelectedNotebook();
        if (nb == null) return;
        var result = MessageBox.Show(
            $"Notizbuch \"{nb.Name}\" wirklich löschen?\nDiese Aktion kann nicht rückgängig gemacht werden.",
            "Löschen bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _noteManager.DeleteNotebook(nb.Id);
            RefreshList(SearchBox.Text);
        }
    }

    private Notebook? GetSelectedNotebook()
    {
        if (NotebookList.SelectedItem is ListBoxItem item && item.Tag is Notebook nb)
            return nb;
        if (NotebookList.SelectedItem is ListBoxItem li)
        {
            // Try to find the Notebook from the visual tree
            return FindNotebookFromItem(li);
        }
        return null;
    }

    private Notebook? FindNotebookFromItem(ListBoxItem item)
    {
        // Walk visual tree to find Border with Tag
        if (item.Content is Border border && border.Tag is Notebook nb)
            return nb;
        return null;
    }

    private void OpenSelected()
    {
        var nb = GetSelectedNotebook();
        if (nb != null)
        {
            NotebookOpened?.Invoke(nb);
            DialogResult = true;
            Close();
        }
    }
}