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
using System.Text.RegularExpressions;

namespace FlipsiInk;

/// <summary>
/// Detects table structures in OCR text output.
/// Recognizes grid-like patterns (rows/columns) separated by
/// tabs, multiple spaces, or pipe characters.
/// Issue #36: Smart Snapshot OCR-Erweiterung (Tabellen).
/// </summary>
public static class TableDetector
{
    // Minimum number of rows and columns to consider a table
    private const int MinRows = 2;
    private const int MinColumns = 2;

    // Separators that indicate column boundaries
    private static readonly Regex PipeSeparator = new(@"\s*\|\s*", RegexOptions.Compiled);
    private static readonly Regex MultiSpaceSeparator = new(@"\s{2,}", RegexOptions.Compiled);
    private static readonly Regex TabSeparator = new(@"\t+", RegexOptions.Compiled);

    /// <summary>
    /// Represents a detected table with rows and columns.
    /// </summary>
    public record DetectedTable(string[][] Cells, int StartLine, int EndLine);

    /// <summary>
    /// Detects tables in multi-line OCR text.
    /// Returns a list of detected tables with their cell data.
    /// </summary>
    public static List<DetectedTable> Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();

        var lines = text.Split('\n', StringSplitOptions.None);
        var tables = new List<DetectedTable>();

        int i = 0;
        while (i < lines.Length)
        {
            var table = TryDetectTable(lines, ref i);
            if (table != null)
                tables.Add(table);
            else
                i++;
        }

        return tables;
    }

    /// <summary>
    /// Attempts to detect a table starting at the given line index.
    /// Advances the index past the detected table or by 1 if no table found.
    /// </summary>
    private static DetectedTable? TryDetectTable(string[] lines, ref int startIndex)
    {
        int i = startIndex;

        // Skip empty lines
        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
            i++;

        if (i >= lines.Length) { startIndex = i; return null; }

        // Determine separator for the first line
        var separator = DetermineSeparator(lines[i]);
        if (separator == null) { startIndex = i + 1; return null; }

        var rows = new List<string[]>();
        int startLine = i;

        // Collect consecutive rows that match the separator pattern
        while (i < lines.Length)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) break;

            var currentSep = DetermineSeparator(line);
            if (currentSep == null) break;

            var cells = SplitRow(line, currentSep);
            // Accept rows with at least MinColumns, or rows that are consistent
            // with the table's column count (allow +/-1 for headers/alignment)
            if (rows.Count > 0 && cells.Length < MinColumns
                && Math.Abs(cells.Length - rows[0].Length) > 1)
                break;

            rows.Add(cells);
            i++;
        }

        startIndex = i;

        // Validate: need at least MinRows and MinColumns
        if (rows.Count < MinRows) return null;
        if (rows.Any(r => r.Length < MinColumns) && rows.Count < 3) return null;

        // Normalize: pad short rows to match the widest row
        int maxCols = rows.Max(r => r.Length);
        var normalizedRows = rows.Select(r =>
        {
            if (r.Length < maxCols)
                return r.Concat(Enumerable.Repeat("", maxCols - r.Length)).ToArray();
            return r;
        }).ToArray();

        return new DetectedTable(normalizedRows, startLine, startLine + rows.Count - 1);
    }

    /// <summary>
    /// Determines which separator pattern a line uses (pipe, tab, or multi-space).
    /// Returns null if the line doesn't look like a table row.
    /// </summary>
    private static Regex? DetermineSeparator(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        // Check pipe separator (e.g., "Name | Alter | Ort")
        if (PipeSeparator.Split(line).Length >= MinColumns)
            return PipeSeparator;

        // Check tab separator
        if (TabSeparator.Split(line).Length >= MinColumns)
            return TabSeparator;

        // Check multi-space separator (e.g., "Name    Alter    Ort")
        if (MultiSpaceSeparator.Split(line).Length >= MinColumns)
            return MultiSpaceSeparator;

        return null;
    }

    /// <summary>
    /// Splits a row into cells using the given separator regex.
    /// </summary>
    private static string[] SplitRow(string line, Regex separator)
    {
        return separator.Split(line.Trim())
            .Select(c => c.Trim())
            .Where(c => c.Length > 0 || separator == PipeSeparator) // Keep empty pipe cells
            .ToArray();
    }

    /// <summary>
    /// Formats a detected table as tab-separated values for clipboard copy.
    /// </summary>
    public static string FormatAsTsv(DetectedTable table)
    {
        return string.Join("\n", table.Cells.Select(row => string.Join("\t", row)));
    }

    /// <summary>
    /// Formats a detected table as CSV for clipboard copy.
    /// </summary>
    public static string FormatAsCsv(DetectedTable table)
    {
        return string.Join("\n", table.Cells.Select(row =>
            string.Join(",", row.Select(c => CsvEscape(c)))));
    }

    private static string CsvEscape(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

    /// <summary>
    /// Formats a detected table as a Markdown table.
    /// </summary>
    public static string FormatAsMarkdown(DetectedTable table)
    {
        if (table.Cells.Length == 0) return "";

        var sb = new System.Text.StringBuilder();

        // Header row
        sb.AppendLine("| " + string.Join(" | ", table.Cells[0]) + " |");

        // Separator row
        sb.AppendLine("| " + string.Join(" | ", table.Cells[0].Select(_ => "---")) + " |");

        // Data rows
        for (int r = 1; r < table.Cells.Length; r++)
            sb.AppendLine("| " + string.Join(" | ", table.Cells[r]) + " |");

        return sb.ToString().TrimEnd();
    }
}