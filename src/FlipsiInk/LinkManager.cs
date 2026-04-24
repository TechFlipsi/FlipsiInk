// LinkManager.cs - Bi-directional [[link]] system (Issue #33)
// Copyright (C) 2026 Fabian Kirchweger / TechFlipsi
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FlipsiInk;

/// <summary>
/// Manages bi-directional [[wiki-links]] between notebooks.
/// Links are stored in .flipsiink metadata and parsed from text content.
/// </summary>
public partial class LinkManager
{
    // Regex to match [[link]] patterns in text
    private static readonly Regex LinkPattern = GetLinkRegex();

    /// <summary>Event raised when a link navigation is requested.</summary>
    public event EventHandler<LinkNavigationEventArgs>? LinkNavigationRequested;

    /// <summary>Event raised when links change (added/removed).</summary>
    public event EventHandler? LinksChanged;

    /// <summary>All known link entries (persisted).</summary>
    private readonly List<LinkEntry> _links = [];

    /// <summary>Reference to NoteManager for notebook name resolution.</summary>
    private NoteManager? _noteManager;

    // ─── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Sets the NoteManager reference for notebook lookups.
    /// </summary>
    public void SetNoteManager(NoteManager noteManager)
    {
        _noteManager = noteManager;
    }

    /// <summary>
    /// Parses [[link]] patterns from text and returns the link target names.
    /// </summary>
    public static List<string> ParseLinks(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var matches = LinkPattern.Matches(text);
        return matches.Select(m => m.Groups[1].Value.Trim()).Distinct().ToList();
    }

    /// <summary>
    /// Returns all notebook names available for autocomplete.
    /// </summary>
    public List<string> GetAutocompleteNames(string? filter = null)
    {
        if (_noteManager == null) return [];
        var index = typeof(NoteManager);
        // Use SearchNotebooks with empty string to get all, then filter
        var all = _noteManager.SearchNotebooks(filter ?? string.Empty);
        return all.Select(n => n.Name).OrderBy(n => n).ToList();
    }

    /// <summary>
    /// Requests navigation to the notebook with the given name.
    /// </summary>
    public void NavigateToLink(string notebookName)
    {
        LinkNavigationRequested?.Invoke(this, new LinkNavigationEventArgs { TargetName = notebookName });
    }

    /// <summary>
    /// Records that a source notebook/page contains a link to a target notebook.
    /// Called when text content changes and [[links]] are detected.
    /// </summary>
    public void UpdateLinksFromContent(Guid sourceNotebookId, int sourcePage, string? content)
    {
        var parsed = ParseLinks(content);
        // Remove old links from this source
        _links.RemoveAll(l => l.SourceNotebookId == sourceNotebookId && l.SourcePage == sourcePage);
        // Add new links
        foreach (var target in parsed)
        {
            _links.Add(new LinkEntry
            {
                SourceNotebookId = sourceNotebookId,
                SourcePage = sourcePage,
                TargetNotebookName = target
            });
        }
        LinksChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Returns all backlinks: notebooks that link TO the given notebook name.
    /// </summary>
    public List<LinkEntry> GetBacklinks(string notebookName)
    {
        return _links
            .Where(l => string.Equals(l.TargetNotebookName, notebookName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Returns all outgoing links FROM the given notebook.
    /// </summary>
    public List<LinkEntry> GetOutgoingLinks(Guid sourceNotebookId)
    {
        return _links.Where(l => l.SourceNotebookId == sourceNotebookId).ToList();
    }

    /// <summary>
    /// Finds unlinked mentions of a notebook name in text content.
    /// Returns positions where the name appears but is NOT wrapped in [[ ]].
    /// </summary>
    public static List<UnlinkedMention> FindUnlinkedMentions(string? text, string notebookName)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(notebookName)) return [];

        var mentions = new List<UnlinkedMention>();
        var linkedNames = ParseLinks(text).Select(n => n.ToLowerInvariant()).ToHashSet();

        // Find all occurrences of the notebook name in text
        var idx = 0;
        while (idx < text.Length)
        {
            var pos = text.IndexOf(notebookName, idx, StringComparison.OrdinalIgnoreCase);
            if (pos < 0) break;

            // Check if this position is already inside a [[ ]] link
            if (!IsInsideLink(text, pos))
            {
                mentions.Add(new UnlinkedMention
                {
                    Position = pos,
                    Text = text.Substring(pos, notebookName.Length)
                });
            }
            idx = pos + 1;
        }

        return mentions;
    }

    /// <summary>
    /// Checks if a position in text is inside a [[ ]] link.
    /// </summary>
    private static bool IsInsideLink(string text, int position)
    {
        // Look backwards for [[ that isn't closed by ]] before position
        var before = text[..position];
        var lastOpen = before.LastIndexOf("[[", StringComparison.Ordinal);
        if (lastOpen < 0) return false;
        var lastClose = before.LastIndexOf("]]", StringComparison.Ordinal);
        return lastOpen > lastClose || lastClose < 0;
    }

    // ─── Persistence ─────────────────────────────────────────────────

    /// <summary>
    /// Gets serializable link data for .flipsiink format.
    /// </summary>
    public List<LinkEntryData> GetAllLinkData()
    {
        return _links.Select(l => new LinkEntryData
        {
            SourceNotebookId = l.SourceNotebookId,
            SourcePage = l.SourcePage,
            TargetNotebookName = l.TargetNotebookName
        }).ToList();
    }

    /// <summary>
    /// Restores links from serialized data.
    /// </summary>
    public void LoadFromData(List<LinkEntryData>? data)
    {
        _links.Clear();
        if (data == null) return;
        foreach (var d in data)
        {
            _links.Add(new LinkEntry
            {
                SourceNotebookId = d.SourceNotebookId,
                SourcePage = d.SourcePage,
                TargetNotebookName = d.TargetNotebookName
            });
        }
    }

    // ─── Source-generated regex for performance ───────────────────────

    [GeneratedRegex(@"\[\[(.+?)\]\]", RegexOptions.Compiled)]
    private static partial Regex GetLinkRegex();
}

// ─── Data classes ───────────────────────────────────────────────────

/// <summary>
/// A single link from one notebook/page to another notebook by name.
/// </summary>
public class LinkEntry
{
    public Guid SourceNotebookId { get; set; }
    public int SourcePage { get; set; }
    public string TargetNotebookName { get; set; } = string.Empty;
}

/// <summary>
/// Serializable link entry for .flipsiink format.
/// </summary>
public class LinkEntryData
{
    public Guid SourceNotebookId { get; set; }
    public int SourcePage { get; set; }
    public string TargetNotebookName { get; set; } = string.Empty;
}

/// <summary>
/// Event args for link navigation requests.
/// </summary>
public class LinkNavigationEventArgs : EventArgs
{
    public string TargetName { get; set; } = string.Empty;
}

/// <summary>
/// Represents an unlinked mention of a notebook name in text.
/// </summary>
public class UnlinkedMention
{
    public int Position { get; set; }
    public string Text { get; set; } = string.Empty;
}