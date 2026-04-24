// StickyNoteManager.cs - Manages sticky notes: add, remove, persist
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
using System.Windows.Controls;

namespace FlipsiInk;

/// <summary>
/// Manages the lifecycle and persistence of sticky notes on the canvas overlay.
/// </summary>
public class StickyNoteManager
{
    private readonly Canvas _overlay;
    private readonly List<StickyNoteControl> _notes = [];

    /// <summary>Whether sticky note placement mode is active.</summary>
    public bool IsStickyNoteMode { get; private set; }

    /// <summary>Fired when a note is added or removed (for save triggers).</summary>
    public event EventHandler? NotesChanged;

    public StickyNoteManager(Canvas overlay)
    {
        _overlay = overlay;
    }

    /// <summary>
    /// Toggles sticky note placement mode.
    /// </summary>
    public void ToggleStickyNoteMode()
    {
        IsStickyNoteMode = !IsStickyNoteMode;
    }

    /// <summary>
    /// Adds a new sticky note at the given position.
    /// </summary>
    public StickyNoteControl AddNote(double x, double y, StickyNoteColor color = StickyNoteColor.Gelb,
        string? text = null, Guid? existingId = null)
    {
        var note = new StickyNoteControl();
        note.SetColor(color);
        if (text != null)
            note.NoteTextContent = text;

        note.Width = 180;
        note.Height = 150;
        Canvas.SetLeft(note, x);
        Canvas.SetTop(note, y);

        // Wire events
        note.DeleteRequested += (s, e) => RemoveNote(note);
        note.Changed += (s, e) => NotesChanged?.Invoke(this, EventArgs.Empty);

        _overlay.Children.Add(note);
        _notes.Add(note);

        NotesChanged?.Invoke(this, EventArgs.Empty);
        return note;
    }

    /// <summary>
    /// Removes a sticky note from the overlay.
    /// </summary>
    public void RemoveNote(StickyNoteControl note)
    {
        _overlay.Children.Remove(note);
        _notes.Remove(note);
        NotesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes all sticky notes.
    /// </summary>
    public void ClearAll()
    {
        foreach (var note in _notes.ToList())
        {
            _overlay.Children.Remove(note);
        }
        _notes.Clear();
        NotesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets serializable data for all current sticky notes.
    /// </summary>
    public List<StickyNoteData> GetAllData()
    {
        return _notes.Select(n => n.ToData()).ToList();
    }

    /// <summary>
    /// Restores sticky notes from serialized data.
    /// </summary>
    public void LoadFromData(List<StickyNoteData>? data)
    {
        ClearAll();
        if (data == null) return;

        foreach (var d in data)
        {
            var note = AddNote(d.X, d.Y,
                Enum.TryParse<StickyNoteColor>(d.Color, out var c) ? c : StickyNoteColor.Gelb,
                d.Text, d.Id);
            note.FromData(d);
        }
    }
}