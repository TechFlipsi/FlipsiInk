// InkNote - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System.Windows;

namespace InkNote;

public partial class App : Application
{
    public static string Version => "0.1.0";
    public static Config Config { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Config = Config.Load();
        var window = new MainWindow();
        window.Show();
    }
}