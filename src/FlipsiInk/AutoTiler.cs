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
using System.Text;
using System.Threading.Tasks;
using System.Windows.Ink;

namespace FlipsiInk;

/// <summary>
/// AI-powered auto-title generation from note content.
/// Analyzes stroke patterns, recognized text, and page metadata to suggest a title.
/// v0.4.0: Simple heuristic-based approach (no external API calls).
/// </summary>
public class AutoTiler
{
    /// <summary>Maximum title length.</summary>
    public int MaxTitleLength { get; set; } = 40;

    /// <summary>Whether auto-title generation is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Generates a title from the current page's strokes and any recognized text.
    /// Uses heuristics: first line of recognized text, date detection, or fallback.
    /// </summary>
    /// <param name="strokes">Current page strokes.</param>
    /// <param name="recognizedText">OCR-recognized text (can be empty).</param>
    /// <param name="pageNumber">Current page number.</param>
    /// <returns>Suggested title string.</returns>
    public string GenerateTitle(StrokeCollection strokes, string? recognizedText = null, int pageNumber = 1)
    {
        if (!IsEnabled) return string.Empty;

        // Strategy 1: Use recognized text (first line)
        if (!string.IsNullOrWhiteSpace(recognizedText))
        {
            var firstLine = recognizedText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));

            if (firstLine != null)
            {
                var title = firstLine.Trim();
                if (title.Length > MaxTitleLength)
                    title = title[..MaxTitleLength] + "...";
                return title;
            }
        }

        // Strategy 2: Detect dates in strokes (common in notes)
        var textForDate = recognizedText ?? string.Empty;
        var dates = DateDetector.Detect(textForDate);
        if (dates.Count > 0)
        {
            var dateStr = dates[0].DateTime?.ToString("dd.MM.yyyy") ?? dates[0].RawText;
            return $"Notizen {dateStr}";
        }

        // Strategy 3: Analyze stroke density for content type hints
        if (strokes.Count > 0)
        {
            var totalPoints = strokes.Sum(s => s.StylusPoints.Count);
            var avgPointsPerStroke = totalPoints / (double)Math.Max(1, strokes.Count);

            if (avgPointsPerStroke < 10)
                return "Kurze Notizen";
            if (avgPointsPerStroke < 30)
                return "Notizen";
            if (strokes.Count > 20)
                return "Ausfuehrliche Notizen";
            return "Skizze";
        }

        // Fallback: Page number based
        return pageNumber > 1 ? $"Seite {pageNumber}" : "Unbenannt";
    }

    /// <summary>
    /// Generates a title asynchronously (for background use).
    /// </summary>
    public async Task<string> GenerateTitleAsync(StrokeCollection strokes, string? recognizedText = null, int pageNumber = 1)
    {
        return await Task.Run(() => GenerateTitle(strokes, recognizedText, pageNumber));
    }
}