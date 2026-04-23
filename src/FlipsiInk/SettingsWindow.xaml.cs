// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace FlipsiInk;

/// <summary>
/// Einstellungs-Window für FlipsiInk.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly Config _config;

    public SettingsWindow()
    {
        InitializeComponent();
        _config = App.Config;

        // PageTemplateType-Werte in ComboBox laden
        TemplateCombo.ItemsSource = Enum.GetValues(typeof(PageTemplateType))
            .Cast<PageTemplateType>()
            .Select(t => t.ToString())
            .ToList();

        LoadValues();
        UpdateModelStatus();
    }

    /// <summary>
    /// Lädt die aktuellen Config-Werte in die UI-Controls.
    /// </summary>
    private void LoadValues()
    {
        ModelPathBox.Text = _config.ModelPath;

        // Sprache
        SelectComboByTag(LanguageCombo, _config.Language);

        // Theme
        SelectComboByTag(ThemeCombo, _config.Theme);

        // Auto-Update
        AutoUpdateCheck.IsChecked = _config.AutoUpdate;

        // Update-Kanal
        SelectComboByTag(UpdateChannelCombo, _config.UpdateChannel);

        // Stiftgröße
        SelectComboByTag(PenSizeCombo, _config.DefaultPenSize.ToString());

        // Stiftfarbe
        SelectComboByTag(PenColorCombo, _config.DefaultPenColor);

        // Auto-Erkennen
        AutoRecognizeCheck.IsChecked = _config.AutoRecognize;

        // Seitenformat – Standard A4
        SelectComboByTag(PageSizeCombo, "A4");

        // Zeilenabstand – Standard 25
        SelectComboByTag(LineSpacingCombo, "25");
    }

    /// <summary>
    /// Wählt das ComboBox-Item dessen Tag mit dem Wert übereinstimmt.
    /// </summary>
    private static void SelectComboByTag(System.Windows.Controls.ComboBox combo, string tagValue)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is System.Windows.Controls.ComboBoxItem item &&
                item.Tag?.ToString() == tagValue)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        // Fallback: erstes Element
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    /// <summary>
    /// Liest den Tag-Wert des ausgewählten ComboBoxItems.
    /// </summary>
    private static string GetSelectedTag(System.Windows.Controls.ComboBox combo)
    {
        if (combo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            return item.Tag?.ToString() ?? "";
        return combo.SelectedItem?.ToString() ?? "";
    }

    /// <summary>
    /// Aktualisiert das Modell-Status-Label.
    /// </summary>
    private void UpdateModelStatus()
    {
        var path = ModelPathBox.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            ModelStatusLabel.Content = "⚠️ Kein Modell-Pfad konfiguriert";
            return;
        }

        if (System.IO.File.Exists(path))
        {
            var size = new System.IO.FileInfo(path).Length;
            ModelStatusLabel.Content = $"✅ Geladen ({FormatSize(size)})";
        }
        else
        {
            ModelStatusLabel.Content = "❌ Modell nicht gefunden";
        }
    }

    /// <summary>
    /// Formatiert eine Dateigröße für die Anzeige.
    /// </summary>
    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.#} {units[unit]}";
    }

    /// <summary>
    /// Browse-Button für Modell-Pfad.
    /// </summary>
    private void BrowseModel_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "ONNX-Modell|*.onnx|Alle Dateien|*.*",
            Title = "Modell-Datei auswählen"
        };
        if (dlg.ShowDialog() == true)
        {
            ModelPathBox.Text = dlg.FileName;
            UpdateModelStatus();
        }
    }

    /// <summary>
    /// Öffnet den Modell-Download-Dialog.
    /// </summary>
    private void DownloadModel_Click(object sender, RoutedEventArgs e)
    {
        var downloader = new ModelDownloader();
        var dlg = new ModelDownloadDialog(downloader)
        {
            Owner = this
        };
        dlg.ShowDialog();
        UpdateModelStatus();
    }

    /// <summary>
    /// Speichert alle Einstellungen in die Config.
    /// </summary>
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _config.ModelPath = ModelPathBox.Text.Trim();
        _config.Language = GetSelectedTag(LanguageCombo);
        _config.Theme = GetSelectedTag(ThemeCombo);
        _config.AutoUpdate = AutoUpdateCheck.IsChecked == true;
        _config.UpdateChannel = GetSelectedTag(UpdateChannelCombo);

        if (double.TryParse(GetSelectedTag(PenSizeCombo), out var penSize))
            _config.DefaultPenSize = penSize;

        _config.DefaultPenColor = GetSelectedTag(PenColorCombo);
        _config.AutoRecognize = AutoRecognizeCheck.IsChecked == true;

        _config.Save();

        // Lokalisierung neu initialisieren
        Localization.Init(_config.Language);

        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Bricht ohne Speichern ab.
    /// </summary>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Zeigt den Über-Dialog.
    /// </summary>
    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            $"FlipsiInk v{App.Version}\n\n" +
            "AI-powered Handwriting & Math Notes App\n" +
            "Copyright © 2026 Fabian Kirchweger\n\n" +
            "Lizenziert unter GPL v3.",
            "Über FlipsiInk",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

/// <summary>
/// Einfacher Dialog für Modell-Downloads (Platzhalter).
/// </summary>
public class ModelDownloadDialog : Window
{
    public ModelDownloadDialog(ModelDownloader downloader)
    {
        Title = "Modell herunterladen";
        Width = 450;
        Height = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };

        var label = new System.Windows.Controls.TextBlock
        {
            Text = "Verfügbare Modelle:",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        panel.Children.Add(label);

        foreach (var model in downloader.GetAvailableModels())
        {
            var btn = new System.Windows.Controls.Button
            {
                Content = $"📥 {model.Name} ({model.Size})",
                Tag = model,
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(8, 4, 8, 4)
            };
            btn.Click += OnModelSelect;
            panel.Children.Add(btn);
        }

        var closeBtn = new System.Windows.Controls.Button
        {
            Content = "Schließen",
            Width = 90,
            Margin = new Thickness(0, 12, 0, 0),
            IsCancel = true
        };
        panel.Children.Add(closeBtn);

        Content = panel;
    }

    private void OnModelSelect(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is ModelInfo model)
        {
            MessageBox.Show(
                $"Modell: {model.Name}\nBeschreibung: {model.Description}\nGröße: {model.Size}\n\n" +
                "Download-URLs sind noch Platzhalter (TODO: echte URLs wenn Modelle verfügbar).",
                model.Name,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}