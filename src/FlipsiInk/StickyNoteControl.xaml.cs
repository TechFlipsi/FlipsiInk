// StickyNoteControl.xaml.cs - Sticky note widget with drag, resize, edit, minimize
// Copyright (C) 2026 Fabian Kirchweger / TechFlipsi
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

#nullable enable

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

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

    /// <summary>Raised when the user clicks the delete button.</summary>
    public event EventHandler? DeleteRequested;

    /// <summary>Raised when position, size, text, or state changes.</summary>
    public event EventHandler? Changed;

    public StickyNoteControl()
    {
        InitializeComponent();
        SetColor(StickyNoteColor.Gelb);
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

    private void NoteText_GotFocus(object sender, RoutedEventArgs e)
    {
        // Prevent ink canvas from capturing pen input while editing text
        var window = Window.GetParent(this) as Window;
        if (window is MainWindow mw)
        {
            mw.MainCanvas.EditingMode = InkCanvasEditingMode.None;
        }
    }

    private void NoteText_LostFocus(object sender, RoutedEventArgs e)
    {
        var window = Window.GetParent(this) as Window;
        if (window is MainWindow mw)
        {
            mw.RestoreCanvasEditingMode();
        }
        Changed?.Invoke(this, EventArgs.Empty);
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