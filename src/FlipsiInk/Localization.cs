// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FlipsiInk;

/// <summary>
/// Simple localization system – loads JSON language files from Lang/ directory.
/// </summary>
public static class Localization
{
    private static Dictionary<string, string>? _strings;
    private static string _lang = "de";

    public static string CurrentLang => _lang;

    /// <summary>
    /// Initialize localization with the given language code.
    /// Falls back to "de" if the requested language is not available.
    /// </summary>
    public static void Init(string lang)
    {
        _lang = lang;
        LoadLanguage(lang);
    }

    /// <summary>
    /// Get a localized string by key. Returns the key itself if not found.
    /// Supports format args: Get("status_recognized", "42") → "✓ 42 Zeichen erkannt"
    /// </summary>
    public static string Get(string key, params object[] args)
    {
        if (_strings == null) Init("de");

        if (_strings != null && _strings.TryGetValue(key, out var value))
        {
            return args.Length > 0 ? string.Format(value, args) : value;
        }
        return key;
    }

    private static void LoadLanguage(string lang)
    {
        var langDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang");
        var langFile = Path.Combine(langDir, $"{lang}.json");

        // Fallback chain: requested → de → en
        if (!File.Exists(langFile))
        {
            langFile = Path.Combine(langDir, "de.json");
            if (!File.Exists(langFile))
            {
                langFile = Path.Combine(langDir, "en.json");
            }
        }

        if (File.Exists(langFile))
        {
            var json = File.ReadAllText(langFile);
            _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        else
        {
            _strings = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Get all available language codes from the Lang directory.
    /// </summary>
    public static List<string> GetAvailableLanguages()
    {
        var langDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang");
        var langs = new List<string>();
        if (Directory.Exists(langDir))
        {
            foreach (var file in Directory.GetFiles(langDir, "*.json"))
            {
                langs.Add(Path.GetFileNameWithoutExtension(file));
            }
        }
        return langs;
    }
}