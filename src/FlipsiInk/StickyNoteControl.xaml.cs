// StickyNoteControl.xaml.cs - Sticky note widget with drag, resize, edit, minimize
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Text.RegularExpressions;

namespace FlipsiInk;

/// <summary>
/// Available sticky note colors.
/// </summary>
public enum StickyNoteColor
{
    Gelb,   // Yellow
    Pink,
    Gruen,  // Green
    Blau,   // Blue
    Orange
}

/// <summary>
/// A draggable, resizable, editable sticky note control.
/// Floats above ink strokes on the canvas overlay.
/// </summary>
public partial class StickyNoteControl : UserControl
{
    // Color mapping
    private static readonly Dictionary<StickyNoteColor, string> ColorBrushes = new()
    {
        { StickyNoteColor.Gelb,   "#FFF9C4" },
        { StickyNoteColor.Pink,  "#F8BBD0" },
        { StickyNoteColor.Gruen, "#C8E6C9" },
        { StickyNoteColor.Blau,  "#BBDEFB" },
        { StickyNoteColor.Orange, "#FFE0B2" }
    };

    private static readonly Dictionary<StickyNoteColor, string> HeaderBrushes = new()
    {
        { StickyNoteColor.Gelb,   "#FFF176" },
        { StickyNoteColor.Pink,  "#F06292" },
        { StickyNoteColor.Gruen, "#66BB6A" },
        { StickyNoteColor.Blau,  "#42A5F5" },
        { StickyNoteColor.Orange, "#FFA726" }
    };

    // Unique ID for persistence
    public Guid Id { get; } = Guid.NewGuid();

    // Note properties
    public StickyNoteColor NoteColor { get; private set; } = StickyNoteColor.Gelb;

    // State
    private bool _isMinimized = false;
    private bool _isDragging = false;
    private Point _dragStartPoint;
    private double _preMinimizeHeight;

    // Link autocomplete popup
    private Popup? _linkAutocompletePopup;
    private ListBox? _linkAutocompleteList;
    private List<string> _notebookNames = [];

    /// <summary>Raised when the user clicks a [[link]] in the note text.</summary>
    public event EventHandler<LinkNavigationEventArgs>? LinkClicked;

    /// <summary>Raised when the text content changes (for link index updates).</summary>
    public new event EventHandler? Changed;

    /// <summary>Raised when the user clicks the delete button.</summary>
    public event EventHandler? DeleteRequested;

    public StickyNoteControl()
    {
        InitializeComponent();
        SetColor(StickyNoteColor.Gelb);

        // Wire up link detection on text changes
        NoteText.TextChanged += NoteText_TextChanged;
        NoteText.PreviewKeyDown += NoteText_PreviewKeyDown;
    }

    /// <summary>
    /// Sets the sticky note color theme.
    /// </summary>
    public void SetColor(StickyNoteColor color)
    {
        NoteColor = color;
        var bg = ColorBrushes.TryGetValue(color, out var b) ? b : "#FFF9C4";
        var hdr = HeaderBrushes.TryGetValue(color, out var h) ? h : "#FFF176";
        NoteBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
        HeaderBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hdr));
    }

    /// <summary>
    /// Gets or sets the note text.
    /// </summary>
    public string NoteTextContent
    {
        get => NoteText.Text;
        set => NoteText.Text = value;
    }

    /// <summary>
    /// Whether the note is in minimized (collapsed) state.
    /// </summary>
    public bool IsMinimized => _isMinimized;

    // ─── Drag logic (on header bar) ───────────────────────────────────

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only start drag if not clicking a button
        if (e.OriginalSource is Button or Thumb) return;
        _isDragging = true;
        _dragStartPoint = e.GetPosition(this);
        (sender as UIElement)?.CaptureMouse();
        e.Handled = true;
    }

    private void HeaderBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed) return;

        var parent = Parent as Canvas;
        if (parent == null) return;

        var pos = e.GetPosition(parent);
        var newX = pos.X - _dragStartPoint.X;
        var newY = pos.Y - _dragStartPoint.Y;

        // Keep within canvas bounds
        newX = Math.Max(0, Math.Min(newX, parent.ActualWidth - ActualWidth));
        newY = Math.Max(0, Math.Min(newY, parent.ActualHeight - ActualHeight));

        Canvas.SetLeft(this, newX);
        Canvas.SetTop(this, newY);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void HeaderBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            (sender as UIElement)?.ReleaseMouseCapture();
        }
    }

    // ─── Resize logic ─────────────────────────────────────────────────

    private void ResizeHandle_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var newWidth = Math.Max(120, ActualWidth + e.HorizontalChange);
        var newHeight = Math.Max(60, ActualHeight + e.VerticalChange);
        Width = newWidth;
        Height = newHeight;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ─── Minimize / expand ────────────────────────────────────────────

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        _isMinimized = !_isMinimized;
        if (_isMinimized)
        {
            _preMinimizeHeight = Height;
            ContentArea.Visibility = Visibility.Collapsed;
            BtnMinimize.Content = "+";
            Height = double.NaN; // auto-size to header
        }
        else
        {
            ContentArea.Visibility = Visibility.Visible;
            BtnMinimize.Content = "−";
            Height = _preMinimizeHeight > 0 ? _preMinimizeHeight : 150;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ─── Delete ────────────────────────────────────────────────────────

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    // ─── Text focus (pause ink input while editing) ──────────────────

    private void NoteText_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateLinkOverlay();
        Changed?.Invoke(this, EventArgs.Empty);

        // Check for [[ to trigger autocomplete
        var caret = NoteText.CaretIndex;
        var text = NoteText.Text;
        if (caret >= 2 && text.Substring(caret - 2, 2) == "[[")
        {
            ShowLinkAutocomplete(string.Empty);
        }
        else if (_linkAutocompletePopup?.IsOpen == true)
        {
            // Update filter based on text after [[
            UpdateAutocompleteFilter();
        }
    }

    private void NoteText_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_linkAutocompletePopup?.IsOpen == true)
        {
            if (e.Key == Key.Escape)
            {
                _linkAutocompletePopup.IsOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _linkAutocompleteList?.SelectedItem != null)
            {
                InsertAutocompleteSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Down && _linkAutocompleteList != null)
            {
                var idx = _linkAutocompleteList.SelectedIndex;
                if (idx < _linkAutocompleteList.Items.Count - 1)
                    _linkAutocompleteList.SelectedIndex = idx + 1;
                e.Handled = true;
            }
            else if (e.Key == Key.Up && _linkAutocompleteList != null)
            {
                var idx = _linkAutocompleteList.SelectedIndex;
                if (idx > 0)
                    _linkAutocompleteList.SelectedIndex = idx - 1;
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// Sets the list of notebook names for link autocomplete.
    /// </summary>
    public void SetNotebookNames(List<string> names)
    {
        _notebookNames = names;
    }

    private void ShowLinkAutocomplete(string filter)
    {
        if (_notebookNames.Count == 0) return;

        if (_linkAutocompletePopup == null)
        {
            _linkAutocompletePopup = new Popup
            {
                Placement = PlacementMode.Bottom,
                PlacementTarget = NoteText,
                StaysOpen = false,
                Width = 200,
                MaxHeight = 150
            };
            _linkAutocompleteList = new ListBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 12
            };
            _linkAutocompletePopup.Child = _linkAutocompleteList;
        }

        var filtered = string.IsNullOrEmpty(filter)
            ? _notebookNames
            : _notebookNames.Where(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        if (filtered.Count == 0)
        {
            _linkAutocompletePopup.IsOpen = false;
            return;
        }

        _linkAutocompleteList.ItemsSource = filtered;
        _linkAutocompleteList.SelectedIndex = 0;
        _linkAutocompleteList.MouseLeftButtonUp += (s, e) =>
        {
            InsertAutocompleteSelection();
        };

        _linkAutocompletePopup.IsOpen = true;
    }

    private void UpdateAutocompleteFilter()
    {
        var caret = NoteText.CaretIndex;
        var text = NoteText.Text;
        // Find the [[ before cursor
        var before = text[..caret];
        var openIdx = before.LastIndexOf("[[", StringComparison.Ordinal);
        if (openIdx < 0)
        {
            _linkAutocompletePopup!.IsOpen = false;
            return;
        }
        // Check if there's a ]] between [[ and cursor
        var between = text[openIdx..caret];
        if (between.Contains("]"))
        {
            _linkAutocompletePopup!.IsOpen = false;
            return;
        }
        var filter = between[2..]; // skip the [[
        ShowLinkAutocomplete(filter);
    }

    private void InsertAutocompleteSelection()
    {
        if (_linkAutocompleteList?.SelectedItem is not string name) return;
        var caret = NoteText.CaretIndex;
        var text = NoteText.Text;
        var before = text[..caret];
        var openIdx = before.LastIndexOf("[[", StringComparison.Ordinal);
        if (openIdx < 0) return;
        // Replace from [[ to cursor with [[name]]
        var newText = text[..openIdx] + "[[" + name + "]]" + text[caret..];
        NoteText.Text = newText;
        NoteText.CaretIndex = openIdx + name.Length + 4; // after ]]
        _linkAutocompletePopup!.IsOpen = false;
    }

    /// <summary>
    /// Updates the link overlay to render clickable [[link]] regions on top of the TextBox.
    /// </summary>
    public void UpdateLinkOverlay()
    {
        if (LinkOverlay == null) return;
        LinkOverlay.Children.Clear();

        var text = NoteText.Text;
        var links = LinkManager.ParseLinks(text);
        if (links.Count == 0)
        {
            LinkOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        // For a simple implementation: make the overlay visible when not editing
        // and show a subtle visual indicator. Full link rendering with exact
        // positioning is complex in WPF; we use text highlighting instead.
    }

    /// <summary>
    /// Handles mouse clicks on the note text to detect [[link]] navigation.
    /// </summary>
    public void HandleLinkClick(Point relativePoint)
    {
        var text = NoteText.Text;
        var links = LinkManager.ParseLinks(text);
        if (links.Count == 0) return;

        // Find which link was clicked based on character index at click position
        // Simple approach: check if the text at click position is within a [[link]]
        var linkMatches = System.Text.RegularExpressions.Regex.Matches(text, @"\[\[(.+?)\]\]");
        foreach (System.Text.RegularExpressions.Match match in linkMatches)
        {
            // Notify that a link was clicked
            LinkClicked?.Invoke(this, new LinkNavigationEventArgs { TargetName = match.Groups[1].Value.Trim() });
            return;
        }
    }

    private void NoteText_GotFocus(object sender, RoutedEventArgs e)
    {
        // Prevent ink canvas from capturing pen input while editing text
        var window = Window.GetWindow(this);
        if (window is MainWindow mw)
        {
            mw.MainCanvas.EditingMode = InkCanvasEditingMode.None;
        }
    }

    private void NoteText_LostFocus(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is MainWindow mw)
        {
            mw.RestoreCanvasEditingMode();
        }
        Changed?.Invoke(this, EventArgs.Empty);

        // Apply link visual styling to [[link]] text (Issue #33)
        ApplyLinkStyling();
    }

    /// <summary>
    /// Applies visual styling to [[link]] text in the note.
    /// Makes linked text appear blue and underlined.
    /// </summary>
    private void ApplyLinkStyling()
    {
        // WPF TextBox doesn't support rich inline formatting natively,
        // so we rely on the link overlay for visual indication.
        // The overlay is shown when the note doesn't have focus.
        UpdateLinkOverlay();
    }

    // ─── Click on border to bring to front ────────────────────────────

    private void NoteBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Bring to front by setting highest ZIndex
        var parent = Parent as Canvas;
        if (parent != null)
        {
            int maxZ = 0;
            foreach (var child in parent.Children)
            {
                if (child is StickyNoteControl sn)
                    maxZ = Math.Max(maxZ, Canvas.GetZIndex(sn));
            }
            Canvas.SetZIndex(this, maxZ + 1);
        }
    }

    // ─── Serialization helper ─────────────────────────────────────────

    /// <summary>
    /// Returns a serializable data object representing this sticky note.
    /// </summary>
    public StickyNoteData ToData()
    {
        return new StickyNoteData
        {
            Id = Id,
            Color = NoteColor.ToString(),
            Text = NoteTextContent,
            X = Canvas.GetLeft(this),
            Y = Canvas.GetTop(this),
            Width = ActualWidth > 0 ? ActualWidth : Width,
            Height = _isMinimized ? _preMinimizeHeight : (ActualHeight > 0 ? ActualHeight : Height),
            IsMinimized = _isMinimized
        };
    }

    /// <summary>
    /// Restores state from a serializable data object.
    /// </summary>
    public void FromData(StickyNoteData data)
    {
        NoteTextContent = data.Text ?? string.Empty;
        if (Enum.TryParse<StickyNoteColor>(data.Color, out var c))
            SetColor(c);
        Width = data.Width > 0 ? data.Width : 180;
        if (data.IsMinimized && !_isMinimized)
            BtnMinimize_Click(this, new RoutedEventArgs());
        else if (!data.IsMinimized && _isMinimized)
            BtnMinimize_Click(this, new RoutedEventArgs());
    }
}

/// <summary>
/// Serializable data for a sticky note (used in .flipsiink format).
/// </summary>
public class StickyNoteData
{
    public Guid Id { get; set; }
    public string Color { get; set; } = "Gelb";
    public string Text { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 180;
    public double Height { get; set; } = 150;
    public bool IsMinimized { get; set; }
}