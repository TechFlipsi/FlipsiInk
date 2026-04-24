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
using System.Windows.Media;

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
        ModelsDirLabel.Text = $"Models: {_manager.ModelsDirectory}";
        RefreshList();

        // Fetch remote catalog in background
        _ = RefreshRemoteCatalogAsync();
    }

    private async Task RefreshRemoteCatalogAsync()
    {
        try
        {
            var loaded = await _manager.FetchRemoteCatalogAsync();
            if (loaded)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusLabel.Text = "Modellkatalog aktualisiert.";
                    RefreshList();
                });
            }
        }
        catch
        {
            // Silently fall back to hardcoded catalog
        }
    }

    private void RefreshList()
    {
        _viewModels.Clear();
        var catalog = _manager.GetCatalog();
        var activeId = _manager.ActiveModelId;
        var totalRamGb = ModelManager.GetTotalRamMb() / 1024.0;

        // Show RAM warning if system RAM is low
        if (totalRamGb < 16 && RamWarningBorder != null)
        {
            RamWarningBorder.Visibility = Visibility.Visible;
            RamWarningText.Text = $"Ihr System hat ~{totalRamGb:F0} GB RAM. Das 'Stark' Modell erfordert 16 GB. 'Mittel' sollte funktionieren.";
        }

        foreach (var entry in catalog)
        {
            var installed = _manager.GetInstalled(entry.Id);
            var isActive = entry.Id == activeId;
            var hasEnoughRam = ModelManager.HasEnoughRam(entry.MinRamGb);

            _viewModels.Add(new ModelViewModel
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description,
                Size = entry.Size,
                Quantization = $"Quantisierung: {entry.Quantization}",
                RamRequirement = $"Min. RAM: {entry.MinRamGb} GB",
                TierLabelShort = ModelManager.GetTierLabel(entry.Tier),
                TierLabel = entry.Tier switch
                {
                    "mittel" => "Blau - Mittel (8 GB RAM)",
                    "stark" => "Blau - Stark (16 GB RAM)",
                    "premium" => "Lila - Premium (32 GB RAM)",
                    _ => entry.Tier
                },
                TierColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ModelManager.GetTierColor(entry.Tier))),
                RamBarWidth = Math.Max(20, Math.Min(120, entry.MinRamGb * 4)),
                RamGbLabel = $"{entry.MinRamGb} GB",
                IsInstalled = installed != null,
                IsActive = isActive,
                InstalledVersion = installed != null ? $"v{installed.Version}" : "",
                HasEnoughRam = hasEnoughRam,
                RecommendedBadge = entry.IsRecommended ? Visibility.Visible : Visibility.Collapsed,
                InstalledBadge = installed != null ? Visibility.Visible : Visibility.Collapsed,
                ActiveBadge = isActive ? Visibility.Visible : Visibility.Collapsed,
                UpdateBadge = Visibility.Collapsed,
                DownloadVisible = installed == null ? Visibility.Visible : Visibility.Collapsed,
                DeleteVisible = installed != null ? Visibility.Visible : Visibility.Collapsed,
                ActivateVisible = installed != null && !isActive ? Visibility.Visible : Visibility.Collapsed,
                UpdateVisible = Visibility.Collapsed,
                RamWarningVisible = hasEnoughRam ? Visibility.Collapsed : Visibility.Visible
            });
        }

        ModelList.ItemsSource = null;
        ModelList.ItemsSource = _viewModels;

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

        // RAM warning
        if (!ModelManager.HasEnoughRam(catalog.MinRamGb))
        {
            var result = MessageBox.Show(
                $"Dieses Modell erfordert mindestens {catalog.MinRamGb} GB RAM. Ihr System hat ~{ModelManager.GetTotalRamMb() / 1024:F0} GB.\n\nTrotzdem herunterladen?",
                "RAM-Warnung", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }

        SetDownloading(true);
        try
        {
            await _manager.DownloadModelAsync(catalog, new Progress<double>(p =>
            {
                DownloadProgress.Value = p * 100;
                StatusLabel.Text = $"Lade {catalog.Name} herunter... {p:P0}";
            }));
            StatusLabel.Text = $"{catalog.Name} heruntergeladen!";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Fehler: {ex.Message}";
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
            $"'{name}' wirklich loeschen?", "Modell loeschen",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        _manager.DeleteModel(id);
        StatusLabel.Text = $"{name} geloescht.";
        RefreshList();
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string id) return;
        _manager.SetActiveModel(id);
        StatusLabel.Text = "Aktives Modell geaendert - OCR wird beim naechsten Erkennen neu geladen.";
        RefreshList();
    }

    private void SetDownloading(bool downloading)
    {
        DownloadProgress.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;
        if (!downloading) DownloadProgress.Value = 0;
        IsEnabled = !downloading;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public class ModelViewModel : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Size { get; set; } = "";
    public string Quantization { get; set; } = "";
    public string RamRequirement { get; set; } = "";
    public string TierLabel { get; set; } = "";
    public string TierLabelShort { get; set; } = "";
    public Brush TierColor { get; set; } = Brushes.Gray;
    public double RamBarWidth { get; set; } = 40;
    public string RamGbLabel { get; set; } = "";
    public string InstalledVersion { get; set; } = "";

    public bool IsInstalled { get; set; }
    public bool IsActive { get; set; }
    public bool HasEnoughRam { get; set; } = true;

    public Visibility RecommendedBadge { get; set; } = Visibility.Collapsed;
    public Visibility InstalledBadge { get; set; } = Visibility.Collapsed;
    public Visibility ActiveBadge { get; set; } = Visibility.Collapsed;
    public Visibility UpdateBadge { get; set; } = Visibility.Collapsed;
    public Visibility RamWarningVisible { get; set; } = Visibility.Collapsed;

    public Visibility DownloadVisible { get; set; } = Visibility.Collapsed;
    public Visibility DeleteVisible { get; set; } = Visibility.Collapsed;
    public Visibility ActivateVisible { get; set; } = Visibility.Collapsed;
    public Visibility UpdateVisible { get; set; } = Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}