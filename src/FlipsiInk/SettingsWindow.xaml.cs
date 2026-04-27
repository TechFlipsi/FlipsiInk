// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlipsiInk;

public partial class SettingsWindow : Window
{
    private static readonly string[] ColorNames = new[]
    {
        "Black", "DarkBlue", "Blue", "Red", "Green",
        "Orange", "Purple", "Brown", "Gray", "Teal"
    };

    private static readonly string[] AccentColors = new[]
    {
        "#0078D7", "#7B1FA2", "#00897B", "#E65100", "#C62828", "#2E7D32"
    };

    // Canvas background theme names mapped to hex colors
    private static readonly string[] CanvasBgPresets = new[]
    {
        "#FFFFFF", "#FFF8E1", "#F5E6CC", "#F5F5F5"
    };

    public SettingsWindow()
    {
        InitializeComponent();

        if (Application.Current.MainWindow != null && Application.Current.MainWindow != this)
            Owner = Application.Current.MainWindow;

        Loaded += (s, e) => ApplyThemeToWindow();

        LoadSettings();
    }

    private void ApplyThemeToWindow()
    {
        try
        {
            var theme = App.Config.Theme switch
            {
                "light" => Theme.Light,
                "dark" => Theme.Dark,
                _ => ThemeManager.GetSystemTheme()
            };
            var colors = ThemeManager.GetCurrentColors(theme);
            bool isDark = colors.Foreground == System.Windows.Media.Colors.White;

            if (!isDark)
            {
                // Light theme overrides for the settings window
                Background = new SolidColorBrush(colors.Background);
                foreach (var expander in FindVisualChildren<Expander>(this))
                {
                    expander.Background = new SolidColorBrush(colors.PanelBg);
                    expander.Foreground = new SolidColorBrush(colors.Foreground);
                }
                foreach (var tb in FindVisualChildren<TextBlock>(this))
                    tb.Foreground = new SolidColorBrush(colors.Foreground);
                foreach (var cb in FindVisualChildren<CheckBox>(this))
                    cb.Foreground = new SolidColorBrush(colors.Foreground);
                foreach (var rb in FindVisualChildren<RadioButton>(this))
                    rb.Foreground = new SolidColorBrush(colors.Foreground);
                foreach (var combo in FindVisualChildren<ComboBox>(this))
                {
                    combo.Background = new SolidColorBrush(colors.PanelBg);
                    combo.Foreground = new SolidColorBrush(colors.Foreground);
                }
                foreach (var tb in FindVisualChildren<TextBox>(this))
                {
                    tb.Background = new SolidColorBrush(colors.PanelBg);
                    tb.Foreground = new SolidColorBrush(colors.Foreground);
                    tb.BorderBrush = new SolidColorBrush(colors.Border);
                }
                foreach (var slider in FindVisualChildren<Slider>(this))
                    slider.Foreground = new SolidColorBrush(colors.Foreground);
            }
        }
        catch { /* dark theme is default, fine */ }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) yield return typed;
            foreach (var grandChild in FindVisualChildren<T>(child))
                yield return grandChild;
        }
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

        // Accent color
        AccentColorCombo.SelectedIndex = Array.IndexOf(AccentColors, config.AccentColor ?? "#0078D7");
        if (AccentColorCombo.SelectedIndex < 0) AccentColorCombo.SelectedIndex = 0;

        // Canvas background - match hex color to preset index
        CanvasBgCombo.SelectedIndex = Array.IndexOf(CanvasBgPresets, config.CanvasBgColor ?? "#FFFFFF");
        if (CanvasBgCombo.SelectedIndex < 0) CanvasBgCombo.SelectedIndex = 0;

        // Toolbar position
        ToolbarPositionCombo.SelectedIndex = config.ToolbarPosition switch
        {
            "bottom" => 1,
            "left" => 2,
            "right" => 3,
            _ => 0
        };

        // Toolbar opacity
        ToolbarOpacitySlider.Value = config.ToolbarOpacity * 100;

        // Animations
        AnimationsCheck.IsChecked = config.AnimationsEnabled;

        // Pen color
        var colorIdx = Array.IndexOf(ColorNames, config.DefaultPenColor);
        PenColorCombo.SelectedIndex = colorIdx >= 0 ? colorIdx : 0;

        // Pen size
        PenThin.IsChecked = config.DefaultPenSize <= 1;
        PenMedium.IsChecked = config.DefaultPenSize > 1 && config.DefaultPenSize <= 3;
        PenThick.IsChecked = config.DefaultPenSize > 3;

        // Default tool
        DefaultToolCombo.SelectedIndex = config.DefaultTool switch
        {
            "highlighter" => 1,
            "eraser" => 2,
            "lasso" => 3,
            _ => 0
        };

        // Pressure
        PressureCheck.IsChecked = config.PressureSensitivity;

        // Palm rejection
        PalmRejectionCheck.IsChecked = config.PalmRejection;

        // Input mode
        InputModeCombo.SelectedIndex = config.InputMode switch
        {
            "pen" => 1,
            "touch" => 2,
            _ => 0
        };

        // Template
        TemplateCombo.SelectedIndex = Math.Clamp(config.DefaultTemplateIndex, 0, 10);

        // Canvas opacity (slider 0-100)
        CanvasOpacitySlider.Value = config.CanvasOpacity * 100;

        // Auto-save interval
        AutoSaveCombo.SelectedIndex = config.AutoSaveIntervalMinutes switch
        {
            0 => 0,
            1 => 1,
            5 => 2,
            10 => 3,
            30 => 4,
            _ => 2
        };

        // Auto-title
        AutoTitleCheck.IsChecked = config.AutoTitleEnabled;

        // Empty notes
        SkipEmptyNotesCheck.IsChecked = config.SkipEmptyNotes;

        // Shape recognition
        ShapeRecognitionCheck.IsChecked = config.ShapeRecognition;

        // PDF resolution
        PdfResolutionCombo.SelectedIndex = (int)config.PdfImportDpi switch
        {
            72 => 0,
            300 => 2,
            _ => 1
        };

        // Active model
        ActiveModelLabel.Text = !string.IsNullOrEmpty(config.ModelPath)
            ? System.IO.Path.GetFileNameWithoutExtension(config.ModelPath)
            : "(nicht geladen)";

        // Auto model update
        AutoModelUpdateCheck.IsChecked = config.AutoModelUpdate;

        // Language
        LanguageCombo.SelectedIndex = config.Language == "en" ? 1 : 0;

        // Storage paths
        NotesFolderBox.Text = config.GetNotesFolder();
        ModelsFolderBox.Text = config.ModelsFolderPath ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlipsiInk", "Models");
        ExportFolderBox.Text = config.ExportFolderPath ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FlipsiInk", "Export");

        // Max backups
        MaxBackupsCombo.SelectedIndex = config.MaxBackupsPerNote switch
        {
            0 => 0,
            1 => 1,
            3 => 2,
            5 => 3,
            10 => 4,
            _ => 1
        };

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

        // About
        AboutVersion.Text = $"FlipsiInk v{App.Version}";
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

        // Accent color
        config.AccentColor = AccentColors[AccentColorCombo.SelectedIndex];

        // Canvas background - save as hex color
        config.CanvasBgColor = CanvasBgPresets[CanvasBgCombo.SelectedIndex];

        // Toolbar position
        config.ToolbarPosition = ToolbarPositionCombo.SelectedIndex switch
        {
            1 => "bottom",
            2 => "left",
            3 => "right",
            _ => "top"
        };

        // Toolbar opacity
        config.ToolbarOpacity = ToolbarOpacitySlider.Value / 100.0;

        // Animations
        config.AnimationsEnabled = AnimationsCheck.IsChecked == true;

        // Pen color
        config.DefaultPenColor = ColorNames[PenColorCombo.SelectedIndex];

        // Pen size
        config.DefaultPenSize = PenThin.IsChecked == true ? 1 :
                                PenThick.IsChecked == true ? 5 : 2.5;

        // Default tool
        config.DefaultTool = DefaultToolCombo.SelectedIndex switch
        {
            1 => "highlighter",
            2 => "eraser",
            3 => "lasso",
            _ => "pen"
        };

        // Pressure
        config.PressureSensitivity = PressureCheck.IsChecked == true;

        // Palm rejection
        config.PalmRejection = PalmRejectionCheck.IsChecked == true;

        // Input mode
        config.InputMode = InputModeCombo.SelectedIndex switch
        {
            1 => "pen",
            2 => "touch",
            _ => "both"
        };

        // Template
        config.DefaultTemplateIndex = TemplateCombo.SelectedIndex;

        // Canvas opacity
        config.CanvasOpacity = CanvasOpacitySlider.Value / 100.0;

        // Auto-save interval
        config.AutoSaveIntervalMinutes = AutoSaveCombo.SelectedIndex switch
        {
            0 => 0,
            1 => 1,
            2 => 5,
            3 => 10,
            4 => 30,
            _ => 5
        };

        // Auto-title
        config.AutoTitleEnabled = AutoTitleCheck.IsChecked == true;

        // Empty notes
        config.SkipEmptyNotes = SkipEmptyNotesCheck.IsChecked == true;

        // Shape recognition
        config.ShapeRecognition = ShapeRecognitionCheck.IsChecked == true;

        // PDF resolution
        config.PdfImportDpi = PdfResolutionCombo.SelectedIndex switch
        {
            0 => 72,
            2 => 300,
            _ => 150
        };

        // Auto model update
        config.AutoModelUpdate = AutoModelUpdateCheck.IsChecked == true;

        // Language
        config.Language = LanguageCombo.SelectedIndex == 1 ? "en" : "de";

        // Storage paths
        config.NotesFolderPath = NotesFolderBox.Text;
        config.ModelsFolderPath = ModelsFolderBox.Text;
        config.ExportFolderPath = ExportFolderBox.Text;

        // Max backups
        config.MaxBackupsPerNote = MaxBackupsCombo.SelectedIndex switch
        {
            0 => 0,
            1 => 1,
            2 => 3,
            3 => 5,
            4 => 10,
            _ => 1
        };

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

    private void OpenModelManager_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var mmw = new ModelManagerWindow(((MainWindow)Application.Current.MainWindow)?._modelManager ?? new ModelManager()) { Owner = this };
            mmw.ShowDialog();
            // Refresh active model label after closing
            ActiveModelLabel.Text = !string.IsNullOrEmpty(App.Config.ModelPath)
                ? System.IO.Path.GetFileNameWithoutExtension(App.Config.ModelPath)
                : "(nicht geladen)";
        }
        catch { }
    }

    private void BrowseNotesFolder_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolder(NotesFolderBox);
    }

    private void BrowseModelsFolder_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolder(ModelsFolderBox);
    }

    private void BrowseExportFolder_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolder(ExportFolderBox);
    }

    private void BrowseFolder(TextBox target)
    {
        try
        {
            // Use OpenFileDialog in folder-picking mode (WPF-only, no WinForms dependency)
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "Ordner auswählen";
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = true;
            dialog.FileName = "FolderSelection";
            dialog.InitialDirectory = System.IO.Directory.Exists(target.Text) ? target.Text : "";
            // Workaround: user must select any file inside the folder, then we take the directory
            dialog.Filter = "Ordner|*.folder|Alle Dateien|*.*";
            dialog.FilterIndex = 2;
            if (dialog.ShowDialog() == true)
                target.Text = System.IO.Path.GetDirectoryName(dialog.FileName) ?? dialog.FileName;
        }
        catch { }
    }

    private void GitHubLink_Click(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch { }
    }
}