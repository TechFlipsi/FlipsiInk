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
using System.Windows.Input;
using System.Windows.Media;

namespace FlipsiInk;

/// <summary>
/// Text-Eingabe-Tool für den InkCanvas.
/// Ermöglicht Tastatureingabe an einer Position auf dem Canvas,
/// mit Font-Einstellungen und Rendering als DrawingVisual.
/// </summary>
public class TextTool
{
    #region Properties

    /// <summary>Ob der Text-Eingabemodus aktiv ist.</summary>
    public bool IsActive { get; set; }

    /// <summary>Position, an der der Text eingefügt wird.</summary>
    public Point Position { get; set; }

    /// <summary>Schriftfamilie. Standard: "Segoe UI".</summary>
    public string FontFamily { get; private set; } = "Segoe UI";

    /// <summary>Schriftgröße. Standard: 16.</summary>
    public double FontSize { get; private set; } = 16;

    /// <summary>Schriftfarbe. Standard: Schwarz.</summary>
    public Color FontColor { get; private set; } = Colors.Black;

    /// <summary>Fettgedruckt.</summary>
    public bool IsBold { get; private set; }

    /// <summary>Kursiv.</summary>
    public bool IsItalic { get; private set; }

    /// <summary>Unterstrichen.</summary>
    public bool IsUnderline { get; private set; }

    #endregion

    #region Methoden

    /// <summary>
    /// Erstellt eine WPF TextBox auf dem Canvas an der angegebenen Position.
    /// Die TextBox verhält sich wie eine Texteingabe: Enter = Abschließen, Escape = Abbrechen.
    /// </summary>
    /// <param name="position">Position auf dem Canvas.</param>
    /// <returns>Die erstellte TextBox als FrameworkElement.</returns>
    public FrameworkElement CreateTextBox(Point position)
    {
        Position = position;

        var textBox = new TextBox
        {
            Width = 300,
            MinWidth = 50,
            MaxWidth = 800,
            FontFamily = new System.Windows.Media.FontFamily(FontFamily),
            FontSize = FontSize,
            Foreground = new SolidColorBrush(FontColor),
            FontWeight = IsBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = IsItalic ? FontStyles.Italic : FontStyles.Normal,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.Gray),
            Background = new SolidColorBrush(Color.FromArgb(30, 200, 200, 255)),
            CaretIndex = 0,
            Tag = this // Referenz auf das TextTool für KeyBinding
        };

        // Enter = Text abschließen
        textBox.PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                textBox.RaiseEvent(new RoutedEventArgs(EnterPressedEvent));
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                textBox.RaiseEvent(new RoutedEventArgs(EscapePressedEvent));
            }
        };

        // Automatische Breitenanpassung bei Texteingabe
        textBox.TextChanged += (s, e) =>
        {
            // Mindestbreite sicherstellen
            if (textBox.ActualWidth < 50)
                textBox.Width = 50;
        };

        return textBox;
    }

    /// <summary>
    /// Schließt die Texteingabe ab: Wandelt den TextBox-Inhalt in ein DrawingVisual um
    /// und entfernt die TextBox vom Canvas. Der Text wird somit "geflammt" (fixiert).
    /// </summary>
    /// <param name="textBox">Die abzuschließende TextBox.</param>
    /// <param name="canvas">Der InkCanvas, auf dem das DrawingVisual eingefügt wird.</param>
    public void FinalizeText(TextBox textBox, InkCanvas canvas)
    {
        if (string.IsNullOrEmpty(textBox.Text))
            return;

        // DrawingVisual für den gerenderten Text erstellen
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var typeface = new Typeface(
                new System.Windows.Media.FontFamily(FontFamily),
                IsItalic ? FontStyles.Italic : FontStyles.Normal,
                IsBold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);

            var formattedText = new FormattedText(
                textBox.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                FontSize,
                new SolidColorBrush(FontColor),
                VisualTreeHelper.GetDpi(visual).PixelsPerDip);

            // Text am Ursprung des DrawingVisual rendern
            dc.DrawText(formattedText, new Point(0, 0));

            // Unterstreichung zeichnen falls aktiviert
            if (IsUnderline)
            {
                var pen = new Pen(new SolidColorBrush(FontColor), 1);
                dc.DrawLine(pen, new Point(0, FontSize + 2), new Point(formattedText.Width, FontSize + 2));
            }
        }

        // DrawingVisual auf dem Canvas platzieren
        // Da InkCanvas kein direktes Children-Support für DrawingVisual hat,
        // verwenden wir einen Container-Ansatz über ein Host-Element
        var host = new DrawingVisualHost(visual);
        InkCanvas.SetLeft(host, Position.X);
        InkCanvas.SetTop(host, Position.Y);

        // Element zum Canvas hinzufügen (über die Children-Auflistung nicht direkt möglich,
        // daher über ein workaround mit einem Border-Container)
        var container = new Border
        {
            Child = host,
            Background = Brushes.Transparent
        };
        InkCanvas.SetLeft(container, Position.X);
        InkCanvas.SetTop(container, Position.Y);

        // TextBox entfernen und fixiertes Element hinzufügen
        var parent = textBox.Parent as Panel;
        if (parent != null)
        {
            var index = parent.Children.IndexOf(textBox);
            parent.Children.Remove(textBox);
            parent.Children.Insert(index, container);
        }

        IsActive = false;
    }

    /// <summary>
    /// Gibt die aktuellen Font-Einstellungen als formatierten String zurück.
    /// </summary>
    /// <returns>Font-Info als String (z.B. "Segoe UI, 16pt, Schwarz, Bold Italic").</returns>
    public string GetFontInfo()
    {
        var style = "";
        if (IsBold) style += "Bold ";
        if (IsItalic) style += "Italic ";
        if (IsUnderline) style += "Underline ";
        style = style.Trim();

        var colorName = FontColor.ToString();

        return string.IsNullOrWhiteSpace(style)
            ? $"{FontFamily}, {FontSize}pt, {colorName}"
            : $"{FontFamily}, {FontSize}pt, {colorName}, {style}";
    }

    /// <summary>
    /// Setzt die Font-Einstellungen für die Texteingabe.
    /// </summary>
    /// <param name="family">Schriftfamilie (z.B. "Segoe UI").</param>
    /// <param name="size">Schriftgröße in Punkt.</param>
    /// <param name="color">Schriftfarbe.</param>
    /// <param name="bold">Fettgedruckt.</param>
    /// <param name="italic">Kursiv.</param>
    /// <param name="underline">Unterstrichen.</param>
    public void SetFont(string family, double size, Color color, bool bold, bool italic, bool underline)
    {
        FontFamily = family;
        FontSize = size;
        FontColor = color;
        IsBold = bold;
        IsItalic = italic;
        IsUnderline = underline;
    }

    /// <summary>
    /// Listet alle auf dem System installierten Schriftarten auf.
    /// </summary>
    /// <returns>Liste der installierten Font-Familien-Namen.</returns>
    public List<string> GetAvailableFonts()
    {
        var fonts = new List<string>();
        foreach (var fontFamily in System.Windows.Media.Fonts.SystemFontFamilies)
        {
            fonts.Add(fontFamily.Source);
        }
        fonts.Sort(StringComparer.OrdinalIgnoreCase);
        return fonts;
    }

    #endregion

    #region RoutedEvents (für Enter/Escape-Handling)

    /// <summary>Custom RoutedEvent für Enter-Taste in der TextBox.</summary>
    public static readonly RoutedEvent EnterPressedEvent =
        EventManager.RegisterRoutedEvent("EnterPressed", RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(TextTool));

    /// <summary>Custom RoutedEvent für Escape-Taste in der TextBox.</summary>
    public static readonly RoutedEvent EscapePressedEvent =
        EventManager.RegisterRoutedEvent("EscapePressed", RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(TextTool));

    #endregion
}

/// <summary>
/// Host-Element für ein DrawingVisual, damit es in WPF-Layout-Strukturen verwendet werden kann.
/// </summary>
internal class DrawingVisualHost : FrameworkElement
{
    private readonly DrawingVisual _visual;

    public DrawingVisualHost(DrawingVisual visual)
    {
        _visual = visual;
        AddVisualChild(_visual);
    }

    protected override Visual GetVisualChild(int index) => _visual;
    protected override int VisualChildrenCount => 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        // Größe basierend auf dem Inhalt des DrawingVisual berechnen
        if (_visual.ContentBounds != Rect.Empty)
            return _visual.ContentBounds.Size;
        return base.MeasureOverride(availableSize);
    }
}