// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Ink;

namespace FlipsiInk;

/// <summary>
/// Verwaltet Touch/Stift-Eingabe-Modi für Palm Rejection.
/// Stift-Modus: Stift schreibt, Touch scrollt nur.
/// Touch-Modus: Touch schreibt.
/// Beide-Modus: Alles zeichnet.
/// </summary>
public enum InputMode
{
    /// <summary>Stift schreibt, Touch scrollt nur (Palm Rejection).</summary>
    Pen,
    /// <summary>Touch schreibt (für Nutzer ohne Stift).</summary>
    Touch,
    /// <summary>Stift und Touch zeichnen beide.</summary>
    Both
}

public class InputModeManager
{
    private InputMode _currentMode = InputMode.Pen;
    private InkCanvas _canvas;
    private ScrollViewer? _scrollViewer;

    /// <summary>
    /// Aktueller Eingabe-Modus.
    /// </summary>
    public InputMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode == value) return;
            _currentMode = value;
            ApplyMode();
            ModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Wird ausgelöst wenn der Eingabe-Modus gewechselt wird.
    /// </summary>
    public event EventHandler? ModeChanged;

    /// <summary>
    /// Erkennt automatisch ob ein Stift verfügbar ist.
    /// </summary>
    public bool StylusAvailable { get; private set; }

    public InputModeManager(InkCanvas canvas, ScrollViewer? scrollViewer = null)
    {
        _canvas = canvas;
        _scrollViewer = scrollViewer;
        
        // Stift-Erkennung
        StylusAvailable = Stylus.CurrentStylusDevice != null 
                          || Tablet.TabletDevices.Cast<TabletDevice>()
                              .Any(td => td.Type == TabletDeviceType.Stylus);
        
        // Standard: Pen-Modus wenn Stift verfügbar, sonst Touch
        _currentMode = StylusAvailable ? InputMode.Pen : InputMode.Touch;

        // Events registrieren
        _canvas.StylusDown += OnStylusDown;
        _canvas.StylusUp += OnStylusUp;
        _canvas.TouchDown += OnTouchDown;
        _canvas.PreviewTouchDown += OnPreviewTouchDown;
        _canvas.PreviewMouseDown += OnPreviewMouseDown;

        // Stylus-Events für automatische Erkennung (StateChanged not available in WPF)
        // Stylus detection is handled via StylusDown/Up events instead

        ApplyMode();
    }

    /// <summary>
    /// Wechselt zum nächsten Modus: Pen → Touch → Both → Pen.
    /// </summary>
    public void CycleMode()
    {
        CurrentMode = _currentMode switch
        {
            InputMode.Pen => InputMode.Touch,
            InputMode.Touch => InputMode.Both,
            _ => InputMode.Pen
        };
    }

    /// <summary>
    /// Gibt ein Emoji für den aktuellen Modus zurück (für UI-Anzeige).
    /// </summary>
    public string GetModeEmoji() => _currentMode switch
    {
        InputMode.Pen => "🖊️",
        InputMode.Touch => "👆",
        InputMode.Both => "✋",
        _ => "🖊️"
    };

    /// <summary>
    /// Gibt eine Beschreibung des aktuellen Modus zurück.
    /// </summary>
    public string GetModeDescription() => _currentMode switch
    {
        InputMode.Pen => "Stift-Modus: Stift schreibt, Touch scrollt",
        InputMode.Touch => "Touch-Modus: Touch schreibt",
        InputMode.Both => "Beide-Modus: Stift und Touch zeichnen",
        _ => "Unbekannt"
    };

    private void ApplyMode()
    {
        switch (_currentMode)
        {
            case InputMode.Pen:
                // Stift zeichnet, Touch scrollt
                _canvas.IsManipulationEnabled = false;
                // Touch-Eingabe wird blockiert (nur Scroll)
                if (_scrollViewer != null)
                {
                    _scrollViewer.PanningMode = PanningMode.Both;
                    _scrollViewer.IsManipulationEnabled = true;
                }
                break;

            case InputMode.Touch:
                // Touch zeichnet
                _canvas.IsManipulationEnabled = false;
                if (_scrollViewer != null)
                {
                    _scrollViewer.PanningMode = PanningMode.None;
                    _scrollViewer.IsManipulationEnabled = false;
                }
                break;

            case InputMode.Both:
                // Beide zeichnen
                _canvas.IsManipulationEnabled = false;
                if (_scrollViewer != null)
                {
                    _scrollViewer.PanningMode = PanningMode.None;
                    _scrollViewer.IsManipulationEnabled = false;
                }
                break;
        }
    }

    private void OnStylusDown(object sender, StylusDownEventArgs e)
    {
        // Stift erkannt – StiftAvailable aktualisieren
        if (!StylusAvailable)
        {
            StylusAvailable = true;
            StylusAvailableChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnStylusUp(object sender, StylusEventArgs e)
    {
        // Stift abgehoben
    }

    private void OnPreviewTouchDown(object sender, TouchEventArgs e)
    {
        if (_currentMode == InputMode.Pen)
        {
            // Im Stift-Modus: Touch soll NICHT zeichnen, nur scrollen
            // Markiere Touch als behandelt, damit InkCanvas es ignoriert
            // ScrollViewer übernimmt das Scrollen
            e.Handled = true;
        }
    }

    private void OnTouchDown(object sender, TouchEventArgs e)
    {
        if (_currentMode == InputMode.Pen)
        {
            // Im Stift-Modus: Touch-Eingabe auf Canvas blockieren
            e.Handled = true;
        }
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Prüfe ob die Eingabe vom Stift oder Touch/Maus kommt
        if (_currentMode == InputMode.Pen && e.StylusDevice?.DeviceType == TabletDeviceType.Touch)
        {
            e.Handled = true;
        }
    }

    private void OnStylusStateChanged(object? sender, EventArgs e)
    {
        // Automatische Modus-Erkennung wenn Stift in Reichweite kommt
        if (Stylus.CurrentStylusDevice != null && !StylusAvailable)
        {
            StylusAvailable = true;
            StylusAvailableChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Wird ausgelöst wenn Stift-Verfügbarkeit sich ändert.
    /// </summary>
    public event EventHandler? StylusAvailableChanged;

    /// <summary>
    /// Gibt alle verfügbaren Tablet-Geräte zurück (für Diagnose).
    /// </summary>
    public string GetDeviceInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"Modus: {_currentMode}");
        info.AppendLine($"Stift verfügbar: {StylusAvailable}");
        info.AppendLine($"Tablet-Geräte: {Tablet.TabletDevices.Count}");
        foreach (TabletDevice td in Tablet.TabletDevices)
        {
            info.AppendLine($"  - {td.Name} ({td.Type})");
        }
        return info.ToString();
    }
}