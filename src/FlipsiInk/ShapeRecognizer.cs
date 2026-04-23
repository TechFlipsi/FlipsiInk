// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;

namespace FlipsiInk;

/// <summary>
/// Art der erkannten Form.
/// </summary>
public enum ShapeType
{
    None,
    Line,
    Rectangle,
    Circle,
    Ellipse,
    Triangle
}

/// <summary>
/// Ergebnis der Formerkennung mit Typ, Konfidenz, Eckpunkten und Bounding Box.
/// </summary>
public class RecognizedShape
{
    public ShapeType Type { get; set; } = ShapeType.None;
    public double Confidence { get; set; } = 0.0;
    public Point[] Points { get; set; } = Array.Empty<Point>();
    public Rect BoundingBox { get; set; } = Rect.Empty;

    /// <summary>
    /// Der geglättete/perfekte Stroke nach Straighten. Null wenn noch nicht geglättet.
    /// </summary>
    public Stroke? StraightenedStroke { get; set; }
}

/// <summary>
/// Erkennt geometrische Formen in Handeingabe-Strokes und kann diese
/// in perfekte Formen umwandeln (Straightening).
/// Erkennung passiert NACH Stroke-Ende (StrokeCollected Event).
/// </summary>
public class ShapeRecognizer
{
    /// <summary>
    /// Schwellwert in Pixeln für die Toleranz bei der Erkennung.
    /// Konfigurierbar – höhere Werte sind toleranter gegenüber ungenauer Eingabe.
    /// </summary>
    public double SnapTolerance { get; set; } = 15.0;

    /// <summary>
    /// Minimalanzahl von StylusPoints, damit ein Stroke analysiert wird.
    /// </summary>
    private const int MinimumPointCount = 5;

    /// <summary>
    /// Erkennt die Form in einem einzelnen Stroke.
    /// Gibt null zurück, wenn keine Form erkannt wird.
    /// </summary>
    public RecognizedShape? RecognizeStroke(Stroke stroke)
    {
        var points = GetPointsFromStroke(stroke);
        if (points.Length < MinimumPointCount)
            return null;

        // Reihenfolge: Linie → Dreieck → Rechteck → Kreis → Ellipse
        var lineResult = RecognizeLine(points);
        if (lineResult != null && lineResult.Confidence > 0.6)
            return lineResult;

        var triangleResult = RecognizeTriangle(points);
        if (triangleResult != null && triangleResult.Confidence > 0.6)
            return triangleResult;

        var rectResult = RecognizeRectangle(points);
        if (rectResult != null && rectResult.Confidence > 0.6)
            return rectResult;

        var circleResult = RecognizeCircle(points);
        if (circleResult != null && circleResult.Confidence > 0.6)
            return circleResult;

        var ellipseResult = RecognizeEllipse(points);
        if (ellipseResult != null && ellipseResult.Confidence > 0.6)
            return ellipseResult;

        // Zweiter Durchlauf mit niedrigerer Schwelle – bestes Ergebnis wählen
        var candidates = new[] { lineResult, triangleResult, rectResult, circleResult, ellipseResult }
            .Where(c => c != null && c.Confidence > 0.3)
            .OrderByDescending(c => c!.Confidence)
            .ToList();

        return candidates.FirstOrDefault();
    }

    /// <summary>
    /// Wandelt einen wackeligen Stroke in die perfekte Form um.
    /// Gibt den neuen perfekten Stroke zurück oder null bei Fehler.
    /// </summary>
    public Stroke? StraightenStroke(Stroke stroke, RecognizedShape shape)
    {
        if (shape.Type == ShapeType.None || shape.Points.Length == 0)
            return null;

        var stylusPoints = new StylusPointCollection();

        switch (shape.Type)
        {
            case ShapeType.Line:
                stylusPoints.Add(new StylusPoint(shape.Points[0].X, shape.Points[0].Y));
                stylusPoints.Add(new StylusPoint(shape.Points[1].X, shape.Points[1].Y));
                break;

            case ShapeType.Rectangle:
                for (int i = 0; i < shape.Points.Length; i++)
                    stylusPoints.Add(new StylusPoint(shape.Points[i].X, shape.Points[i].Y));
                // Schließen: zurück zum Startpunkt
                stylusPoints.Add(new StylusPoint(shape.Points[0].X, shape.Points[0].Y));
                break;

            case ShapeType.Triangle:
                for (int i = 0; i < shape.Points.Length; i++)
                    stylusPoints.Add(new StylusPoint(shape.Points[i].X, shape.Points[i].Y));
                stylusPoints.Add(new StylusPoint(shape.Points[0].X, shape.Points[0].Y));
                break;

            case ShapeType.Circle:
            case ShapeType.Ellipse:
                // Form als Polylinie mit vielen Punkten annähern
                var ellipsePoints = GenerateEllipsePoints(shape);
                foreach (var pt in ellipsePoints)
                    stylusPoints.Add(new StylusPoint(pt.X, pt.Y));
                // Schließen
                stylusPoints.Add(new StylusPoint(ellipsePoints[0].X, ellipsePoints[0].Y));
                break;
        }

        return stylusPoints.Count > 0 ? new Stroke(stylusPoints) : null;
    }

    /// <summary>
    /// Erkennt die Form und wandelt sie in einem Schritt um.
    /// Gibt null zurück, wenn keine Form erkannt wurde.
    /// </summary>
    public RecognizedShape? RecognizeAndStraighten(Stroke stroke)
    {
        var shape = RecognizeStroke(stroke);
        if (shape == null || shape.Type == ShapeType.None)
            return null;

        var straightened = StraightenStroke(stroke, shape);
        shape.StraightenedStroke = straightened;
        return shape;
    }

    /// <summary>
    /// Analysiert alle Strokes in einer Sammlung und gibt erkannte Formen zurück.
    /// </summary>
    public List<RecognizedShape> AnalyzeAllStrokes(StrokeCollection strokes)
    {
        var results = new List<RecognizedShape>();
        foreach (Stroke? stroke in strokes)
        {
            if (stroke == null) continue;
            var shape = RecognizeStroke(stroke);
            if (shape != null && shape.Type != ShapeType.None)
                results.Add(shape);
        }
        return results;
    }

    #region Erkennungsmethoden

    /// <summary>
    /// Erkennt eine gerade Linie: Prüft ob alle Punkte nah an einer Geraden liegen.
    /// </summary>
    private RecognizedShape? RecognizeLine(Point[] points)
    {
        if (points.Length < 2)
            return null;

        var start = points[0];
        var end = points[points.Length - 1];
        double lineLength = Distance(start, end);

        if (lineLength < SnapTolerance)
            return null;

        // Durchschnittliche Abweichung aller Punkte von der Geraden berechnen
        double totalDeviation = 0;
        foreach (var pt in points)
        {
            totalDeviation += PointToLineDistance(pt, start, end);
        }
        double avgDeviation = totalDeviation / points.Length;

        // Konfidenz: je geringer die Abweichung, desto höher
        double confidence = Math.Max(0, 1.0 - (avgDeviation / SnapTolerance));

        if (avgDeviation < SnapTolerance)
        {
            return new RecognizedShape
            {
                Type = ShapeType.Line,
                Confidence = confidence,
                Points = new[] { start, end },
                BoundingBox = new Rect(start, end)
            };
        }

        return null;
    }

    /// <summary>
    /// Erkennt ein Rechteck: 4 Eckpunkte, nahezu 90° Winkel, geschlossen.
    /// </summary>
    private RecognizedShape? RecognizeRectangle(Point[] points)
    {
        var corners = FindCorners(points, 4);
        if (corners == null || corners.Length != 4)
            return null;

        // Prüfe ob die Winkel nahe 90° sind
        double angleTolerance = 20.0; // Grad Toleranz
        for (int i = 0; i < 4; i++)
        {
            double angle = AngleBetween(corners[i], corners[(i + 1) % 4], corners[(i + 2) % 4]);
            if (Math.Abs(angle - 90) > angleTolerance)
                return null;
        }

        // Prüfe ob die Form geschlossen ist (Ende nahe Start)
        double closeDistance = Distance(points[0], points[points.Length - 1]);
        double maxDim = Math.Max(corners[0].X - corners[2].X, corners[0].Y - corners[2].Y);
        if (closeDistance > maxDim * 0.3)
            return null;

        // Konfidenz basierend auf Winkel-Abweichung
        double avgAngleDev = corners.Select((_, i) => Math.Abs(AngleBetween(corners[i], corners[(i + 1) % 4], corners[(i + 2) % 4]) - 90))
            .Average();
        double confidence = Math.Max(0, 1.0 - (avgAngleDev / angleTolerance));

        var bbox = GetBoundingBox(corners);

        return new RecognizedShape
        {
            Type = ShapeType.Rectangle,
            Confidence = confidence,
            Points = corners,
            BoundingBox = bbox
        };
    }

    /// <summary>
    /// Erkennt einen Kreis: Punkte liegen auf einem Kreis (Fit-Algorithmus).
    /// </summary>
    private RecognizedShape? RecognizeCircle(Point[] points)
    {
        var center = GetCentroid(points);
        var distances = points.Select(p => Distance(p, center)).ToArray();
        double avgRadius = distances.Average();
        double deviation = distances.Select(d => Math.Abs(d - avgRadius)).Average();

        // Prüfe ob die Form geschlossen ist
        double closeDistance = Distance(points[0], points[points.Length - 1]);
        double avgDist = distances.Average();
        if (closeDistance > avgDist * 0.4)
            return null;

        // Kreis: Abweichung der Radien sollte klein sein
        double radiusVariation = deviation / avgRadius;
        double confidence = Math.Max(0, 1.0 - (radiusVariation * 5));

        if (deviation < SnapTolerance && radiusVariation < 0.2)
        {
            return new RecognizedShape
            {
                Type = ShapeType.Circle,
                Confidence = confidence,
                Points = new[] { center },
                BoundingBox = new Rect(center.X - avgRadius, center.Y - avgRadius, avgRadius * 2, avgRadius * 2)
            };
        }

        return null;
    }

    /// <summary>
    /// Erkennt eine Ellipse: Wie Kreis, aber mit unterschiedlicher Ausdehnung in X und Y.
    /// </summary>
    private RecognizedShape? RecognizeEllipse(Point[] points)
    {
        var bbox = GetBoundingBox(points);

        // Prüfe ob geschlossen
        double closeDistance = Distance(points[0], points[points.Length - 1]);
        double maxDim = Math.Max(bbox.Width, bbox.Height);
        if (closeDistance > maxDim * 0.4)
            return null;

        // Ellipsen-Fit: Prüfe Abweichung der Punkte von der perfekten Ellipse
        double cx = bbox.X + bbox.Width / 2;
        double cy = bbox.Y + bbox.Height / 2;
        double rx = bbox.Width / 2;
        double ry = bbox.Height / 2;

        if (rx < 1 || ry < 1)
            return null;

        // Seitenverhältnis prüfen – wenn nah an 1.0, ist es eher ein Kreis
        double aspectRatio = rx / ry;
        if (aspectRatio is > 0.8 and < 1.25)
            return null; // Das ist eher ein Kreis

        double totalDeviation = 0;
        foreach (var pt in points)
        {
            // Normalisierte Koordinaten auf der Einheitsellipse
            double nx = (pt.X - cx) / rx;
            double ny = (pt.Y - cy) / ry;
            double r = Math.Sqrt(nx * nx + ny * ny);
            totalDeviation += Math.Abs(r - 1.0) * Math.Max(rx, ry);
        }

        double avgDeviation = totalDeviation / points.Length;
        double confidence = Math.Max(0, 1.0 - (avgDeviation / SnapTolerance));

        if (avgDeviation < SnapTolerance)
        {
            return new RecognizedShape
            {
                Type = ShapeType.Ellipse,
                Confidence = confidence,
                Points = new[] { new Point(cx, cy) },
                BoundingBox = bbox
            };
        }

        return null;
    }

    /// <summary>
    /// Erkennt ein Dreieck: 3 Eckpunkte.
    /// </summary>
    private RecognizedShape? RecognizeTriangle(Point[] points)
    {
        var corners = FindCorners(points, 3);
        if (corners == null || corners.Length != 3)
            return null;

        // Prüfe ob geschlossen
        double closeDistance = Distance(points[0], points[points.Length - 1]);
        double maxDim = Math.Max(
            Math.Max(corners[0].X, corners[1].X) - Math.Min(corners[0].X, corners[1].X),
            Math.Max(corners[0].Y, corners[1].Y) - Math.Min(corners[0].Y, corners[1].Y));
        if (closeDistance > maxDim * 0.4)
            return null;

        // Konfidenz: Wie gut liegen die Punkte auf den 3 Linien?
        double totalDeviation = 0;
        int pointCount = 0;
        for (int i = 0; i < points.Length; i++)
        {
            double minDist = double.MaxValue;
            for (int e = 0; e < 3; e++)
            {
                double d = PointToLineDistance(points[i], corners[e], corners[(e + 1) % 3]);
                minDist = Math.Min(minDist, d);
            }
            totalDeviation += minDist;
            pointCount++;
        }

        double avgDeviation = totalDeviation / pointCount;
        double confidence = Math.Max(0, 1.0 - (avgDeviation / SnapTolerance));

        if (avgDeviation < SnapTolerance)
        {
            return new RecognizedShape
            {
                Type = ShapeType.Triangle,
                Confidence = confidence,
                Points = corners,
                BoundingBox = GetBoundingBox(corners)
            };
        }

        return null;
    }

    #endregion

    #region Hilfsmethoden

    /// <summary>
    /// Extrahiert Point[] aus einem Stroke.
    /// </summary>
    private static Point[] GetPointsFromStroke(Stroke stroke)
    {
        return stroke.StylusPoints.Select(sp => new Point(sp.X, sp.Y)).ToArray();
    }

    /// <summary>
    /// Berechnet den Abstand zwischen zwei Punkten.
    /// </summary>
    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Berechnet den Abstand eines Punktes von einer Geraden (definiert durch zwei Punkte).
    /// </summary>
    private static double PointToLineDistance(Point pt, Point lineStart, Point lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < 0.001)
            return Distance(pt, lineStart);

        // Kreuzprodukt-Methode für Abstand Punkt-Gerade
        double cross = Math.Abs((pt.X - lineStart.X) * dy - (pt.Y - lineStart.Y) * dx);
        return cross / Math.Sqrt(lengthSquared);
    }

    /// <summary>
    /// Berechnet den Winkel bei pointB zwischen pointA-pointB und pointB-pointC in Grad.
    /// </summary>
    private static double AngleBetween(Point pointA, Point pointB, Point pointC)
    {
        double ax = pointA.X - pointB.X;
        double ay = pointA.Y - pointB.Y;
        double cx = pointC.X - pointB.X;
        double cy = pointC.Y - pointB.Y;

        double dot = ax * cx + ay * cy;
        double cross = ax * cy - ay * cx;
        double angle = Math.Atan2(Math.Abs(cross), dot) * (180.0 / Math.PI);
        return angle;
    }

    /// <summary>
    /// Findet die Eckpunkte einer Form durch Douglas-Peucker-ähnliche Vereinfachung.
    /// Gibt null zurück, wenn nicht genug Eckpunkte gefunden werden.
    /// </summary>
    private Point[]? FindCorners(Point[] points, int targetCorners)
    {
        if (points.Length < targetCorners)
            return null;

        // Winkel-basierte Eckpunkterkennung
        var cornerScores = new List<(int Index, double Score)>();

        double step = Math.Max(1, points.Length / 50.0);
        for (int i = 0; i < points.Length; i += (int)step)
        {
            int prev = Math.Max(0, i - (int)step);
            int next = Math.Min(points.Length - 1, i + (int)step);
            double angle = AngleBetween(points[prev], points[i], points[next]);

            // Spitze Winkel = hohes Score
            if (angle < 160)
            {
                cornerScores.Add((i, 160 - angle));
            }
        }

        // Nimm die besten targetCorners Eckpunkte
        var bestCorners = cornerScores
            .OrderByDescending(c => c.Score)
            .Take(targetCorners)
            .OrderBy(c => c.Index)
            .Select(c => points[c.Index])
            .ToArray();

        if (bestCorners.Length != targetCorners)
            return null;

        return bestCorners;
    }

    /// <summary>
    /// Berechnet den Mittelpunkt (Zentroid) einer Punktemenge.
    /// </summary>
    private static Point GetCentroid(Point[] points)
    {
        double cx = points.Average(p => p.X);
        double cy = points.Average(p => p.Y);
        return new Point(cx, cy);
    }

    /// <summary>
    /// Berechnet die Bounding Box einer Punktemenge.
    /// </summary>
    private static Rect GetBoundingBox(Point[] points)
    {
        double minX = points.Min(p => p.X);
        double minY = points.Min(p => p.Y);
        double maxX = points.Max(p => p.X);
        double maxY = points.Max(p => p.Y);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Generiert Punkte entlang einer perfekten Ellipse/Kreis für die Straighten-Funktion.
    /// </summary>
    private static Point[] GenerateEllipsePoints(RecognizedShape shape)
    {
        const int pointCount = 72; // Alle 5 Grad
        var result = new Point[pointCount];

        double cx = shape.BoundingBox.X + shape.BoundingBox.Width / 2;
        double cy = shape.BoundingBox.Y + shape.BoundingBox.Height / 2;
        double rx = shape.BoundingBox.Width / 2;
        double ry = shape.BoundingBox.Height / 2;

        for (int i = 0; i < pointCount; i++)
        {
            double angle = 2 * Math.PI * i / pointCount;
            result[i] = new Point(cx + rx * Math.Cos(angle), cy + ry * Math.Sin(angle));
        }

        return result;
    }

    #endregion
}
