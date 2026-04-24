// BacklinksPanel.xaml.cs - Panel showing backlinks, outgoing links, and unlinked mentions (Issue #33)
// Copyright (C) 2026 Fabian Kirchweger / TechFlipsi
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

#nullable enable

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace FlipsiInk;

/// <summary>
/// Displays backlinks (incoming links), outgoing links, and unlinked mentions
/// for the current notebook.
/// </summary>
public partial class BacklinksPanel : Border
{
    private LinkManager? _linkManager;
    private string _currentNotebookName = string.Empty;
    private Guid _currentNotebookId;

    /// <summary>Raised when user clicks a link to navigate.</summary>
    public event EventHandler<LinkNavigationEventArgs>? LinkNavigationRequested;

    public BacklinksPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the LinkManager reference and updates the panel.
    /// </summary>
    public void SetLinkManager(LinkManager linkManager)
    {
        _linkManager = linkManager;
        _linkManager.LinksChanged += (s, e) => Refresh();
    }

    /// <summary>
    /// Sets the current notebook context and refreshes all displays.
    /// </summary>
    public void SetCurrentNotebook(Guid notebookId, string notebookName)
    {
        _currentNotebookId = notebookId;
        _currentNotebookName = notebookName;
        Refresh();
    }

    /// <summary>
    /// Refreshes all sections: backlinks, outgoing links, and unlinked mentions.
    /// </summary>
    public void Refresh()
    {
        RefreshBacklinks();
        RefreshOutgoingLinks();
    }

    /// <summary>
    /// Updates the unlinked mentions section based on the given text content.
    /// Called when sticky note or OCR text changes.
    /// </summary>
    public void UpdateUnlinkedMentions(string? content, string currentNotebookName)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(currentNotebookName))
        {
            UnlinkedMentionsList.ItemsSource = null;
            NoUnlinkedText.Visibility = Visibility.Visible;
            return;
        }

        // Find other notebook names mentioned in this text that are NOT [[linked]]
        var mentions = LinkManager.FindUnlinkedMentions(content, currentNotebookName);
        if (mentions.Count == 0)
        {
            UnlinkedMentionsList.ItemsSource = null;
            NoUnlinkedText.Visibility = Visibility.Visible;
            return;
        }

        NoUnlinkedText.Visibility = Visibility.Collapsed;
        UnlinkedMentionsList.ItemsSource = mentions.ConvertAll(m => $"„{m.Text}“ an Position {m.Position}");
    }

    // ─── Private helpers ────────────────────────────────────────────

    private void RefreshBacklinks()
    {
        if (_linkManager == null || string.IsNullOrEmpty(_currentNotebookName))
        {
            BacklinksList.ItemsSource = null;
            NoBacklinksText.Visibility = Visibility.Visible;
            return;
        }

        var backlinks = _linkManager.GetBacklinks(_currentNotebookName);
        if (backlinks.Count == 0)
        {
            BacklinksList.ItemsSource = null;
            NoBacklinksText.Visibility = Visibility.Visible;
            return;
        }

        NoBacklinksText.Visibility = Visibility.Collapsed;
        var items = new List<BacklinkDisplayItem>();
        foreach (var bl in backlinks)
        {
            items.Add(new BacklinkDisplayItem
            {
                DisplayName = $"📌 {bl.TargetNotebookName} (S. {bl.SourcePage})",
                TargetNotebookName = bl.TargetNotebookName
            });
        }
        BacklinksList.ItemsSource = items;
    }

    private void RefreshOutgoingLinks()
    {
        if (_linkManager == null)
        {
            OutgoingLinksList.ItemsSource = null;
            NoOutgoingText.Visibility = Visibility.Visible;
            return;
        }

        var outgoing = _linkManager.GetOutgoingLinks(_currentNotebookId);
        if (outgoing.Count == 0)
        {
            OutgoingLinksList.ItemsSource = null;
            NoOutgoingText.Visibility = Visibility.Visible;
            return;
        }

        NoOutgoingText.Visibility = Visibility.Collapsed;
        OutgoingLinksList.ItemsSource = outgoing;
    }

    private void Backlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string targetName)
        {
            LinkNavigationRequested?.Invoke(this, new LinkNavigationEventArgs { TargetName = targetName });
        }
    }

    private void OutgoingLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink hl && hl.Tag is string targetName)
        {
            LinkNavigationRequested?.Invoke(this, new LinkNavigationEventArgs { TargetName = targetName });
        }
    }
}

/// <summary>
/// Display item for backlinks list.
/// </summary>
internal class BacklinkDisplayItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string TargetNotebookName { get; set; } = string.Empty;
}