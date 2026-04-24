// FlipsiInk - AI-powered Handwriting & Math Notes App
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

namespace FlipsiInk;

/// <summary>
/// Bookmark panel displayed as a collapsible sidebar.
/// Shows all bookmarks for the current notebook and allows jumping to them.
/// </summary>
public partial class BookmarkPanel : UserControl
{
    private readonly BookmarkManager _bookmarkManager;
    private Guid _currentNotebookId;
    public ObservableCollection<Bookmark> Bookmarks { get; } = new();

    public event Action<int>? NavigateToBookmark;
    public event Action<Guid, int>? BookmarkAdded;
    public event Action<Guid, int>? BookmarkRemoved;

    public BookmarkPanel(BookmarkManager bookmarkManager)
    {
        InitializeComponent();
        _bookmarkManager = bookmarkManager;

        BtnAddBookmark.Click += OnAddBookmark;
        BookmarkList.ItemsSource = Bookmarks;
    }

    /// <summary>
    /// Sets the current notebook context and refreshes the bookmark list.
    /// </summary>
    public void SetNotebook(Guid notebookId, int currentPageNumber)
    {
        _currentNotebookId = notebookId;
        RefreshBookmarks();
    }

    /// <summary>
    /// Refreshes the bookmark list from BookmarkManager.
    /// </summary>
    public void RefreshBookmarks()
    {
        Bookmarks.Clear();
        foreach (var b in _bookmarkManager.GetBookmarksForNotebook(_currentNotebookId))
            Bookmarks.Add(b);
    }

    private void OnAddBookmark(object sender, RoutedEventArgs e)
    {
        BookmarkAdded?.Invoke(_currentNotebookId, 0);
    }

    private void RemoveBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid bookmarkId)
        {
            var bookmark = _bookmarkManager.GetBookmarksForNotebook(_currentNotebookId)
                .Find(b => b.Id == bookmarkId);
            if (bookmark != null)
            {
                BookmarkRemoved?.Invoke(_currentNotebookId, bookmark.PageNumber);
                _bookmarkManager.RemoveBookmark(_currentNotebookId, bookmark.PageNumber);
                RefreshBookmarks();
            }
        }
    }

    private void Bookmark_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (BookmarkList.SelectedItem is Bookmark b)
        {
            NavigateToBookmark?.Invoke(b.PageNumber);
        }
    }
}