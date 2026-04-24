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
using System.Windows.Ink;

namespace FlipsiInk;

/// <summary>
/// Recognizes geometric shapes (circles, lines, rectangles, triangles)
/// from freehand strokes for auto-straightening.
/// v0.4.0: Enhanced with configurable confidence thresholds.
/// </summary>
public class ShapeRecognizer
{
    /// <summary>Snap tolerance in pixels for shape recognition.</summary>
    public double SnapTolerance { get; set; } = 15.0;

    /// <summary>Minimum confidence threshold for auto-recognition (0-1).</summary>
    public double AutoRecognizeThreshold { get; set; } = 0.65;

    /// <summary>Whether auto shape recognition is enabled.</summary>
    public bool AutoRecognizeEnabled { get; set; } = true;

    private const int MinimumPointCount = 5;

    /// <summary>
    /// Recognizes the shape in a single stroke.
    /// Returns null if no shape is recognized.
    /// </summary>
    public RecognizedShape? RecognizeStroke(Stroke stroke)
    {
        var points = GetPointsFromStroke(stroke);
        if (points.Length < MinimumPointCount)
            return null;

        var candidates = new List<RecognizedShape?>();

        var lineResult = RecognizeLine(points);
        if (lineResult != null) candidates.Add(lineResult);

        var triangleResult = RecognizeTriangle(points);
        if (triangleResult != null) candidates.Add(triangleResult);

        var rectResult = RecognizeRectangle(points);
        if (rectResult != null) candidates.Add(rectResult);

        var circleResult = RecognizeCircle(points);
        if (circleResult != null) candidates.Add(circleResult);

        var ellipseResult = RecognizeEllipse(points);
        if (ellipseResult != null) candidates.Add(ellipseResult);

        // Return the best candidate above threshold
        var best = candidates
            .Where(c => c != null && c.Confidence > AutoRecognizeThreshold)
            .OrderByDescending(c => c!.Confidence)
            .FirstOrDefault();

        // Fallback: lower threshold
        if (best == null)
        {
            best = candidates
                .Where(c => c != null && c.Confidence > 0.3)
                .OrderByDescending(c => c!.Confidence)
                .FirstOrDefault();
        }

        return best;
    }

    /// <summary>
    /// Straightens a stroke into a perfect geometric shape.
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
            case ShapeType.Triangle:
                for (int i = 0; i < shape.Points.Length; i++)
                    stylusPoints.Add(new StylusPoint(shape.Points[i].X, shape.Points[i].Y));
                stylusPoints.Add(new StylusPoint(shape.Points[0].X, shape.Points[0].Y));
                break;

            case ShapeType.Circle:
            case ShapeType.Ellipse:
                var ellipsePoints = GenerateEllipsePoints(shape);
                foreach (var pt in ellipsePoints)
                    stylusPoints.Add(new StylusPoint(pt.X, pt.Y));
                stylusPoints.Add(new StylusPoint(ellipsePoints[0].X, ellipsePoints[0].Y));
                break;
        }

        var newStroke = stylusPoints.Count > 0 ? new Stroke(stylusPoints) : null;
        if (newStroke != null)
        {
            newStroke.DrawingAttributes = stroke.DrawingAttributes.Clone();
        }
        return newStroke;
    }

    /// <summary>
    /// Recognizes and straightens in one step.
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
    /// Analyzes all strokes and returns recognized shapes.
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

    #region Recognition Methods

    private RecognizedShape? RecognizeLine(Point[] points)
    {
        if (points.Length < 2) return null;
        var start = points[0];
        var end = points[points.Length - 1];
        double lineLength = Distance(start, end);
        if (lineLength < SnapTolerance) return null;

        double totalDeviation = 0;
        foreach (var pt in points)
            totalDeviation += PointToLineDistance(pt, start, end);
        double avgDeviation = totalDeviation / points.Length;
        double confidence = Math.Max(0, 1.0 - (avgDeviation / SnapTolerance));

        if (avgDeviation < SnapTolerance)
        {
            return new RecognizedShape
            {
                Type = ShapeType.Line, Confidence = confidence,
                Points = new[] { start, end }, BoundingBox = new Rect(start, end)
            };
        }
        return null;
    }

    private RecognizedShape? RecognizeTriangle(Point[] points)
    {
        var corners = FindCorners(points, 3);
        if (corners == null || corners.Length != 3) return null;

        double closeDistance = Distance(points[0], points[points.Length - 1]);
        double maxDim = Math.Max(
            Math.Abs(corners[0].X - corners[2].X),
            Math.Abs(corners[0].Y - corners[2].Y));
        if (maxDim < 1) maxDim = 1;
        if (closeDistance > maxDim * 0.4) return null;

        double avgAngleDev = corners.Select((_, i) =>
            Math.Abs(AngleBetween(corners[i], corners[(i + 1) % 3], corners[(i + 2) % 3]) - 60)).Average();
        double confidence = Math.Max(0, 1.0 - (avgAngleDev / 40.0));

        return new RecognizedShape
        {
            Type = ShapeType.Triangle, Confidence = confidence,
            Points = corners, BoundingBox = GetBoundingBox(corners)
        };
    }

    private RecognizedShape? RecognizeRectangle(Point[] points)
    {
        var corners = FindCorners(points, 4);
        if (corners == null || corners.Length != 4) return null;

        double angleTolerance = 20.0;
        for (int i = 0; i < 4; i++)
        {
            double angle = AngleBetween(corners[i], corners[(i + 1) % 4], corners[(i + 2) % 4]);
            if (Math.Abs(angle - 90) > angleTolerance) return null;
        }

        double closeDistance = Distance(points[0], points[points.Length - 1]);
        double maxDim = Math.Max(
            Math.Abs(corners[0].X - corners[2].X),
            Math.Abs(corners[0].Y - corners[2].Y));
        if (maxDim < 1) maxDim = 1;
        if (closeDistance > maxDim * 0.3) return null;

        double avgAngleDev = corners.Select((_, i) =>
            Math.Abs(AngleBetween(corners[i], corners[(i + 1) % 4], corners[(i + 2) % 4]) - 90)).Average();
        double confidence = Math.Max(0, 1.0 - (avgAngleDev / angleTolerance));

        return new RecognizedShape
        {
            Type = ShapeType.Rectangle, Confidence = confidence,
            Points = corners, BoundingBox = GetBoundingBox(corners)
        };
    }

    private RecognizedShape? RecognizeCircle(Point[] points)
    {
        var center = GetCentroid(points);
        var distances = points.Select(p => Distance(p, center)).ToArray();
        double avgRadius = distances.Average();
        double deviation = distances.Select(d => Math.Abs(d - avgRadius)).Average();

        double closeDistance = Distance(points[0], points[points.Length - 1]);
        if (closeDistance > avgRadius * 0.4) return null;

        double radiusVariation = deviation / avgRadius;
        double confidence = Math.Max(0, 1.0 - (radiusVariation * 5));

        if (deviation < SnapTolerance && radiusVariation < 0.2)
        {
            return new RecognizedShape
            {
                Type = ShapeType.Circle, Confidence = confidence,
                Points = new[] { center },
                BoundingBox = new Rect(center.X - avgRadius, center.Y - avgRadius, avgRadius * 2, avgRadius * 2)
            };
        }
        return null;
    }

    private RecognizedShape? RecognizeEllipse(Point[] points)
    {
        var bbox = GetBoundingBox(points);
        double closeDistance = Distance(points[0], points[points.Length - 1]);
        double maxDim = Math.Max(bbox.Width, bbox.Height);
        if (closeDistance > maxDim * 0.4) return null;

        double cx = bbox.X + bbox.Width / 2;
        double cy = bbox.Y + bbox.Height / 2;
        double rx = bbox.Width / 2;
        double ry = bbox.Height / 2;
        if (rx < 1 || ry < 1) return null;

        double aspectRatio = rx / ry;
        if (aspectRatio is > 0.8 and < 1.25) return null;

        double totalDeviation = 0;
        foreach (var pt in points)
        {
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
                Type = ShapeType.Ellipse, Confidence = confidence,
                Points = new[] { new Point(cx, cy) }, BoundingBox = bbox
            };
        }
        return null;
    }

    #endregion

    #region Geometry Helpers

    private static Point[] GetPointsFromStroke(Stroke stroke)
    {
        var points = new Point[stroke.StylusPoints.Count];
        for (int i = 0; i < stroke.StylusPoints.Count; i++)
            points[i] = new Point(stroke.StylusPoints[i].X, stroke.StylusPoints[i].Y);
        return points;
    }

    private static double Distance(Point a, Point b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static double PointToLineDistance(Point pt, Point lineStart, Point lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double lengthSquared = dx * dx + dy * dy;
        if (lengthSquared < 0.001) return Distance(pt, lineStart);
        double cross = Math.Abs((pt.X - lineStart.X) * dy - (pt.Y - lineStart.Y) * dx);
        return cross / Math.Sqrt(lengthSquared);
    }

    private static double AngleBetween(Point pointA, Point pointB, Point pointC)
    {
        double ax = pointA.X - pointB.X, ay = pointA.Y - pointB.Y;
        double cx = pointC.X - pointB.X, cy = pointC.Y - pointB.Y;
        double dot = ax * cx + ay * cy;
        double cross = ax * cy - ay * cx;
        return Math.Atan2(Math.Abs(cross), dot) * (180.0 / Math.PI);
    }

    private Point[]? FindCorners(Point[] points, int targetCorners)
    {
        if (points.Length < targetCorners) return null;

        var cornerScores = new List<(int Index, double Score)>();
        double step = Math.Max(1, points.Length / 50.0);
        for (int i = 0; i < points.Length; i += (int)step)
        {
            int prev = Math.Max(0, i - (int)step);
            int next = Math.Min(points.Length - 1, i + (int)step);
            double angle = AngleBetween(points[prev], points[i], points[next]);
            if (angle < 160) cornerScores.Add((i, 160 - angle));
        }

        var bestCorners = cornerScores
            .OrderByDescending(c => c.Score)
            .Take(targetCorners)
            .OrderBy(c => c.Index)
            .Select(c => points[c.Index])
            .ToArray();

        return bestCorners.Length == targetCorners ? bestCorners : null;
    }

    private static Point GetCentroid(Point[] points) =>
        new(points.Average(p => p.X), points.Average(p => p.Y));

    private static Rect GetBoundingBox(Point[] points) =>
        new(points.Min(p => p.X), points.Min(p => p.Y),
            points.Max(p => p.X) - points.Min(p => p.X),
            points.Max(p => p.Y) - points.Min(p => p.Y));

    private static Point[] GenerateEllipsePoints(RecognizedShape shape)
    {
        const int pointCount = 72;
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

public enum ShapeType { None, Line, Rectangle, Circle, Ellipse, Triangle }

public class RecognizedShape
{
    public ShapeType Type { get; set; } = ShapeType.None;
    public double Confidence { get; set; } = 0.0;
    public Point[] Points { get; set; } = Array.Empty<Point>();
    public Rect BoundingBox { get; set; } = Rect.Empty;
    public Stroke? StraightenedStroke { get; set; }
}