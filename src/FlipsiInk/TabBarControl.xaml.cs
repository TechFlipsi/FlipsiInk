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
using System.Windows.Input;
using System.Windows.Media;

namespace FlipsiInk;

/// <summary>
/// Tab bar control that displays open notebook tabs at the top of the window.
/// Supports drag-and-drop reorder, pin/unpin, close, and active tab highlighting.
/// </summary>
public partial class TabBarControl : UserControl
{
    private readonly TabManager _tabManager;
    private readonly Dictionary<string, Border> _tabElements = new();
    private Func<Guid, string>? _nameResolver;

    // Drag-and-drop state
    private Border? _draggedTab;
    private bool _isDragging;
    private Point _dragStart;
    private int _dragSourceIndex;

    public event Action<Guid>? TabActivated;
    public event Action<Guid>? TabClosed;
    public event Action<Guid>? TabPinned;
    public event Action<Guid>? TabUnpinned;

    public TabBarControl(TabManager tabManager)
    {
        InitializeComponent();
        _tabManager = tabManager;
        _tabManager.TabsChanged += RefreshTabs;
    }

    /// <summary>
    /// Refreshes the tab strip from TabManager state.
    /// </summary>
    public void RefreshTabs()
    {
        TabStrip.Children.Clear();
        _tabElements.Clear();

        // Sort: pinned tabs first, then by OpenedAt
        var tabs = new List<TabItem>(_tabManager.OpenTabs);
        tabs.Sort((a, b) =>
        {
            if (a.IsPinned && !b.IsPinned) return -1;
            if (!a.IsPinned && b.IsPinned) return 1;
            return a.OpenedAt.CompareTo(b.OpenedAt);
        });

        foreach (var tab in tabs)
        {
            var border = CreateTabElement(tab);
            _tabElements[tab.NotebookId.ToString()] = border;
            TabStrip.Children.Add(border);
        }

        UpdateActiveTab();
    }

    /// <summary>
    /// Creates a single tab UI element for the given TabItem.
    /// </summary>
    private Border CreateTabElement(TabItem tab)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)),
            CornerRadius = new CornerRadius(4, 4, 0, 0),
            Margin = new Thickness(1, 2, 0, 0),
            Padding = new Thickness(4, 2, 2, 2),
            Tag = tab.NotebookId,
            AllowDrop = true,
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal };

        // Pin indicator
        var pinIcon = new TextBlock
        {
            Text = tab.IsPinned ? "📌" : "",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 2, 0),
            Tag = "PinIcon"
        };
        stack.Children.Add(pinIcon);

        // Tab label
        var label = new TextBlock
        {
            Text = tab.IsPinned ? GetNotebookName(tab.NotebookId) : GetNotebookName(tab.NotebookId),
            Foreground = Brushes.White,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 4, 0),
            MaxWidth = 120,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Tag = "TabLabel"
        };
        stack.Children.Add(label);

        // Close button
        var closeBtn = new Button
        {
            Content = "×",
            FontSize = 11,
            Width = 18,
            Height = 18,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Tag = tab.NotebookId
        };
        closeBtn.Click += (s, e) =>
        {
            e.Handled = true;
            TabClosed?.Invoke(tab.NotebookId);
        };
        closeBtn.MouseEnter += (s, e) => closeBtn.Foreground = Brushes.Red;
        closeBtn.MouseLeave += (s, e) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
        stack.Children.Add(closeBtn);

        border.Child = stack;

        // Click to activate
        border.MouseLeftButtonUp += (s, e) =>
        {
            if (!_isDragging)
                TabActivated?.Invoke(tab.NotebookId);
        };

        // Right-click context menu
        var menu = new ContextMenu();

        var pinItem = new MenuItem
        {
            Header = tab.IsPinned ? "Tab lösen" : "Tab anheften",
            Tag = tab.NotebookId
        };
        pinItem.Click += (s, e) =>
        {
            if (tab.IsPinned) TabUnpinned?.Invoke(tab.NotebookId);
            else TabPinned?.Invoke(tab.NotebookId);
        };
        menu.Items.Add(pinItem);

        var closeItem = new MenuItem
        {
            Header = "Tab schließen",
            Tag = tab.NotebookId
        };
        closeItem.Click += (s, e) => TabClosed?.Invoke(tab.NotebookId);
        menu.Items.Add(closeItem);

        border.ContextMenu = menu;

        // Drag-and-drop: Mouse down starts potential drag
        border.MouseLeftButtonDown += (s, e) =>
        {
            _draggedTab = border;
            _dragStart = e.GetPosition(this);
            _dragSourceIndex = TabStrip.Children.IndexOf(border);
            _isDragging = false;
        };

        border.MouseMove += (s, e) =>
        {
            if (_draggedTab == border && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                if (!_isDragging && (Math.Abs(pos.X - _dragStart.X) > 5 || Math.Abs(pos.Y - _dragStart.Y) > 5))
                    _isDragging = true;

                if (_isDragging)
                {
                    // Determine target position based on mouse X
                    int targetIndex = 0;
                    for (int i = 0; i < TabStrip.Children.Count; i++)
                    {
                        var child = (FrameworkElement)TabStrip.Children[i];
                        var childPos = child.TransformToAncestor(this).Transform(new Point(0, 0));
                        if (pos.X > childPos.X + child.ActualWidth / 2)
                            targetIndex = i + 1;
                    }

                    if (targetIndex != _dragSourceIndex && targetIndex != _dragSourceIndex + 1)
                    {
                        // Reorder in TabManager
                        var movedTab = _tabManager.OpenTabs[_dragSourceIndex];
                        _tabManager.OpenTabs.RemoveAt(_dragSourceIndex);
                        var insertAt = targetIndex > _dragSourceIndex ? targetIndex - 1 : targetIndex;
                        _tabManager.OpenTabs.Insert(insertAt, movedTab);
                        _dragSourceIndex = insertAt;
                        RefreshTabs();
                    }
                }
            }
        };

        border.MouseLeftButtonUp += (s, e) =>
        {
            _draggedTab = null;
            _isDragging = false;
        };

        // Drop target
        border.AllowDrop = true;

        return border;
    }

    /// <summary>
    /// Updates the visual highlight of the active tab.
    /// </summary>
    public void UpdateActiveTab()
    {
        foreach (var kvp in _tabElements)
        {
            var isActive = kvp.Key == _tabManager.ActiveTabId?.ToString();
            kvp.Value.Background = isActive
                ? new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D))
                : new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
            kvp.Value.BorderBrush = isActive
                ? new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7))
                : Brushes.Transparent;
            kvp.Value.BorderThickness = isActive ? new Thickness(0, 0, 0, 2) : new Thickness(0);
        }
    }

    public void SetNameResolver(Func<Guid, string> resolver) => _nameResolver = resolver;

    /// <summary>
    /// Gets a notebook name by ID. Returns fallback if not found.
    /// </summary>
    private string GetNotebookName(Guid notebookId)
    {
        return _nameResolver?.Invoke(notebookId) ?? "Notizbuch";
    }

    /// <summary>
    /// Switches to the next tab (Ctrl+Tab).
    /// </summary>
    public void SwitchToNextTab()
    {
        var tabs = _tabManager.OpenTabs;
        if (tabs.Count <= 1) return;
        var currentIndex = tabs.FindIndex(t => t.NotebookId == _tabManager.ActiveTabId);
        var nextIndex = (currentIndex + 1) % tabs.Count;
        TabActivated?.Invoke(tabs[nextIndex].NotebookId);
    }

    /// <summary>
    /// Switches to the previous tab (Ctrl+Shift+Tab).
    /// </summary>
    public void SwitchToPreviousTab()
    {
        var tabs = _tabManager.OpenTabs;
        if (tabs.Count <= 1) return;
        var currentIndex = tabs.FindIndex(t => t.NotebookId == _tabManager.ActiveTabId);
        var prevIndex = (currentIndex - 1 + tabs.Count) % tabs.Count;
        TabActivated?.Invoke(tabs[prevIndex].NotebookId);
    }
}