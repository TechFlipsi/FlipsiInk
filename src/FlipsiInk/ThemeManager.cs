// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace FlipsiInk;

/// <summary>
/// Manages application theming (Light, Dark, System).
/// </summary>
public enum Theme
{
    System,
    Light,
    Dark
}

public class ThemeManager
{
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    private Theme _currentTheme = Theme.System;

    public Theme CurrentTheme => _currentTheme;

    /// <summary>
    /// Applies the given theme to the specified root element and all descendant controls.
    /// </summary>
    public void ApplyTheme(FrameworkElement root, Theme theme)
    {
        _currentTheme = theme;
        var effectiveTheme = theme == Theme.System ? GetSystemTheme() : theme;
        var colors = GetCurrentColors(theme);

        if (root is Control rootControl)
            rootControl.Background = new SolidColorBrush(colors.Background);
        else if (root is Panel rootPanel)
            rootPanel.Background = new SolidColorBrush(colors.Background);
        ApplyThemeRecursive(root, colors);
        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(theme, effectiveTheme, colors));
    }

    private void ApplyThemeRecursive(DependencyObject parent, ThemeColors colors)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Control control)
            {
                control.Foreground = new SolidColorBrush(colors.Foreground);
                // Don't override InkCanvas background – it must follow template/theme
                if (control is not InkCanvas && control is not TextBox)
                {
                    control.Background = new SolidColorBrush(colors.PanelBg);
                }
                control.BorderBrush = new SolidColorBrush(colors.Border);
            }
            ApplyThemeRecursive(child, colors);
        }
    }

    /// <summary>
    /// Detects the current Windows system theme (Light or Dark) from the registry.
    /// </summary>
    public static Theme GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 0 ? Theme.Dark : Theme.Light;
            }
        }
        catch
        {
            // Registry not available or key missing — default to Light
        }
        return Theme.Light;
    }

    /// <summary>
    /// Returns the ThemeColors for the given theme (resolves System to actual theme).
    /// </summary>
    public static ThemeColors GetCurrentColors(Theme theme)
    {
        var effective = theme == Theme.System ? GetSystemTheme() : theme;
        return effective == Theme.Dark ? ThemeColors.Dark : ThemeColors.Light;
    }
}

public class ThemeChangedEventArgs : EventArgs
{
    public Theme RequestedTheme { get; }
    public Theme EffectiveTheme { get; }
    public ThemeColors Colors { get; }
    public ThemeChangedEventArgs(Theme requested, Theme effective, ThemeColors colors)
    {
        RequestedTheme = requested;
        EffectiveTheme = effective;
        Colors = colors;
    }
}

/// <summary>
/// Holds all themed colors for a specific theme.
/// </summary>
public class ThemeColors
{
    public Color Background { get; init; }
    public Color PanelBg { get; init; }
    public Color TopBarBg { get; init; }
    public Color Foreground { get; init; }
    public Color Border { get; init; }
    public Color Accent { get; init; }
    public Color CanvasBg { get; init; }

    public static ThemeColors Dark => new()
    {
        Background  = (Color)ColorConverter.ConvertFromString("#1E1E1E"),
        PanelBg     = (Color)ColorConverter.ConvertFromString("#252525"),
        TopBarBg    = (Color)ColorConverter.ConvertFromString("#2D2D2D"),
        Foreground  = (Color)ColorConverter.ConvertFromString("#FFFFFF"),
        Border      = (Color)ColorConverter.ConvertFromString("#444444"),
        Accent      = (Color)ColorConverter.ConvertFromString("#0078D7"),
        CanvasBg    = (Color)ColorConverter.ConvertFromString("#FFFFFF"),
    };

    public static ThemeColors Light => new()
    {
        Background  = (Color)ColorConverter.ConvertFromString("#F5F5F5"),
        PanelBg     = (Color)ColorConverter.ConvertFromString("#E8E8E8"),
        TopBarBg    = (Color)ColorConverter.ConvertFromString("#DCDCDC"),
        Foreground  = (Color)ColorConverter.ConvertFromString("#1E1E1E"),
        Border      = (Color)ColorConverter.ConvertFromString("#CCCCCC"),
        Accent      = (Color)ColorConverter.ConvertFromString("#0078D7"),
        CanvasBg    = (Color)ColorConverter.ConvertFromString("#FFFFFF"),
    };
}