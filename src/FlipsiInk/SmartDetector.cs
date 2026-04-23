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
/// Smart-Detection (Issue #30): Erkennt E-Mails, Telefonnummern und URLs im OCR-Text
/// und macht diese klickbar.
/// </summary>
public static class SmartDetector
{
    // Regex-Patterns für Erkennung
    private static readonly Regex EmailPattern = new(
        @"\b[\w.-]+@[\w.-]+\.\w+\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhonePattern = new(
        @"\b\d{3}[-/]?\d{3,}\b",
        RegexOptions.Compiled);

    private static readonly Regex UrlPattern = new(
        @"https?://\S+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Erkannte Entität mit Typ und Wert.
    /// </summary>
    public record SmartMatch(SmartMatchType Type, string Value, int Start, int Length);

    public enum SmartMatchType
    {
        Email,
        Phone,
        Url
    }

    /// <summary>
    /// Durchsucht den Text nach E-Mails, Telefonnummern und URLs.
    /// </summary>
    public static List<SmartMatch> Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();

        var matches = new List<SmartMatch>();

        // URLs zuerst (priorisieren – können E-Mail-Teile enthalten)
        foreach (Match m in UrlPattern.Matches(text))
            matches.Add(new SmartMatch(SmartMatchType.Url, m.Value, m.Index, m.Length));

        // E-Mails
        foreach (Match m in EmailPattern.Matches(text))
        {
            // Nur hinzufügen wenn nicht bereits Teil einer URL
            if (!matches.Any(x => m.Index >= x.Start && m.Index < x.Start + x.Length))
                matches.Add(new SmartMatch(SmartMatchType.Email, m.Value, m.Index, m.Length));
        }

        // Telefonnummern
        foreach (Match m in PhonePattern.Matches(text))
        {
            if (!matches.Any(x => m.Index >= x.Start && m.Index < x.Start + x.Length))
                matches.Add(new SmartMatch(SmartMatchType.Phone, m.Value, m.Index, m.Length));
        }

        return matches.OrderBy(m => m.Start).ToList();
    }

    /// <summary>
    /// Gibt die URI für einen erkannten Treffer zurück (für klickbare Links).
    /// </summary>
    public static string GetUri(SmartMatch match)
    {
        return match.Type switch
        {
            SmartMatchType.Email => $"mailto:{match.Value}",
            SmartMatchType.Phone => $"tel:{match.Value}",
            SmartMatchType.Url => match.Value,
            _ => match.Value
        };
    }

    /// <summary>
    /// Gibt ein Display-Label für den Match-Typ zurück.
    /// </summary>
    public static string GetTypeLabel(SmartMatchType type)
    {
        return type switch
        {
            SmartMatchType.Email => "📧",
            SmartMatchType.Phone => "📞",
            SmartMatchType.Url => "🔗",
            _ => "🔍"
        };
    }
}