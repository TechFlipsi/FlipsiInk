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
using System.Windows.Media;

namespace FlipsiInk;

/// <summary>
/// Manages zoom and pan for an InkCanvas host element.
/// Supports Ctrl+MouseWheel zoom, pinch-to-zoom, and programmatic zoom.
/// </summary>
public class ZoomManager
{
    private double _zoomLevel = 1.0;
    private const double MinZoom = 0.25;
    private const double MaxZoom = 5.0;
    private const double ZoomStep = 0.25;

    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            var clamped = ClampZoom(value);
            if (Math.Abs(_zoomLevel - clamped) < 0.001) return;
            _zoomLevel = clamped;
            ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(_zoomLevel));
        }
    }

    public double ZoomPercentage => _zoomLevel * 100.0;

    public void ZoomIn() => ZoomLevel += ZoomStep;
    public void ZoomOut() => ZoomLevel -= ZoomStep;
    public void ResetZoom() => ZoomLevel = 1.0;

    /// <summary>
    /// Calculates a zoom level that fits the content within the given viewport size.
    /// </summary>
    public void FitToPage(Size viewportSize, Size contentSize)
    {
        if (contentSize.Width <= 0 || contentSize.Height <= 0) return;
        var scaleX = viewportSize.Width / contentSize.Width;
        var scaleY = viewportSize.Height / contentSize.Height;
        ZoomLevel = Math.Min(scaleX, scaleY);
    }

    /// <summary>
    /// Applies the current zoom level as a ScaleTransform on the target element.
    /// </summary>
    public void ApplyZoom(FrameworkElement target)
    {
        if (target.RenderTransform is ScaleTransform st)
        {
            st.ScaleX = _zoomLevel;
            st.ScaleY = _zoomLevel;
        }
        else
        {
            target.RenderTransform = new ScaleTransform(_zoomLevel, _zoomLevel);
        }
        target.RenderTransformOrigin = new Point(0.5, 0.5);
    }

    /// <summary>
    /// Clamps a zoom level to the allowed range [0.25, 5.0].
    /// </summary>
    public static double ClampZoom(double level) => Math.Max(MinZoom, Math.Min(MaxZoom, level));

    /// <summary>
    /// Attaches Ctrl+MouseWheel zoom handling to the given element.
    /// </summary>
    public void AttachMouseWheelZoom(FrameworkElement element)
    {
        element.PreviewMouseWheel += OnMouseWheel;
    }

    /// <summary>
    /// Detaches Ctrl+MouseWheel zoom handling.
    /// </summary>
    public void DetachMouseWheelZoom(FrameworkElement element)
    {
        element.PreviewMouseWheel -= OnMouseWheel;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        e.Handled = true;
        ZoomLevel += (e.Delta > 0 ? 1 : -1) * ZoomStep * 0.5;
    }

    /// <summary>
    /// Attaches pinch-to-zoom handling for touch input on the given element.
    /// Call this on a container that supports Manipulation events.
    /// </summary>
    public void AttachPinchZoom(FrameworkElement element)
    {
        element.IsManipulationEnabled = true;
        element.ManipulationStarting += OnManipulationStarting;
        element.ManipulationDelta += OnManipulationDelta;
    }

    public void DetachPinchZoom(FrameworkElement element)
    {
        element.IsManipulationEnabled = false;
        element.ManipulationStarting -= OnManipulationStarting;
        element.ManipulationDelta -= OnManipulationDelta;
    }

    private void OnManipulationStarting(object sender, ManipulationStartingEventArgs e)
    {
        e.ManipulationContainer = (FrameworkElement)sender;
        e.Handled = true;
    }

    private void OnManipulationDelta(object sender, ManipulationDeltaEventArgs e)
    {
        if (e.DeltaManipulation.Scale.Length > 0)
        {
            var scale = e.DeltaManipulation.Scale.X;
            ZoomLevel *= scale;
        }
        e.Handled = true;
    }
}

public class ZoomChangedEventArgs : EventArgs
{
    public double NewZoomLevel { get; }
    public double NewZoomPercentage => NewZoomLevel * 100.0;
    public ZoomChangedEventArgs(double zoomLevel) => NewZoomLevel = zoomLevel;
}