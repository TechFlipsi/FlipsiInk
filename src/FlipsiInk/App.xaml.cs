// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System.Windows;

namespace FlipsiInk;

public partial class App : Application
{
    public static string Version => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
    public static Config Config { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handler – zeigt Fehler statt silent crash
        DispatcherUnhandledException += (s, args) =>
        {
            System.Windows.MessageBox.Show($"Unerwarteter Fehler:\n\n{args.Exception}\n\nBitte an den Entwickler melden.",
                "FlipsiInk – Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                System.Windows.MessageBox.Show($"Kritischer Fehler:\n\n{ex}",
                    "FlipsiInk – Absturz", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        try
        {
            Config = Config.Load();
        }
        catch
        {
            Config = new Config();
        }

        // Beim Start nach Updates suchen (non-blocking, errors ignored)
        try { _ = new AutoUpdater().CheckForUpdatesAsync(); }
        catch { /* ignore */ }

        var window = new MainWindow();
        window.Show();
    }
}