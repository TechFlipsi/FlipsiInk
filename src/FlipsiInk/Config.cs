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
/// v0.4.0: Comprehensive settings for appearance, pen, notes, AI, storage.
/// </summary>
public class Config
{
    private static readonly string AppName = "FlipsiInk";
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    // ─── Erscheinungsbild (Appearance) ───────────────────────────────
    public string Language { get; set; } = "de";
    public string Theme { get; set; } = "system";
    public string AccentColor { get; set; } = "#0078D7";
    public string CanvasBgColor { get; set; } = "#FFFFFF";
    public string ToolbarPosition { get; set; } = "top";    // top/bottom/left/right
    public double ToolbarOpacity { get; set; } = 0.85;       // 0.0-1.0
    public bool AnimationsEnabled { get; set; } = true;

    // ─── Stift & Eingabe (Pen & Input) ──────────────────────────────
    public string ModelPath { get; set; } = "";
    public double DefaultPenSize { get; set; } = 2.5;
    public string DefaultPenColor { get; set; } = "Black";
    public string DefaultTool { get; set; } = "pen";         // pen/highlighter/eraser
    public bool PressureSensitivity { get; set; } = true;
    public bool PalmRejection { get; set; } = true;
    public string InputMode { get; set; } = "both";           // pen/touch/both

    // ─── Notizen (Notes) ────────────────────────────────────────────
    public bool AutoUpdate { get; set; } = true;
    public string UpdateChannel { get; set; } = "stable";
    public string ToolbarLayout { get; set; } = "floating";
    public string Setting_NotebookViewMode { get; set; } = "grid";
    public string Setting_NotebookSortOrder { get; set; } = "name";
    public int DefaultTemplateIndex { get; set; } = 1;       // LinedWide
    public double TemplateLineOpacity { get; set; } = 1.0;    // 0.0-1.0
    public int AutoSaveIntervalMinutes { get; set; } = 0;    // 0 = disabled
    public bool AutoTitleEnabled { get; set; } = true;
    public bool SkipEmptyNotes { get; set; } = true;
    public bool ShapeRecognition { get; set; } = true;
    public double PdfImportDpi { get; set; } = 150;           // 72/150/300

    // ─── KI-Modelle (AI Models) ────────────────────────────────────
    public bool AutoModelUpdate { get; set; } = true;
    public string ModelCatalogUrl { get; set; } = "https://github.com/TechFlipsi/FlipsiInk/releases/download/models/catalog.json";

    // ─── Legacy/compat ──────────────────────────────────────────────
    public bool AutoRecognize { get; set; } = false;
    public bool AutoCalcEnabled { get; set; } = true;
    public double CanvasOpacity { get; set; } = 1.0;
    public string StartupBehavior { get; set; } = "blank";
    public bool RightPanelVisible { get; set; } = false;

    // ─── Datenspeicherung (Storage) ─────────────────────────────────
    public string NotesFolderPath { get; set; } = "";
    public string ModelsFolderPath { get; set; } = "";
    public string ExportFolderPath { get; set; } = "";
    public int MaxBackupsPerNote { get; set; } = 5;

    // ─── Keyboard Shortcuts ─────────────────────────────────────────
    // (stored as simple key binding strings, used by MainWindow)

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

    // ─── Helper: Resolved paths ──────────────────────────────────────

    public string GetNotesFolder()
    {
        return !string.IsNullOrWhiteSpace(NotesFolderPath) && Directory.Exists(NotesFolderPath)
            ? NotesFolderPath
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FlipsiInk");
    }

    public string GetModelsFolder()
    {
        return !string.IsNullOrWhiteSpace(ModelsFolderPath) && Directory.Exists(ModelsFolderPath)
            ? ModelsFolderPath
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlipsiInk", "Models");
    }

    public string GetExportFolder()
    {
        return !string.IsNullOrWhiteSpace(ExportFolderPath)
            ? ExportFolderPath
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FlipsiInk", "Export");
    }
}