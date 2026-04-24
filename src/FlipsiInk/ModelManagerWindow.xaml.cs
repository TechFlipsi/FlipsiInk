// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace FlipsiInk;

public partial class ModelManagerWindow : Window
{
    private readonly ModelManager _manager;
    private List<ModelViewModel> _viewModels = new();

    public ModelManagerWindow(ModelManager manager)
    {
        InitializeComponent();
        _manager = manager;
        if (Application.Current.MainWindow != null && Application.Current.MainWindow != this)
            Owner = Application.Current.MainWindow;
        ModelsDirLabel.Text = $"📁 {_manager.ModelsDirectory}";
        RefreshList();
    }

    /// <summary>Rebuild the model list from catalog + installed state.</summary>
    private void RefreshList()
    {
        _viewModels.Clear();
        var catalog = _manager.GetCatalog();
        var activeId = _manager.ActiveModelId;

        foreach (var entry in catalog)
        {
            var installed = _manager.GetInstalled(entry.Id);
            var isActive = entry.Id == activeId;

            _viewModels.Add(new ModelViewModel
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description,
                Size = entry.Size,
                Quantization = $"Quantisierung: {entry.Quantization}",
                RamRequirement = $"Min. RAM: {entry.MinRamGb} GB",
                TierLabel = entry.Tier switch
                {
                    "schwach" => "⬜ Leichtgewichtig",
                    "stark" => "🟦 Ausgewogen",
                    "bester" => "🟪 Maximale Qualität",
                    _ => entry.Tier
                },
                IsInstalled = installed != null,
                IsActive = isActive,
                InstalledVersion = installed != null ? $"v{installed.Version}" : "",
                // Visibility
                RecommendedBadge = entry.IsRecommended ? Visibility.Visible : Visibility.Collapsed,
                InstalledBadge = installed != null ? Visibility.Visible : Visibility.Collapsed,
                ActiveBadge = isActive ? Visibility.Visible : Visibility.Collapsed,
                UpdateBadge = Visibility.Collapsed, // populated async
                DownloadVisible = installed == null ? Visibility.Visible : Visibility.Collapsed,
                DeleteVisible = installed != null ? Visibility.Visible : Visibility.Collapsed,
                ActivateVisible = installed != null && !isActive ? Visibility.Visible : Visibility.Collapsed,
                UpdateVisible = Visibility.Collapsed
            });
        }

        ModelList.ItemsSource = null;
        ModelList.ItemsSource = _viewModels;

        // Async update check
        _ = CheckUpdatesAsync();
    }

    private async Task CheckUpdatesAsync()
    {
        var catalog = _manager.GetCatalog();
        for (int i = 0; i < _viewModels.Count; i++)
        {
            var vm = _viewModels[i];
            if (!vm.IsInstalled) continue;
            try
            {
                var hasUpdate = await _manager.CheckForUpdateAsync(catalog[i]);
                if (hasUpdate)
                {
                    vm.UpdateBadge = Visibility.Visible;
                    vm.UpdateVisible = Visibility.Visible;
                    ModelList.ItemsSource = null;
                    ModelList.ItemsSource = _viewModels;
                }
            }
            catch { /* ignore */ }
        }
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string id) return;
        var catalog = _manager.GetCatalog().Find(c => c.Id == id);
        if (catalog == null) return;

        SetDownloading(true);
        try
        {
            await _manager.DownloadModelAsync(catalog, new Progress<double>(p =>
            {
                DownloadProgress.Value = p * 100;
                StatusLabel.Text = $"↓ Lade {catalog.Name} herunter… {p:P0}";
            }));
            StatusLabel.Text = $"✓ {catalog.Name} heruntergeladen!";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"✕ Fehler: {ex.Message}";
            MessageBox.Show($"Download fehlgeschlagen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetDownloading(false);
            RefreshList();
        }
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        // Update = delete + re-download
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string id) return;
        _manager.DeleteModel(id);
        Download_Click(sender, e);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string id) return;
        var catalog = _manager.GetCatalog().Find(c => c.Id == id);
        var name = catalog?.Name ?? id;

        var result = MessageBox.Show(
            $"'{name}' wirklich löschen?", "Modell löschen",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        _manager.DeleteModel(id);
        StatusLabel.Text = $"✕ {name} gelöscht.";
        RefreshList();
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string id) return;
        _manager.SetActiveModel(id);
        StatusLabel.Text = "✓ Aktives Modell geändert – OCR wird beim nächsten Erkennen neu geladen.";
        RefreshList();
    }

    private void SetDownloading(bool downloading)
    {
        DownloadProgress.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;
        if (!downloading) DownloadProgress.Value = 0;
        IsEnabled = !downloading; // Block interaction during download
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

/// <summary>ViewModel for a single model row in the list.</summary>
public class ModelViewModel : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Size { get; set; } = "";
    public string Quantization { get; set; } = "";
    public string RamRequirement { get; set; } = "";
    public string TierLabel { get; set; } = "";
    public string InstalledVersion { get; set; } = "";

    public bool IsInstalled { get; set; }
    public bool IsActive { get; set; }

    public Visibility RecommendedBadge { get; set; } = Visibility.Collapsed;
    public Visibility InstalledBadge { get; set; } = Visibility.Collapsed;
    public Visibility ActiveBadge { get; set; } = Visibility.Collapsed;
    public Visibility UpdateBadge { get; set; } = Visibility.Collapsed;

    public Visibility DownloadVisible { get; set; } = Visibility.Collapsed;
    public Visibility DeleteVisible { get; set; } = Visibility.Collapsed;
    public Visibility ActivateVisible { get; set; } = Visibility.Collapsed;
    public Visibility UpdateVisible { get; set; } = Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}