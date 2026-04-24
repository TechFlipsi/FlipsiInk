// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlipsiInk;

/// <summary>
/// Page Overview panel: scrollable thumbnail grid with page flags/markers.
/// v0.4.0 feature.
/// </summary>
public partial class PageOverviewControl : UserControl, INotifyPropertyChanged
{
    // ─── Flag color palette ──────────────────────────────────────────
    public static readonly string[] FlagColors = { "#FF0000", "#FF9500", "#FFCC00", "#34C759", "#007AFF", "#AF52DE" };
    public static readonly string[] FlagColorNames = { "Rot", "Orange", "Gelb", "Gruen", "Blau", "Lila" };

    // ─── Events ──────────────────────────────────────────────────────
    /// <summary>Fired when user clicks a thumbnail to navigate to that page.</summary>
    public event Action<int>? NavigateToPage;

    /// <summary>Fired when user wants to flag/unflag a page.</summary>
    public event Action<int, string?>? FlagPageRequested;

    /// <summary>Fired when user closes the panel.</summary>
    public event Action? CloseRequested;

    // ─── Data ────────────────────────────────────────────────────────
    public ObservableCollection<PageThumbItem> Thumbnails { get; } = new();

    private bool _showFlaggedOnly;

    public bool ShowFlaggedOnly
    {
        get => _showFlaggedOnly;
        set { _showFlaggedOnly = value; OnPropertyChanged(); }
    }

    private int _currentPageIndex;

    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set { _currentPageIndex = value; OnPropertyChanged(); HighlightCurrentPage(); }
    }

    private int _totalPages = 1;
    public int TotalPages
    {
        get => _totalPages;
        set { _totalPages = value; OnPropertyChanged(); UpdatePageCountLabel(); }
    }

    public PageOverviewControl()
    {
        InitializeComponent();
        ThumbGrid.ItemsSource = Thumbnails;
    }

    // ─── Public Methods ──────────────────────────────────────────────

    /// <summary>
    /// Refreshes the thumbnail grid from the given notebook.
    /// </summary>
    public void Refresh(Notebook? notebook, int currentPageIndex0)
    {
        Thumbnails.Clear();
        if (notebook == null) return;

        CurrentPageIndex = currentPageIndex0;
        TotalPages = notebook.Pages.Count;

        foreach (var page in notebook.Pages)
        {
            var item = new PageThumbItem
            {
                PageNumber = page.PageNumber,
                IsFlagged = page.IsFlagged,
                FlagColor = page.FlagColor ?? string.Empty,
                Label = $"S. {page.PageNumber}",
                Thumbnail = page.Thumbnail // may be null, XAML handles gracefully
            };
            item.UpdateFlagVisibility();
            Thumbnails.Add(item);
        }

        // Update header
        Dispatcher.BeginInvoke(() =>
        {
            PageCountLabel.Text = $"({TotalPages})";
            HeaderLabel.Text = Localization.Get("page_overview_title", "Seiten\u00fcbersicht");
            FilterAll.Content = Localization.Get("filter_all", "Alle");
            FilterFlagged.Content = Localization.Get("filter_flagged", "Markiert");
            BtnMarkCurrent.Content = Localization.Get("mark_page", "Seite markieren");
        });

        ApplyFilter();
    }

    /// <summary>
    /// Updates a single page's flag state in the grid.
    /// </summary>
    public void UpdatePageFlag(int pageNumber, bool isFlagged, string flagColor)
    {
        foreach (var thumb in Thumbnails)
        {
            if (thumb.PageNumber == pageNumber)
            {
                thumb.IsFlagged = isFlagged;
                thumb.FlagColor = flagColor;
                thumb.UpdateFlagVisibility();
                break;
            }
        }
        ApplyFilter();
    }

    // ─── UI Handlers ─────────────────────────────────────────────────

    private void ThumbCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is int pageNum)
        {
            NavigateToPage?.Invoke(pageNum);
        }
    }

    private void ThumbCard_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is int pageNum)
        {
            e.Handled = true;
            ShowFlagContextMenu(fe, pageNum);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private void FilterAll_Click(object sender, RoutedEventArgs e)
    {
        ShowFlaggedOnly = false;
        FilterAll.IsChecked = true;
        FilterFlagged.IsChecked = false;
        ApplyFilter();
    }

    private void FilterFlagged_Click(object sender, RoutedEventArgs e)
    {
        ShowFlaggedOnly = true;
        FilterAll.IsChecked = false;
        FilterFlagged.IsChecked = true;
        ApplyFilter();
    }

    private void BtnMarkCurrent_Click(object sender, RoutedEventArgs e)
    {
        // Mark the current page
        int currentPage = CurrentPageIndex + 1; // 1-based
        var thumb = GetThumbItem(currentPage);
        if (thumb != null)
        {
            // Toggle: if flagged, unflag; if not, flag with default color
            if (thumb.IsFlagged)
            {
                FlagPageRequested?.Invoke(currentPage, null);
            }
            else
            {
                FlagPageRequested?.Invoke(currentPage, FlagColors[0]);
            }
        }
    }

    // ─── Context Menu for Flag Colors ────────────────────────────────

    private void ShowFlagContextMenu(FrameworkElement target, int pageNumber)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };

        // "Mark page" header
        var headerItem = new MenuItem
        {
            Header = Localization.Get("mark_page_with_color", "Seite markieren mit..."),
            IsEnabled = false
        };
        menu.Items.Add(headerItem);

        // Flag colors
        for (int i = 0; i < FlagColors.Length; i++)
        {
            var color = FlagColors[i];
            var name = FlagColorNames[i];
            var item = new MenuItem
            {
                Header = $"  {name}",
                Tag = color
            };
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            item.Icon = new Border { Width = 12, Height = 12, Background = brush, CornerRadius = new CornerRadius(2) };
            item.Click += (s, e) => FlagPageRequested?.Invoke(pageNumber, color);
            menu.Items.Add(item);
        }

        // Separator
        menu.Items.Add(new Separator());

        // Remove flag
        var removeItem = new MenuItem
        {
            Header = Localization.Get("remove_flag", "Markierung entfernen")
        };
        removeItem.Click += (s, e) => FlagPageRequested?.Invoke(pageNumber, null);
        menu.Items.Add(removeItem);

        menu.PlacementTarget = target;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    // ─── Filtering ────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        if (ShowFlaggedOnly)
        {
            // Show only flagged pages
            foreach (var thumb in Thumbnails)
            {
                // We can't easily hide items in ItemsControl,
                // so we rebuild the view. For simplicity, we'll use a CollectionView.
            }
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Thumbnails);
            view.Filter = obj => obj is PageThumbItem t && t.IsFlagged;
        }
        else
        {
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Thumbnails);
            view.Filter = null;
        }
    }

    private void HighlightCurrentPage()
    {
        foreach (var thumb in Thumbnails)
        {
            thumb.IsCurrent = (thumb.PageNumber == CurrentPageIndex + 1);
        }
    }

    private PageThumbItem? GetThumbItem(int pageNumber)
    {
        foreach (var thumb in Thumbnails)
        {
            if (thumb.PageNumber == pageNumber) return thumb;
        }
        return null;
    }

    private void UpdatePageCountLabel()
    {
        PageCountLabel.Text = $"({TotalPages})";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// View model for a single thumbnail in the page overview grid.
/// </summary>
public class PageThumbItem : INotifyPropertyChanged
{
    public int PageNumber { get; set; }
    public string Label { get; set; } = "";
    public string? Thumbnail { get; set; }

    private bool _isFlagged;
    public bool IsFlagged
    {
        get => _isFlagged;
        set { _isFlagged = value; OnPropertyChanged(nameof(IsFlagged)); }
    }

    private string _flagColor = string.Empty;
    public string FlagColor
    {
        get => _flagColor;
        set { _flagColor = value; OnPropertyChanged(nameof(FlagColor)); UpdateFlagVisibility(); }
    }

    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            _isCurrent = value;
            OnPropertyChanged(nameof(IsCurrent));
            OnPropertyChanged(nameof(CurrentBorderBrush));
        }
    }

    public Brush CurrentBorderBrush => IsCurrent
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D7"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A"));

    private Visibility _flagVisibility = Visibility.Collapsed;
    public Visibility FlagVisibility
    {
        get => _flagVisibility;
        set { _flagVisibility = value; OnPropertyChanged(nameof(FlagVisibility)); }
    }

    public Brush FlagBrush
    {
        get
        {
            if (string.IsNullOrEmpty(FlagColor)) return Brushes.Transparent;
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(FlagColor)); }
            catch { return Brushes.Transparent; }
        }
    }

    public void UpdateFlagVisibility()
    {
        FlagVisibility = IsFlagged ? Visibility.Visible : Visibility.Collapsed;
        OnPropertyChanged(nameof(FlagBrush));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}