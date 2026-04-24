// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.IO;
using System.Text.Json;

namespace FlipsiInk;

/// <summary>
/// Application configuration persisted as JSON.
/// </summary>
public class Config
{
    private static readonly string AppName = "FlipsiInk";
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    public string Language { get; set; } = "de";
    public string Theme { get; set; } = "system";
    public string ModelPath { get; set; } = "";
    public bool AutoUpdate { get; set; } = true;
    public string UpdateChannel { get; set; } = "stable";
    public double DefaultPenSize { get; set; } = 2;
    public string DefaultPenColor { get; set; } = "Black";
    public bool AutoRecognize { get; set; } = false;
    public bool AutoCalcEnabled { get; set; } = true;
    public string ToolbarLayout { get; set; } = "modern"; // "classic" = vertical left, "modern" = horizontal top
    public string Setting_NotebookViewMode { get; set; } = "grid"; // "grid" or "list"

    private string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static Config Load()
    {
        Directory.CreateDirectory(ConfigDir);
        var path = Path.Combine(ConfigDir, "config.json");

        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Config>(json) ?? new Config();
        }
        return new Config();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static string GetConfigDir() => ConfigDir;
}