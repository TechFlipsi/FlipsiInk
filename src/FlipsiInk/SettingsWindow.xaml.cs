// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Windows;

namespace FlipsiInk;

public partial class SettingsWindow : Window
{
    private static readonly string[] ColorNames = new[]
    {
        "Black", "DarkBlue", "Blue", "Red", "Green",
        "Orange", "Purple", "Brown", "Gray", "Teal"
    };

    public SettingsWindow()
    {
        InitializeComponent();

        if (Application.Current.MainWindow != null && Application.Current.MainWindow != this)
            Owner = Application.Current.MainWindow;
        else if (Application.Current.MainWindow != null)
            Owner = Application.Current.MainWindow;

        LoadSettings();
    }

    private void LoadSettings()
    {
        var config = App.Config;

        // Theme
        ThemeCombo.SelectedIndex = config.Theme switch
        {
            "light" => 1,
            "dark" => 2,
            _ => 0
        };

        // Template
        TemplateCombo.SelectedIndex = Math.Clamp(config.DefaultTemplateIndex, 0, 10);

        // Pen color
        var colorIdx = Array.IndexOf(ColorNames, config.DefaultPenColor);
        PenColorCombo.SelectedIndex = colorIdx >= 0 ? colorIdx : 0;

        // Pen size
        PenThin.IsChecked = config.DefaultPenSize <= 1;
        PenMedium.IsChecked = config.DefaultPenSize > 1 && config.DefaultPenSize <= 3;
        PenThick.IsChecked = config.DefaultPenSize > 3;

        // Language
        LanguageCombo.SelectedIndex = config.Language == "en" ? 1 : 0;

        // Auto-save interval
        AutoSaveInterval.Text = config.AutoSaveIntervalMinutes.ToString();

        // Canvas opacity
        CanvasOpacity.Text = config.CanvasOpacity.ToString("0.0");

        // Startup
        StartupCombo.SelectedIndex = config.StartupBehavior switch
        {
            "last" => 1,
            _ => 0
        };

        // Toolbar layout
        ToolbarLayoutCombo.SelectedIndex = config.ToolbarLayout == "classic" ? 1 : 0;

        // Auto-update
        AutoUpdateCheck.IsChecked = config.AutoUpdate;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var config = App.Config;

        // Theme
        config.Theme = ThemeCombo.SelectedIndex switch
        {
            1 => "light",
            2 => "dark",
            _ => "system"
        };

        // Template
        config.DefaultTemplateIndex = TemplateCombo.SelectedIndex;

        // Pen color
        config.DefaultPenColor = ColorNames[PenColorCombo.SelectedIndex];

        // Pen size
        config.DefaultPenSize = PenThin.IsChecked == true ? 1 :
                                PenThick.IsChecked == true ? 5 : 2.5;

        // Language
        config.Language = LanguageCombo.SelectedIndex == 1 ? "en" : "de";

        // Auto-save interval
        if (int.TryParse(AutoSaveInterval.Text, out int interval))
            config.AutoSaveIntervalMinutes = interval;

        // Canvas opacity
        if (double.TryParse(CanvasOpacity.Text, out double opacity))
            config.CanvasOpacity = Math.Clamp(opacity, 0, 1);

        // Startup
        config.StartupBehavior = StartupCombo.SelectedIndex == 1 ? "last" : "blank";

        // Toolbar
        config.ToolbarLayout = ToolbarLayoutCombo.SelectedIndex == 1 ? "classic" : "modern";

        // Auto-update
        config.AutoUpdate = AutoUpdateCheck.IsChecked == true;

        config.Save();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}