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
using System.Windows.Input;
using System.Windows.Media;

namespace FlipsiInk;

/// <summary>
/// Dialog for creating/editing notebook cover and metadata.
/// Shows a live preview of the cover, color picker, template selector,
/// and metadata fields (title, author, description).
/// </summary>
public partial class NotebookCoverDialog : Window
{
    private string _selectedColor = "#007AFF";
    private CoverTemplate _selectedTemplate = CoverTemplate.SolidColor;

    /// <summary>Result metadata after dialog closes with OK.</summary>
    public NotebookMetadata Result { get; private set; }

    /// <summary>
    /// Creates the dialog. Pass existing metadata to edit, or null for a new notebook.
    /// </summary>
    public NotebookCoverDialog(NotebookMetadata? existing = null)
    {
        InitializeComponent();

        Result = existing != null
            ? new NotebookMetadata
            {
                Id = existing.Id,
                Title = existing.Title,
                Description = existing.Description,
                Color = existing.Color,
                Template = existing.Template,
                CreatedAt = existing.CreatedAt,
                ModifiedAt = existing.ModifiedAt,
                PageCount = existing.PageCount,
                ThumbnailPath = existing.ThumbnailPath,
                CustomCoverPath = existing.CustomCoverPath,
                FileSize = existing.FileSize
            }
            : new NotebookMetadata();

        // Populate fields
        TitleBox.Text = Result.Title;
        AuthorBox.Text = Result.Description; // reuse Description field as Author is stored in Description for simplicity; add Author property below
        DescriptionBox.Text = Result.Description;
        _selectedColor = Result.Color;
        _selectedTemplate = Result.Template;

        CreatedLabel.Text = Result.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        ModifiedLabel.Text = Result.ModifiedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        // Populate color buttons
        BuildColorButtons();

        // Set template combo
        TemplateCombo.SelectedIndex = (int)_selectedTemplate;

        // Live preview on text change
        TitleBox.TextChanged += (s, e) => RefreshPreview();
        DescriptionBox.TextChanged += (s, e) => RefreshPreview();

        RefreshPreview();
    }

    /// <summary>Author entered by the user.</summary>
    public string Author { get; private set; } = string.Empty;

    private void BuildColorButtons()
    {
        ColorPanel.Children.Clear();
        foreach (var kvp in NotebookCover.CoverColors)
        {
            var btn = new Button
            {
                Width = 36,
                Height = 36,
                Margin = new Thickness(3),
                Background = NotebookCover.GetCoverBrush(kvp.Value),
                BorderThickness = new Thickness(2),
                Tag = kvp.Value,
                Cursor = Cursors.Hand
            };

            if (kvp.Value == _selectedColor)
                btn.BorderBrush = Brushes.White;
            else
                btn.BorderBrush = new SolidColorBrush(Colors.Transparent);

            btn.Click += ColorBtn_Click;
            ColorPanel.Children.Add(btn);
        }
    }

    private void ColorBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            _selectedColor = hex;
            // Update border highlights
            foreach (var child in ColorPanel.Children)
            {
                if (child is Button b)
                    b.BorderBrush = (b.Tag as string) == hex
                        ? Brushes.White
                        : new SolidColorBrush(Colors.Transparent);
            }
            RefreshPreview();
        }
    }

    private void TemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateCombo.SelectedIndex >= 0)
        {
            _selectedTemplate = (CoverTemplate)TemplateCombo.SelectedIndex;
            RefreshPreview();
        }
    }

    private void RefreshPreview()
    {
        var brush = NotebookCover.CreateCoverVisual(_selectedColor, _selectedTemplate, TitleBox.Text);
        CoverPreviewBorder.Background = brush;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result.Title = string.IsNullOrWhiteSpace(TitleBox.Text) ? "Unbenannt" : TitleBox.Text.Trim();
        Result.Description = DescriptionBox.Text ?? string.Empty;
        Result.Color = _selectedColor;
        Result.Template = _selectedTemplate;
        Result.ModifiedAt = DateTime.UtcNow;
        Author = AuthorBox.Text ?? string.Empty;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}