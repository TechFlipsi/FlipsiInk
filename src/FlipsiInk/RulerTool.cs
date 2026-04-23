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
using System.Windows.Media;

namespace FlipsiInk;

/// <summary>
/// Vordefinierte Winkel für das Lineal.
/// </summary>
public enum RulerAngle
{
    Horizontal = 0,
    Vertical = 90,
    Angle45 = 45,
    Angle135 = 135,
    Custom = -1
}

/// <summary>
/// Lineal-Werkzeug für den Canvas. Zeigt ein halbtransparentes Lineal
/// mit cm-Markierungen und Zahlen. Drehbar per Winkel-Property.
/// Unterstützt Snap-to-Ruler-Funktionalität.
/// </summary>
public class RulerTool
{
    // 1 cm ≈ 37.8 Pixel bei 96 DPI
    private const double CmInPixels = 37.7952755906;

    private bool _isVisible;
    private double _angle;
    private Point _position;
    private double _length;

    /// <summary>
    /// Lineal ein- oder ausschalten.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnChanged();
            }
        }
    }

    /// <summary>
    /// Winkel in Grad (0=horizontal, 90=vertikal, oder custom).
    /// </summary>
    public double Angle
    {
        get => _angle;
        set
        {
            if (Math.Abs(_angle - value) > 0.01)
            {
                _angle = value;
                OnChanged();
            }
        }
    }

    /// <summary>
    /// Position des Lineal-Ursprungs auf dem Canvas.
    /// </summary>
    public Point Position
    {
        get => _position;
        set
        {
            if (_position != value)
            {
                _position = value;
                OnChanged();
            }
        }
    }

    /// <summary>
    /// Länge des Lineals in Pixel.
    /// </summary>
    public double Length
    {
        get => _length;
        set
        {
            if (Math.Abs(_length - value) > 0.01)
            {
                _length = value;
                OnChanged();
            }
        }
    }

    /// <summary>
    /// Wird ausgelöst, wenn sich eine Eigenschaft ändert (für Re-Rendering).
    /// </summary>
    public event Action? Changed;

    public RulerTool()
    {
        _isVisible = false;
        _angle = 0;
        _position = new Point(0, 0);
        _length = 500;
    }

    /// <summary>
    /// Setzt den Winkel anhand des RulerAngle-Enums.
    /// </summary>
    public void SetAngle(RulerAngle rulerAngle)
    {
        Angle = rulerAngle switch
        {
            RulerAngle.Horizontal => 0,
            RulerAngle.Vertical => 90,
            RulerAngle.Angle45 => 45,
            RulerAngle.Angle135 => 135,
            RulerAngle.Custom => Angle, // bleibt unverändert
            _ => Angle
        };
    }

    /// <summary>
    /// Gibt die Geometry des Lineals als DrawingVisual zurück.
    /// </summary>
    /// <returns>DrawingVisual mit der Lineal-Geometrie.</returns>
    public DrawingVisual GetRulerGeometry()
    {
        return CreateRulerVisual(_length, _angle);
    }

    /// <summary>
    /// Gibt Snap-Punkte entlang des Lineals im angegebenen Abstand zurück.
    /// </summary>
    /// <param name="interval">Abstand zwischen Snap-Punkten in Pixel (Standard: 50).</param>
    /// <returns>Liste der Snap-Punkte auf dem Canvas.</returns>
    public List<Point> GetSnapPoints(double interval = 50)
    {
        var points = new List<Point>();
        var radians = _angle * Math.PI / 180.0;
        var dx = Math.Cos(radians);
        var dy = Math.Sin(radians);

        for (double d = 0; d <= _length; d += interval)
        {
            points.Add(new Point(
                _position.X + dx * d,
                _position.Y + dy * d));
        }

        return points;
    }

    /// <summary>
    /// Zeichnet das Lineal mit cm-Markierungen und Zahlen in den DrawingContext.
    /// </summary>
    /// <param name="dc">DrawingContext des aufrufenden Controls.</param>
    public void RenderRuler(DrawingContext dc)
    {
        if (!_isVisible) return;

        // Halbtransparenter Hintergrund
        var bgBrush = new SolidColorBrush(Color.FromArgb(160, 220, 230, 240));
        bgBrush.Freeze();

        var linePen = new Pen(new SolidColorBrush(Colors.DarkSlateGray), 1.0);
        linePen.Freeze();

        var markPen = new Pen(new SolidColorBrush(Colors.DarkSlateGray), 0.8);
        markPen.Freeze();

        var textBrush = new SolidColorBrush(Colors.DarkSlateGray);
        textBrush.Freeze();

        var typeface = new Typeface("Segoe UI");

        // Lineal-Körper berechnen
        var radians = _angle * Math.PI / 180.0;
        var dx = Math.Cos(radians);
        var dy = Math.Sin(radians);
        // Normale (senkrecht zum Lineal)
        var nx = -dy;
        var ny = dx;

        const double rulerHeight = CmInPixels * 2.5; // 2.5 cm hoch

        // Vier Ecken des Lineals
        var p0 = _position;
        var p1 = new Point(_position.X + dx * _length, _position.Y + dy * _length);
        var p2 = new Point(p1.X + nx * rulerHeight, p1.Y + ny * rulerHeight);
        var p3 = new Point(_position.X + nx * rulerHeight, _position.Y + ny * rulerHeight);

        // Hintergrund-Rechteck
        var bgGeom = new StreamGeometry();
        using (var ctx = bgGeom.Open())
        {
            ctx.BeginFigure(p0, true, true);
            ctx.LineTo(p1, true, false);
            ctx.LineTo(p2, true, false);
            ctx.LineTo(p3, true, false);
        }
        bgGeom.Freeze();
        dc.DrawGeometry(bgBrush, linePen, bgGeom);

        // Hauptlinie (Kante des Lineals)
        dc.DrawLine(linePen, p0, p1);

        // cm-Markierungen und Zahlen
        int cmCount = (int)Math.Floor(_length / CmInPixels);
        for (int cm = 0; cm <= cmCount; cm++)
        {
            double d = cm * CmInPixels;
            var basePoint = new Point(_position.X + dx * d, _position.Y + dy * d);

            // Jede cm-Marke: halbe Höhe
            var markEnd = new Point(basePoint.X + nx * rulerHeight * 0.4, basePoint.Y + ny * rulerHeight * 0.4);

            // Hauptmarkierung
            if (cm % 5 == 0)
            {
                // Lange Marke bei 5er-Schritten
                var longEnd = new Point(basePoint.X + nx * rulerHeight * 0.7, basePoint.Y + ny * rulerHeight * 0.7);
                dc.DrawLine(markPen, basePoint, longEnd);

                // Zahl
                var formatted = new FormattedText(
                    cm.ToString(),
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    9,
                    textBrush,
                    1.0);

                // Text an der langen Marke positionieren
                var textPos = new Point(
                    basePoint.X + nx * rulerHeight * 0.75 - formatted.Width / 2,
                    basePoint.Y + ny * rulerHeight * 0.75 - formatted.Height / 2);
                dc.DrawText(formatted, textPos);
            }
            else
            {
                dc.DrawLine(markPen, basePoint, markEnd);
            }

            // mm-Unterteilungen (10 pro cm)
            if (cm < cmCount)
            {
                for (int mm = 1; mm < 10; mm++)
                {
                    double mmDist = (cm + mm / 10.0) * CmInPixels;
                    var mmPoint = new Point(_position.X + dx * mmDist, _position.Y + dy * mmDist);
                    var mmEnd = new Point(
                        mmPoint.X + nx * rulerHeight * 0.15,
                        mmPoint.Y + ny * rulerHeight * 0.15);
                    dc.DrawLine(markPen, mmPoint, mmEnd);
                }
            }
        }
    }

    /// <summary>
    /// Snappt einen Punkt an die nächste Lineal-Kante.
    /// </summary>
    /// <param name="inputPoint">Eingabepunkt auf dem Canvas.</param>
    /// <returns>Der gesnappte Punkt (auf der Lineal-Kante).</returns>
    public Point SnapToRuler(Point inputPoint)
    {
        if (!_isVisible) return inputPoint;

        var radians = _angle * Math.PI / 180.0;
        var dx = Math.Cos(radians);
        var dy = Math.Sin(radians);

        // Projektion des Eingabepunkts auf die Lineal-Linie
        var vx = inputPoint.X - _position.X;
        var vy = inputPoint.Y - _position.Y;
        double proj = vx * dx + vy * dy;
        proj = Math.Max(0, Math.Min(_length, proj)); // Auf Lineal-Länge begrenzen

        // Gesnappter Punkt auf der Kante
        return new Point(
            _position.X + dx * proj,
            _position.Y + dy * proj);
    }

    /// <summary>
    /// Erstellt ein DrawingVisual mit der Lineal-Darstellung.
    /// Statische Methode für unabhängige Rendering-Zwecke.
    /// </summary>
    /// <param name="length">Länge des Lineals in Pixel.</param>
    /// <param name="angle">Winkel in Grad.</param>
    /// <param name="showMarks">cm-Markierungen anzeigen (Standard: true).</param>
    /// <returns>DrawingVisual mit Lineal.</returns>
    public static DrawingVisual CreateRulerVisual(double length, double angle, bool showMarks = true)
    {
        var visual = new DrawingVisual();
        var dc = visual.RenderOpen();

        var radians = angle * Math.PI / 180.0;
        var dx = Math.Cos(radians);
        var dy = Math.Sin(radians);
        var nx = -dy;
        var ny = dx;

        var origin = new Point(0, 0);
        const double rulerHeight = CmInPixels * 2.5;

        // Hintergrund
        var bgBrush = new SolidColorBrush(Color.FromArgb(160, 220, 230, 240));
        bgBrush.Freeze();
        var linePen = new Pen(new SolidColorBrush(Colors.DarkSlateGray), 1.0);
        linePen.Freeze();

        // Rechteck
        var p0 = origin;
        var p1 = new Point(dx * length, dy * length);
        var p2 = new Point(p1.X + nx * rulerHeight, p1.Y + ny * rulerHeight);
        var p3 = new Point(nx * rulerHeight, ny * rulerHeight);

        var bgGeom = new StreamGeometry();
        using (var ctx = bgGeom.Open())
        {
            ctx.BeginFigure(p0, true, true);
            ctx.LineTo(p1, true, false);
            ctx.LineTo(p2, true, false);
            ctx.LineTo(p3, true, false);
        }
        bgGeom.Freeze();
        dc.DrawGeometry(bgBrush, linePen, bgGeom);

        if (showMarks)
        {
            var markPen = new Pen(new SolidColorBrush(Colors.DarkSlateGray), 0.8);
            markPen.Freeze();
            var textBrush = new SolidColorBrush(Colors.DarkSlateGray);
            textBrush.Freeze();
            var typeface = new Typeface("Segoe UI");

            int cmCount = (int)Math.Floor(length / CmInPixels);
            for (int cm = 0; cm <= cmCount; cm++)
            {
                double d = cm * CmInPixels;
                var basePoint = new Point(dx * d, dy * d);

                if (cm % 5 == 0)
                {
                    var longEnd = new Point(basePoint.X + nx * rulerHeight * 0.7, basePoint.Y + ny * rulerHeight * 0.7);
                    dc.DrawLine(markPen, basePoint, longEnd);

                    var formatted = new FormattedText(
                        cm.ToString(),
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        9,
                        textBrush,
                        1.0);
                    var textPos = new Point(
                        basePoint.X + nx * rulerHeight * 0.75 - formatted.Width / 2,
                        basePoint.Y + ny * rulerHeight * 0.75 - formatted.Height / 2);
                    dc.DrawText(formatted, textPos);
                }
                else
                {
                    var markEnd = new Point(basePoint.X + nx * rulerHeight * 0.4, basePoint.Y + ny * rulerHeight * 0.4);
                    dc.DrawLine(markPen, basePoint, markEnd);
                }

                if (cm < cmCount)
                {
                    for (int mm = 1; mm < 10; mm++)
                    {
                        double mmDist = (cm + mm / 10.0) * CmInPixels;
                        var mmPoint = new Point(dx * mmDist, dy * mmDist);
                        var mmEnd = new Point(mmPoint.X + nx * rulerHeight * 0.15, mmPoint.Y + ny * rulerHeight * 0.15);
                        dc.DrawLine(markPen, mmPoint, mmEnd);
                    }
                }
            }
        }

        dc.Close();
        return visual;
    }

    private void OnChanged()
    {
        Changed?.Invoke();
    }
}