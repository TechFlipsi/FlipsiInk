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
/// Result of math expression recognition from ink strokes.
/// </summary>
public class MathRecognitionResult
{
    public string Latex { get; set; } = "";
    public string PlainText { get; set; } = "";
    public double Confidence { get; set; }
    public List<RecognizedSymbol> Symbols { get; set; } = new();
}

/// <summary>
/// A single recognized symbol with its position and bounding box.
/// </summary>
public class RecognizedSymbol
{
    public string Symbol { get; set; } = "";
    public string LatexCode { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// Recognizes mathematical expressions from ink strokes using geometric/heuristic analysis.
/// Detects numbers, operators, fractions, powers, and common math symbols.
/// </summary>
public class MathRecognizer
{
    // ── Symbol Recognition Patterns ────────────────────────────

    private static readonly Dictionary<string, (string Plain, string Latex)> MathSymbolMap = new()
    {
        // Basic operators
        { "+", ("+", "+") },
        { "-", ("-", "-") },
        { "×", ("×", "\\times") },
        { "÷", ("÷", "\\div") },
        { "=", ("=", "=") },
        // Comparison
        { "<", ("<", "<") },
        { ">", (">", ">") },
        { "≤", ("≤", "\\leq") },
        { "≥", ("≥", "\\geq") },
        { "≠", ("≠", "\\neq") },
        { "≈", ("≈", "\\approx") },
        // Greek
        { "π", ("π", "\\pi") },
        { "α", ("α", "\\alpha") },
        { "β", ("β", "\\beta") },
        { "θ", ("θ", "\\theta") },
        { "γ", ("γ", "\\gamma") },
        { "δ", ("δ", "\\delta") },
        { "λ", ("λ", "\\lambda") },
        { "μ", ("μ", "\\mu") },
        { "σ", ("σ", "\\sigma") },
        { "φ", ("φ", "\\phi") },
        { "ω", ("ω", "\\omega") },
        { "Ω", ("Ω", "\\Omega") },
        // Powers / Roots
        { "²", ("²", "^{2}") },
        { "³", ("³", "^{3}") },
        { "√", ("√", "\\sqrt") },
        // Calculus
        { "∫", ("∫", "\\int") },
        { "∑", ("∑", "\\sum") },
        { "∏", ("∏", "\\prod") },
        { "∂", ("∂", "\\partial") },
        { "∞", ("∞", "\\infty") },
        { "∇", ("∇", "\\nabla") },
        // Brackets
        { "(", ("(", "(") },
        { ")", (")", ")") },
        { "[", ("[", "[") },
        { "]", ("]", "]") },
        { "|", ("|", "|") },
        { "{", ("{", "\\{") },
        { "}", ("}", "\\}") },
        // Misc
        { "°", ("°", "^{\\circ}") },
        { "%", ("%", "\\%") },
        { "‰", ("‰", "\\permil") },
    };

    /// <summary>
    /// Recognizes a math expression from a collection of strokes.
    /// Groups strokes by spatial proximity, then assembles into an expression.
    /// </summary>
    public MathRecognitionResult Recognize(StrokeCollection strokes)
    {
        if (strokes == null || strokes.Count == 0)
            return new MathRecognitionResult { Confidence = 0 };

        // Step 1: Group strokes into symbol clusters based on proximity
        var groups = GroupStrokesByProximity(strokes);

        // Step 2: Recognize each group as a symbol or character
        var symbols = new List<RecognizedSymbol>();
        foreach (var group in groups.OrderBy(g => g.MinX))
        {
            var symbol = RecognizeGroup(group);
            if (symbol != null)
                symbols.Add(symbol);
        }

        // Step 3: Detect structural elements (fractions, powers)
        var (latex, plainText) = AssembleExpression(symbols);

        // Step 4: Calculate overall confidence
        var confidence = symbols.Count > 0 ? symbols.Average(s => s.Confidence) : 0;

        return new MathRecognitionResult
        {
            Latex = latex,
            PlainText = plainText,
            Confidence = confidence,
            Symbols = symbols
        };
    }

    // ── Stroke Grouping ────────────────────────────────────────

    private class StrokeGroup
    {
        public List<Stroke> Strokes { get; set; } = new();
        public double MinX => Strokes.Min(s => s.GetBounds().Left);
        public double MaxX => Strokes.Max(s => s.GetBounds().Right);
        public double MinY => Strokes.Min(s => s.GetBounds().Top);
        public double MaxY => Strokes.Max(s => s.GetBounds().Bottom);
        public double CenterX => (MinX + MaxX) / 2;
        public double CenterY => (MinY + MaxY) / 2;
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
    }

    private List<StrokeGroup> GroupStrokesByProximity(StrokeCollection strokes)
    {
        var groups = new List<StrokeGroup>();
        var assigned = new HashSet<Stroke>();
        const double proximityThreshold = 15.0; // pixels

        foreach (Stroke stroke in strokes)
        {
            if (assigned.Contains(stroke)) continue;

            var bounds = stroke.GetBounds();
            var group = new StrokeGroup();
            group.Strokes.Add(stroke);
            assigned.Add(stroke);

            // Merge nearby strokes into this group
            bool merged = true;
            while (merged)
            {
                merged = false;
                foreach (Stroke other in strokes)
                {
                    if (assigned.Contains(other)) continue;
                    var otherBounds = other.GetBounds();

                    // Check if this stroke is close to any stroke in the group
                    foreach (var groupStroke in group.Strokes)
                    {
                        var gBounds = groupStroke.GetBounds();
                        var expandBounds = new System.Windows.Rect(
                            gBounds.Left - proximityThreshold,
                            gBounds.Top - proximityThreshold,
                            gBounds.Width + 2 * proximityThreshold,
                            gBounds.Height + 2 * proximityThreshold);

                        if (expandBounds.IntersectsWith(otherBounds))
                        {
                            group.Strokes.Add(other);
                            assigned.Add(other);
                            merged = true;
                            break;
                        }
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    // ── Symbol Recognition ─────────────────────────────────────

    private RecognizedSymbol? RecognizeGroup(StrokeGroup group)
    {
        // First try to recognize as a known math symbol
        var symbolResult = RecognizeMathSymbol(group);
        if (symbolResult != null && symbolResult.Confidence > 0.5)
            return symbolResult;

        // Then try digit recognition
        var digitResult = RecognizeDigit(group);
        if (digitResult != null)
            return digitResult;

        // Fallback: use spatial properties
        return new RecognizedSymbol
        {
            Symbol = "?",
            LatexCode = "?",
            X = group.CenterX,
            Y = group.CenterY,
            Confidence = 0.2
        };
    }

    private RecognizedSymbol? RecognizeMathSymbol(StrokeGroup group)
    {
        if (group.Strokes.Count == 0) return null;

        var bounds = new System.Windows.Rect(group.MinX, group.MinY, group.Width, group.Height);
        double aspectRatio = group.Width > 0 ? group.Height / group.Width : 1;
        double avgStrokeLength = group.Strokes.Average(s => s.StylusPoints.Count);

        // Plus sign (+): two crossing strokes, roughly square aspect
        if (group.Strokes.Count == 2 && aspectRatio is > 0.7 and < 1.4)
        {
            var s1Bounds = group.Strokes[0].GetBounds();
            var s2Bounds = group.Strokes[1].GetBounds();
            bool s1Horizontal = s1Bounds.Width > s1Bounds.Height;
            bool s2Horizontal = s2Bounds.Width > s2Bounds.Height;
            if (s1Horizontal != s2Horizontal)
            {
                return new RecognizedSymbol
                {
                    Symbol = "+", LatexCode = "+",
                    X = group.CenterX, Y = group.CenterY, Confidence = 0.85
                };
            }
        }

        // Minus sign (-): single horizontal stroke, width >> height
        if (group.Strokes.Count == 1 && aspectRatio < 0.4 && group.Width > 10)
        {
            return new RecognizedSymbol
            {
                Symbol = "-", LatexCode = "-",
                X = group.CenterX, Y = group.CenterY, Confidence = 0.8
            };
        }

        // Equals (==): two parallel horizontal strokes
        if (group.Strokes.Count == 2)
        {
            var s1Bounds = group.Strokes[0].GetBounds();
            var s2Bounds = group.Strokes[1].GetBounds();
            bool bothHorizontal = s1Bounds.Width > s1Bounds.Height * 2 && s2Bounds.Width > s2Bounds.Height * 2;
            double verticalGap = Math.Abs(s1Bounds.Top - s2Bounds.Top);
            double avgHeight = (s1Bounds.Height + s2Bounds.Height) / 2;
            if (bothHorizontal && verticalGap < avgHeight * 3)
            {
                return new RecognizedSymbol
                {
                    Symbol = "=", LatexCode = "=",
                    X = group.CenterX, Y = group.CenterY, Confidence = 0.8
                };
            }
        }

        // Division (÷): minus with dots above and below
        // This is rare in handwriting, skip for now

        // Angle brackets (<, >): single stroke forming an angle
        if (group.Strokes.Count == 1 && group.Width > 10 && group.Height > 5)
        {
            var points = GetStrokePoints(group.Strokes[0]);
            if (points.Length >= 3)
            {
                double startEndDY = Math.Abs(points[0].Y - points[^1].Y);
                double minY = points.Min(p => p.Y);
                double apexY = points.Min(p => p.Y);
                bool isVShape = Math.Abs(points[points.Length / 2].Y - minY) < 5;

                if (startEndDY < group.Height * 0.3 && group.Width > group.Height * 1.2)
                {
                    // Could be < or >
                    bool opensRight = points[^1].X > points[0].X;
                    if (!opensRight)
                    {
                        return new RecognizedSymbol
                        {
                            Symbol = "<", LatexCode = "<",
                            X = group.CenterX, Y = group.CenterY, Confidence = 0.6
                        };
                    }
                    else
                    {
                        return new RecognizedSymbol
                        {
                            Symbol = ">", LatexCode = ">",
                            X = group.CenterX, Y = group.CenterY, Confidence = 0.6
                        };
                    }
                }
            }
        }

        // Parentheses: curved single strokes
        if (group.Strokes.Count == 1 && group.Height > group.Width * 1.5 && group.Width < 20)
        {
            bool curvesLeft = group.Strokes[0].StylusPoints[0].X > group.CenterX;
            return new RecognizedSymbol
            {
                Symbol = curvesLeft ? ")" : "(",
                LatexCode = curvesLeft ? ")" : "(",
                X = group.CenterX, Y = group.CenterY, Confidence = 0.7
            };
        }

        return null;
    }

    private RecognizedSymbol? RecognizeDigit(StrokeGroup group)
    {
        // Basic digit recognition using stroke count and spatial features
        // This is a simplified heuristic — Florence-2 OCR handles the heavy lifting
        int strokeCount = group.Strokes.Count;
        var bounds = new System.Windows.Rect(group.MinX, group.MinY, group.Width, group.Height);
        double aspectRatio = group.Width > 0 ? group.Height / group.Width : 1;

        // Single stroke digits: 0, 1, 2, 3, 6, 7, 8, 9
        // Two stroke digits: 4, 5 (sometimes)

        // 1: very tall and narrow, single stroke
        if (strokeCount == 1 && aspectRatio > 3 && group.Width < 15)
        {
            return new RecognizedSymbol
            {
                Symbol = "1", LatexCode = "1",
                X = group.CenterX, Y = group.CenterY, Confidence = 0.7
            };
        }

        // 0: roughly circular, single stroke
        if (strokeCount == 1 && aspectRatio is > 0.7 and < 1.4 && group.Width > 10 && group.Height > 10)
        {
            // Check circularity using the shape recognizer
            var shape = new ShapeRecognizer().RecognizeStroke(group.Strokes[0]);
            if (shape != null && shape.Type == ShapeType.Circle)
            {
                return new RecognizedSymbol
                {
                    Symbol = "0", LatexCode = "0",
                    X = group.CenterX, Y = group.CenterY, Confidence = shape.Confidence
                };
            }
        }

        // For other digits, we rely on the OCR engine — return unknown
        return null;
    }

    // ── Expression Assembly ─────────────────────────────────────

    private (string latex, string plainText) AssembleExpression(List<RecognizedSymbol> symbols)
    {
        if (symbols.Count == 0) return ("", "");

        // Calculate baseline (median Y position)
        var baselineY = symbols.OrderBy(s => s.Y).Skip(symbols.Count / 2).First().Y;
        var avgHeight = symbols.Count > 0 ? symbols.Average(s => Math.Max(1, GetSymbolHeight(s))) : 20;
        double superscriptThreshold = avgHeight * 0.4;

        var latexParts = new List<string>();
        var plainParts = new List<string>();

        for (int i = 0; i < symbols.Count; i++)
        {
            var sym = symbols[i];

            // Detect superscript (power): symbol Y is significantly above baseline
            if (sym.Y < baselineY - superscriptThreshold && i > 0)
            {
                latexParts.Add("^{" + sym.LatexCode + "}");
                plainParts.Add(sym.Symbol == sym.PlainText ? "^" + sym.Symbol : $"^{sym.Symbol}");
            }
            // Detect fraction line (horizontal stroke between two groups)
            else if (sym.Symbol == "-" && i > 0 && i < symbols.Count - 1)
            {
                var above = symbols.Take(i).Where(s => s.Y < sym.Y).ToList();
                var below = symbols.Skip(i + 1).Where(s => s.Y > sym.Y).ToList();
                if (above.Count > 0 && below.Count > 0)
                {
                    var numLatex = string.Join(" ", above.Select(s => s.LatexCode));
                    var denLatex = string.Join(" ", below.Select(s => s.LatexCode));
                    var numPlain = string.Join("", above.Select(s => s.Symbol));
                    var denPlain = string.Join("", below.Select(s => s.Symbol));
                    latexParts.Add($"\\frac{{{numLatex}}}{{{denLatex}}}");
                    plainParts.Add($"({numPlain})/({denPlain})");
                    // Mark consumed symbols
                    continue;
                }
                else
                {
                    latexParts.Add(sym.LatexCode);
                    plainParts.Add(sym.Symbol);
                }
            }
            else
            {
                latexParts.Add(sym.LatexCode);
                plainParts.Add(sym.Symbol);
            }
        }

        return (string.Join(" ", latexParts), string.Join(" ", plainParts));
    }

    private double GetSymbolHeight(RecognizedSymbol sym)
    {
        // Estimate height based on common symbols
        return sym.Symbol.Length switch
        {
            1 => 20, // Single character
            > 1 => 25, // Multi-character (like √)
            _ => 20
        };
    }

    private static System.Windows.Point[] GetStrokePoints(Stroke stroke)
    {
        var points = new System.Windows.Point[stroke.StylusPoints.Count];
        for (int i = 0; i < stroke.StylusPoints.Count; i++)
            points[i] = new System.Windows.Point(stroke.StylusPoints[i].X, stroke.StylusPoints[i].Y);
        return points;
    }
}