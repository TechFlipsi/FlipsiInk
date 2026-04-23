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
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlipsiInk;

/// <summary>
/// Datenklasse für ein Lesezeichen auf einer bestimmten Seite eines Notizbuchs.
/// </summary>
public class Bookmark
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotebookId { get; set; }
    public int PageNumber { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Datenklasse für einen geöffneten Tab im Notizbuch-Bereich.
/// </summary>
public class TabItem
{
    public Guid NotebookId { get; set; }
    public bool IsPinned { get; set; }
    public bool IsModified { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Verwaltet Lesezeichen für Notizbücher. Persistiert als JSON-Datei
/// in %AppData%/FlipsiInk/bookmarks.json.
/// </summary>
public class BookmarkManager
{
    private readonly Dictionary<(Guid NotebookId, int Page), Bookmark> _bookmarks = new();
    private readonly HashSet<Guid> _favorites = new();
    private readonly string _dataPath;

    public BookmarkManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "FlipsiInk");
        Directory.CreateDirectory(dir);
        _dataPath = Path.Combine(dir, "bookmarks.json");
        Load();
    }

    /// <summary>
    /// Setzt ein Lesezeichen auf der angegebenen Seite.
    /// </summary>
    public void AddBookmark(Guid notebookId, int pageNumber)
    {
        var key = (notebookId, pageNumber);
        if (!_bookmarks.ContainsKey(key))
        {
            _bookmarks[key] = new Bookmark
            {
                NotebookId = notebookId,
                PageNumber = pageNumber,
                Label = $"S. {pageNumber}"
            };
            Save();
        }
    }

    /// <summary>
    /// Entfernt ein Lesezeichen.
    /// </summary>
    public void RemoveBookmark(Guid notebookId, int pageNumber)
    {
        if (_bookmarks.Remove((notebookId, pageNumber)))
            Save();
    }

    /// <summary>
    /// Prüft ob eine Seite als Lesezeichen markiert ist.
    /// </summary>
    public bool IsBookmarked(Guid notebookId, int pageNumber)
    {
        return _bookmarks.ContainsKey((notebookId, pageNumber));
    }

    /// <summary>
    /// Gibt alle Lesezeichen zurück.
    /// </summary>
    public List<Bookmark> GetAllBookmarks()
    {
        return _bookmarks.Values.OrderBy(b => b.NotebookId).ThenBy(b => b.PageNumber).ToList();
    }

    /// <summary>
    /// Gibt alle Lesezeichen für ein bestimmtes Notizbuch zurück.
    /// </summary>
    public List<Bookmark> GetBookmarksForNotebook(Guid notebookId)
    {
        return _bookmarks.Values
            .Where(b => b.NotebookId == notebookId)
            .OrderBy(b => b.PageNumber)
            .ToList();
    }

    #region Favoriten

    /// <summary>
    /// Fügt ein Notizbuch zu den Favoriten hinzu.
    /// </summary>
    public void AddFavorite(Guid notebookId)
    {
        if (_favorites.Add(notebookId))
            Save();
    }

    /// <summary>
    /// Entfernt ein Notizbuch aus den Favoriten.
    /// </summary>
    public void RemoveFavorite(Guid notebookId)
    {
        if (_favorites.Remove(notebookId))
            Save();
    }

    /// <summary>
    /// Prüft ob ein Notizbuch ein Favorit ist.
    /// </summary>
    public bool IsFavorite(Guid notebookId) => _favorites.Contains(notebookId);

    /// <summary>
    /// Gibt alle Favoriten zurück.
    /// </summary>
    public List<Guid> GetFavorites() => _favorites.ToList();

    #endregion

    #region Persistenz

    private void Save()
    {
        var data = new BookmarkData
        {
            Bookmarks = _bookmarks.Values.ToList(),
            Favorites = _favorites.ToList()
        };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_dataPath, json);
    }

    private void Load()
    {
        if (!File.Exists(_dataPath)) return;
        try
        {
            var json = File.ReadAllText(_dataPath);
            var data = JsonSerializer.Deserialize<BookmarkData>(json);
            if (data == null) return;

            _bookmarks.Clear();
            foreach (var b in data.Bookmarks)
                _bookmarks[(b.NotebookId, b.PageNumber)] = b;

            _favorites.Clear();
            foreach (var f in data.Favorites)
                _favorites.Add(f);
        }
        catch
        {
            // Silently ignore corrupted data
        }
    }

    /// <summary>
    /// Interne Datenstruktur für JSON-Serialisierung.
    /// </summary>
    private class BookmarkData
    {
        public List<Bookmark> Bookmarks { get; set; } = new();
        public List<Guid> Favorites { get; set; } = new();
    }

    #endregion
}

/// <summary>
/// Verwaltet die Liste der zuletzt geöffneten Notizbücher (max. 10).
/// </summary>
public class RecentNotebooksManager
{
    private readonly List<Guid> _recent = new();
    private readonly int _maxRecent;
    private readonly string _dataPath;

    public RecentNotebooksManager(int maxRecent = 10)
    {
        _maxRecent = maxRecent;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "FlipsiInk");
        Directory.CreateDirectory(dir);
        _dataPath = Path.Combine(dir, "recent.json");
        Load();
    }

    /// <summary>
    /// Fügt ein Notizbuch zu "Zuletzt geöffnet" hinzu. Wenn bereits vorhanden,
    /// wird es an den Anfang verschoben. Max. _maxRecent Einträge.
    /// </summary>
    public void AddRecent(Guid notebookId)
    {
        _recent.Remove(notebookId);
        _recent.Insert(0, notebookId);
        while (_recent.Count > _maxRecent)
            _recent.RemoveAt(_recent.Count - 1);
        Save();
    }

    /// <summary>
    /// Gibt die letzten count geöffneten Notizbücher zurück.
    /// </summary>
    public List<Guid> GetRecent(int count = 10) => _recent.Take(count).ToList();

    /// <summary>
    /// Leert die Liste der zuletzt geöffneten Notizbücher.
    /// </summary>
    public void ClearRecent()
    {
        _recent.Clear();
        Save();
    }

    /// <summary>
    /// Entfernt ein bestimmtes Notizbuch aus der Liste.
    /// </summary>
    public void RemoveRecent(Guid notebookId)
    {
        if (_recent.Remove(notebookId))
            Save();
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_recent, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_dataPath, json);
    }

    private void Load()
    {
        if (!File.Exists(_dataPath)) return;
        try
        {
            var json = File.ReadAllText(_dataPath);
            var loaded = JsonSerializer.Deserialize<List<Guid>>(json);
            if (loaded != null)
                _recent.AddRange(loaded.Take(_maxRecent));
        }
        catch { /* ignoriere korrupte Daten */ }
    }
}

/// <summary>
/// Verwaltet offene Tabs (Notizbuch-Reiter) in der Benutzeroberfläche.
/// </summary>
public class TabManager
{
    private readonly List<TabItem> _openTabs = new();

    /// <summary>
    /// Liste der aktuell offenen Tabs.
    /// </summary>
    public List<TabItem> OpenTabs => _openTabs;

    /// <summary>
    /// ID des aktuell aktiven Tabs.
    /// </summary>
    public Guid? ActiveTabId { get; private set; }

    /// <summary>
    /// Event das ausgelöst wird, wenn sich die Tab-Liste ändert.
    /// </summary>
    public event Action? TabsChanged;

    /// <summary>
    /// Öffnet einen neuen Tab für das angegebene Notizbuch.
    /// Wenn bereits offen, wird er aktiviert.
    /// </summary>
    public void OpenTab(Guid notebookId)
    {
        var existing = _openTabs.FirstOrDefault(t => t.NotebookId == notebookId);
        if (existing != null)
        {
            ActiveTabId = notebookId;
            TabsChanged?.Invoke();
            return;
        }

        _openTabs.Add(new TabItem { NotebookId = notebookId });
        ActiveTabId = notebookId;
        TabsChanged?.Invoke();
    }

    /// <summary>
    /// Schließt den Tab für das angegebene Notizbuch.
    /// Gepinnte Tabs können nur geschlossen werden, wenn force true ist.
    /// </summary>
    public void CloseTab(Guid notebookId, bool force = false)
    {
        var tab = _openTabs.FirstOrDefault(t => t.NotebookId == notebookId);
        if (tab == null) return;
        if (tab.IsPinned && !force) return;

        _openTabs.Remove(tab);

        // Wenn der geschlossene Tab aktiv war, den letzten Tab aktivieren
        if (ActiveTabId == notebookId)
        {
            ActiveTabId = _openTabs.LastOrDefault()?.NotebookId;
        }

        TabsChanged?.Invoke();
    }

    /// <summary>
    /// Pinnt einen Tab an (kann nicht versehentlich geschlossen werden).
    /// </summary>
    public void PinTab(Guid notebookId)
    {
        var tab = _openTabs.FirstOrDefault(t => t.NotebookId == notebookId);
        if (tab != null)
        {
            tab.IsPinned = true;
            TabsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Entpinnt einen Tab.
    /// </summary>
    public void UnpinTab(Guid notebookId)
    {
        var tab = _openTabs.FirstOrDefault(t => t.NotebookId == notebookId);
        if (tab != null)
        {
            tab.IsPinned = false;
            TabsChanged?.Invoke();
        }
    }
}