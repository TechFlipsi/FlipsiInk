// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FlipsiInk;

/// <summary>
/// Detects date and time patterns in OCR text.
/// Supports German (24.12.2026, 24.12.26, 24.12.) and
/// English (Dec 24, 2026, December 24 2026) formats.
/// Issue #36: Smart Snapshot OCR-Erweiterung (Termine).
/// </summary>
public static class DateDetector
{
    /// <summary>
    /// Represents a detected date/time with context.
    /// </summary>
    public record DetectedDate(
        DateTime? DateTime,
        string RawText,
        bool HasTime,
        TimeSpan? Time,
        int StartIndex,
        int Length);

    // German date formats: 24.12.2026, 24.12.26, 24.12., 24.12
    private static readonly Regex GermanDatePattern = new(
        @"\b(\d{1,2})\.(\d{1,2})\.(?:\s*)?(\d{2,4})?\b|\b(\d{1,2})\.(\d{1,2})\b",
        RegexOptions.Compiled);

    // English date formats: Dec 24, 2026 / December 24, 2026 / Dec 24 2026
    private static readonly Regex EnglishDatePattern = new(
        @"\b(Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+(\d{1,2})(?:\s*,?\s*)(\d{4})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Time patterns: 14:30, 14:30 Uhr, 2:30 PM, 2:30PM
    private static readonly Regex TimePattern = new(
        @"\b(\d{1,2}):(\d{2})(?:\s*(Uhr|h|))\b|\b(\d{1,2}):(\d{2})\s*(AM|PM|am|pm)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Relative date keywords (German)
    private static readonly Regex GermanRelativePattern = new(
        @"\b(morgen|übermorgen|heute|nächste\s+Woche|nächster\s+Montag|nächster\s+Dienstag|nächster\s+Mittwoch|nächster\s+Donnerstag|nächster\s+Freitag|nächster\s+Samstag|nächster\s+Sonntag)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, int> EnglishMonthMap = new()
    {
        {"jan", 1}, {"january", 1}, {"feb", 2}, {"february", 2},
        {"mar", 3}, {"march", 3}, {"apr", 4}, {"april", 4},
        {"may", 5}, {"jun", 6}, {"june", 6}, {"jul", 7}, {"july", 7},
        {"aug", 8}, {"august", 8}, {"sep", 9}, {"september", 9},
        {"oct", 10}, {"october", 10}, {"nov", 11}, {"november", 11},
        {"dec", 12}, {"december", 12}
    };

    // German weekday names mapped to DayOfWeek
    private static readonly Dictionary<string, DayOfWeek> GermanWeekdayMap = new()
    {
        {"montag", DayOfWeek.Monday}, {"dienstag", DayOfWeek.Tuesday},
        {"mittwoch", DayOfWeek.Wednesday}, {"donnerstag", DayOfWeek.Thursday},
        {"freitag", DayOfWeek.Friday}, {"samstag", DayOfWeek.Saturday},
        {"sonntag", DayOfWeek.Sunday}
    };

    /// <summary>
    /// Detects all dates and times in the given text.
    /// </summary>
    public static List<DetectedDate> Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();

        var results = new List<DetectedDate>();

        // Detect German dates
        foreach (Match m in GermanDatePattern.Matches(text))
        {
            var date = ParseGermanDate(m);
            if (date != null)
            {
                // Look for an associated time near this date
                var nearbyTime = FindNearbyTime(text, m.Index, m.Length);
                results.Add(new DetectedDate(
                    date, m.Value.TrimEnd('.'), nearbyTime != null, nearbyTime,
                    m.Index, m.Value.TrimEnd('.').Length));
            }
        }

        // Detect English dates
        foreach (Match m in EnglishDatePattern.Matches(text))
        {
            var date = ParseEnglishDate(m);
            if (date != null)
            {
                var nearbyTime = FindNearbyTime(text, m.Index, m.Length);
                results.Add(new DetectedDate(
                    date, m.Value, nearbyTime != null, nearbyTime,
                    m.Index, m.Length));
            }
        }

        // Detect relative German dates
        foreach (Match m in GermanRelativePattern.Matches(text))
        {
            var date = ParseRelativeDate(m.Value.ToLowerInvariant());
            if (date != null)
            {
                var nearbyTime = FindNearbyTime(text, m.Index, m.Length);
                results.Add(new DetectedDate(
                    date, m.Value, nearbyTime != null, nearbyTime,
                    m.Index, m.Length));
            }
        }

        return results.OrderBy(d => d.StartIndex).ToList();
    }

    private static DateTime? ParseGermanDate(Match m)
    {
        // Groups: 1=day 2=month (3=year) | 4=day 5=month (no year)
        int day, month, year;

        if (m.Groups[1].Success)
        {
            day = int.Parse(m.Groups[1].Value);
            month = int.Parse(m.Groups[2].Value);
            year = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : DateTime.Now.Year;
        }
        else if (m.Groups[4].Success)
        {
            day = int.Parse(m.Groups[4].Value);
            month = int.Parse(m.Groups[5].Value);
            year = DateTime.Now.Year;
        }
        else return null;

        // Two-digit year
        if (year < 100) year += 2000;

        if (month < 1 || month > 12 || day < 1 || day > 31) return null;

        try { return new DateTime(year, month, day); }
        catch { return null; }
    }

    private static DateTime? ParseEnglishDate(Match m)
    {
        var monthStr = m.Groups[1].Value.ToLowerInvariant();
        if (!EnglishMonthMap.TryGetValue(monthStr, out int month)) return null;

        int day = int.Parse(m.Groups[2].Value);
        int year = int.Parse(m.Groups[3].Value);

        if (day < 1 || day > 31) return null;

        try { return new DateTime(year, month, day); }
        catch { return null; }
    }

    private static DateTime? ParseRelativeDate(string relativeText)
    {
        var today = DateTime.Today;

        if (relativeText == "heute") return today;
        if (relativeText == "morgen") return today.AddDays(1);
        if (relativeText == "übermorgen") return today.AddDays(2);

        // "nächster <wochentag>"
        foreach (var kvp in GermanWeekdayMap)
        {
            if (relativeText.Contains(kvp.Key))
                return NextOccurrenceOfDay(today, kvp.Value);
        }

        if (relativeText.Contains("nächste woche")) return today.AddDays(7);

        return null;
    }

    private static DateTime NextOccurrenceOfDay(DateTime from, DayOfWeek day)
    {
        int diff = ((int)day - (int)from.DayOfWeek + 7) % 7;
        if (diff == 0) diff = 7; // Next week if today
        return from.AddDays(diff);
    }

    /// <summary>
    /// Finds a time pattern near the date in the same line or nearby.
    /// </summary>
    private static TimeSpan? FindNearbyTime(string text, int dateIndex, int dateLength)
    {
        // Search within 30 chars after the date
        int searchStart = dateIndex;
        int searchEnd = Math.Min(text.Length, dateIndex + dateLength + 30);
        string nearby = text[searchStart..Math.Min(searchEnd, text.Length)];

        var timeMatch = TimePattern.Match(nearby);
        if (!timeMatch.Success)
        {
            // Also search 30 chars before the date
            int beforeStart = Math.Max(0, dateIndex - 30);
            string before = text[beforeStart..dateIndex];
            timeMatch = TimePattern.Match(before);
        }

        if (!timeMatch.Success) return null;

        int hours, minutes;
        bool isPm = false;

        if (timeMatch.Groups[4].Success) // AM/PM format
        {
            hours = int.Parse(timeMatch.Groups[3].Value);
            minutes = int.Parse(timeMatch.Groups[4].Value);
            isPm = timeMatch.Groups[5].Value.ToUpperInvariant() == "PM";
        }
        else // 24h format
        {
            hours = int.Parse(timeMatch.Groups[1].Value);
            minutes = int.Parse(timeMatch.Groups[2].Value);
        }

        if (isPm && hours < 12) hours += 12;
        if (!isPm && hours == 12) hours = 0;

        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) return null;

        return new TimeSpan(hours, minutes, 0);
    }

    /// <summary>
    /// Generates an ICS calendar entry for the detected date.
    /// </summary>
    public static string GenerateIcs(DetectedDate dateEvent, string title = "Termin", string? description = null)
    {
        var dt = dateEvent.DateTime ?? DateTime.Today;
        var start = dateEvent.HasTime && dateEvent.Time != null
            ? dt.Add(dateEvent.Time.Value)
            : dt;
        var end = start.AddHours(1);

        string dtFormat = "yyyyMMddTHHmmss";
        string dtStart = start.ToString(dtFormat);
        string dtEnd = end.ToString(dtFormat);
        string uid = Guid.NewGuid().ToString("D");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//FlipsiInk//Smart Snapshot//DE");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{uid}");
        sb.AppendLine($"DTSTART:{dtStart}");
        sb.AppendLine($"DTEND:{dtEnd}");
        sb.AppendLine($"SUMMARY:{EscapeIcs(title)}");
        if (!string.IsNullOrEmpty(description))
            sb.AppendLine($"DESCRIPTION:{EscapeIcs(description)}");
        sb.AppendLine($"LOCATION:");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string EscapeIcs(string text)
    {
        return text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
                   .Replace("\n", "\\n").Replace("\r", "");
    }
}