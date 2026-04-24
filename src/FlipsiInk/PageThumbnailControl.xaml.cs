// FlipsiInk - Page Thumbnail Strip Control
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.

#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlipsiInk;

/// <summary>
/// View model for a single page thumbnail entry.
/// </summary>
public class PageThumbnailItem
{
    public int PageIndex { get; set; } // 0-based index
    public string Label { get; set; } = "";
    public BitmapSource? Thumbnail { get; set; }
}

/// <summary>
/// Vertical thumbnail strip showing all pages in the notebook.
/// Supports click-to-navigate, add/delete, and drag-and-drop reordering.
/// </summary>
public partial class PageThumbnailControl : UserControl
{
    /// <summary>Bindable collection of thumbnail items.</summary>
    public ObservableCollection<PageThumbnailItem> Items { get; } = new();

    /// <summary>Fired when user clicks a thumbnail to navigate to a page.</summary>
    public event Action<int>? NavigateToPage;

    /// <summary>Fired when user clicks the add-page button.</summary>
    public event Action? AddPageRequested;

    /// <summary>Fired when user clicks the delete-page button.</summary>
    public event Action<int>? DeletePageRequested;

    /// <summary>Fired when user drag-drops to reorder pages.</summary>
    public event Action<int, int>? ReorderPages;

    private int _currentPageIndex; // 0-based

    // Drag-and-drop state
    private int _dragSourceIndex = -1;
    private bool _isDragging;

    public PageThumbnailControl()
    {
        InitializeComponent();
        ThumbnailList.ItemsSource = Items;

        BtnAddPage.Click += (s, e) => AddPageRequested?.Invoke();
        BtnDeletePage.Click += (s, e) =>
        {
            if (_currentPageIndex >= 0 && _currentPageIndex < Items.Count)
                DeletePageRequested?.Invoke(_currentPageIndex);
        };
    }

    /// <summary>
    /// Refreshes the entire thumbnail list from the page manager.
    /// </summary>
    public void RefreshThumbnails(PageManager pageManager)
    {
        Items.Clear();
        var pages = pageManager.GetAllPages();
        for (int i = 0; i < pages.Count; i++)
        {
            Items.Add(new PageThumbnailItem
            {
                PageIndex = i,
                Label = $"Seite {i + 1}"
            });
        }
        _currentPageIndex = pageManager.CurrentPageNumber - 1; // Convert 1-based to 0-based
        HighlightCurrentPage();
    }

    /// <summary>
    /// Updates the thumbnail image for a specific page.
    /// </summary>
    public void UpdateThumbnail(int pageIndex, BitmapSource thumbnail)
    {
        if (pageIndex >= 0 && pageIndex < Items.Count)
        {
            Items[pageIndex].Thumbnail = thumbnail;
            // Force refresh by removing and re-adding
            var item = Items[pageIndex];
            Items[pageIndex] = item;
        }
    }

    /// <summary>
    /// Sets the current page highlight.
    /// </summary>
    public void SetCurrentPage(int pageIndex0Based)
    {
        _currentPageIndex = pageIndex0Based;
        HighlightCurrentPage();
    }

    private void HighlightCurrentPage()
    {
        // Update border highlight for the current page
        for (int i = 0; i < ThumbnailList.Items.Count; i++)
        {
            var container = ThumbnailList.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
            if (container == null) continue;
            var border = FindVisualChild<Border>(container);
            if (border != null)
            {
                border.BorderBrush = (i == _currentPageIndex)
                    ? new SolidColorBrush(Color.FromRgb(0, 120, 215)) // #0078D7
                    : new SolidColorBrush(Color.FromRgb(68, 68, 68));  // #444
                border.BorderThickness = (i == _currentPageIndex)
                    ? new Thickness(2.5)
                    : new Thickness(1.5);
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    // ─── Click handling ─────────────────────────────────────────────────

    private void Thumbnail_Click(object sender, MouseButtonEventArgs e)
    {
        var border = sender as Border;
        if (border == null) return;
        var stackPanel = border.Child as StackPanel;
        if (stackPanel == null) return;
        var label = FindVisualChild<TextBlock>(stackPanel);
        if (label == null) return;

        // Find the item index
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Label == label.Text)
            {
                NavigateToPage?.Invoke(i);
                break;
            }
        }
    }

    // ─── Drag-and-drop reordering ──────────────────────────────────────

    private Border? _dragBorder;

    public void Thumbnail_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
        {
            _isDragging = true;
            _dragBorder = sender as Border;
            if (_dragBorder != null)
            {
                DragDrop.DoDragDrop(_dragBorder, _dragBorder, DragDropEffects.Move);
            }
            _isDragging = false;
        }
    }

    private void Thumbnail_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void Thumbnail_Drop(object sender, DragEventArgs e)
    {
        var targetBorder = sender as Border;
        if (targetBorder == null) return;

        // Find source and target indices
        int sourceIndex = FindBorderIndex(_dragBorder);
        int targetIndex = FindBorderIndex(targetBorder);

        if (sourceIndex >= 0 && targetIndex >= 0 && sourceIndex != targetIndex)
        {
            ReorderPages?.Invoke(sourceIndex, targetIndex);
        }

        _dragSourceIndex = -1;
        _dragBorder = null;
    }

    private int FindBorderIndex(Border? border)
    {
        if (border == null) return -1;
        for (int i = 0; i < ThumbnailList.Items.Count; i++)
        {
            var container = ThumbnailList.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
            if (container == null) continue;
            var child = FindVisualChild<Border>(container);
            if (child == border) return i;
        }
        return -1;
    }
}