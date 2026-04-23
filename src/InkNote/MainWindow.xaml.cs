// InkNote - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NCalc.Async;

namespace InkNote;

public partial class MainWindow : Window
{
    private InkCanvasEditingMode _currentTool = InkCanvasEditingMode.Ink;
    private System.Windows.Media.Color _currentColor = Colors.Black;
    private double _currentSize = 2;
    private OcrEngine? _ocrEngine;
    private bool _modelLoaded = false;

    public MainWindow()
    {
        InitializeComponent();
        VersionLabel.Text = $"v{App.Version}";
        SetupToolButtons();
        SetupCanvas();
        LoadModelAsync();
    }

    private void SetupCanvas()
    {
        MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
        MainCanvas.DefaultDrawingAttributes.Color = _currentColor;
        MainCanvas.DefaultDrawingAttributes.Width = _currentSize;
        MainCanvas.DefaultDrawingAttributes.Height = _currentSize;
        MainCanvas.DefaultDrawingAttributes.FitToCurve = true;
        MainCanvas.DefaultDrawingAttributes.StylusTip = StylusTip.Ellipse;

        // Enable pressure sensitivity
        MainCanvas.DefaultDrawingAttributes.IgnorePressure = false;
    }

    private void SetupToolButtons()
    {
        // Tools
        BtnPen.Click += (s, e) => SetTool(InkCanvasEditingMode.Ink, BtnPen);
        BtnHighlighter.Click += (s, e) => SetHighlighter(BtnHighlighter);
        BtnEraser.Click += (s, e) => SetTool(InkCanvasEditingMode.EraseByStroke, BtnEraser);
        BtnSelect.Click += (s, e) => SetTool(InkCanvasEditingMode.Select, BtnSelect);
        BtnLine.Click += (s, e) => SetTool(InkCanvasEditingMode.Ink, BtnLine); // TODO: line mode
        BtnRect.Click += (s, e) => SetTool(InkCanvasEditingMode.Ink, BtnRect); // TODO: rect mode
        BtnCircle.Click += (s, e) => SetTool(InkCanvasEditingMode.Ink, BtnCircle); // TODO: circle mode

        // Colors
        BtnBlack.Click += (s, e) => SetColor(Colors.Black, BtnBlack);
        BtnBlue.Click += (s, e) => SetColor(Colors.Blue, BtnBlue);
        BtnRed.Click += (s, e) => SetColor(Colors.Red, BtnRed);
        BtnGreen.Click += (s, e) => SetColor(Colors.Green, BtnGreen);

        // Sizes
        BtnThin.Click += (s, e) => SetSize(1, BtnThin);
        BtnMedium.Click += (s, e) => SetSize(2.5, BtnMedium);
        BtnThick.Click += (s, e) => SetSize(5, BtnThick);

        // Actions
        BtnUndo.Click += (s, e) => MainCanvas.Strokes.Remove(MainCanvas.Strokes.Last());
        BtnRedo.Click += (s, e) => { /* TODO: redo stack */ };
        BtnClear.Click += (s, e) => MainCanvas.Strokes.Clear();
        BtnRecognize.Click += async (s, e) => await RecognizeText();
        BtnCalc.Click += async (s, e) => await RecognizeAndCalculate();
        BtnSave.Click += (s, e) => SaveNote();
        BtnSettings.Click += (s, e) => { /* TODO: settings */ };
    }

    private void SetTool(InkCanvasEditingMode mode, Button activeBtn)
    {
        MainCanvas.EditingMode = mode;
        // Reset all tool button backgrounds
        foreach (var btn in new[] { BtnPen, BtnHighlighter, BtnEraser, BtnSelect, BtnLine, BtnRect, BtnCircle })
            btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 45, 45, 45));
        activeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 120, 215));
        _currentTool = mode;
    }

    private void SetHighlighter(Button activeBtn)
    {
        MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
        MainCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
        MainCanvas.DefaultDrawingAttributes.Width = 12;
        MainCanvas.DefaultDrawingAttributes.Height = 12;
        MainCanvas.DefaultDrawingAttributes.IsHighlighter = true;
        foreach (var btn in new[] { BtnPen, BtnHighlighter, BtnEraser, BtnSelect, BtnLine, BtnRect, BtnCircle })
            btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 45, 45, 45));
        activeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 120, 215));
    }

    private void SetColor(System.Windows.Media.Color color, Button activeBtn)
    {
        _currentColor = color;
        MainCanvas.DefaultDrawingAttributes.Color = color;
        MainCanvas.DefaultDrawingAttributes.IsHighlighter = false;
        foreach (var btn in new[] { BtnBlack, BtnBlue, BtnRed, BtnGreen })
            btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 45, 45, 45));
        activeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 120, 215));
    }

    private void SetSize(double size, Button activeBtn)
    {
        _currentSize = size;
        MainCanvas.DefaultDrawingAttributes.Width = size;
        MainCanvas.DefaultDrawingAttributes.Height = size;
        foreach (var btn in new[] { BtnThin, BtnMedium, BtnThick })
            btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 45, 45, 45));
        activeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 120, 215));
    }

    private async void LoadModelAsync()
    {
        StatusText.Text = "Lade KI-Modell...";
        ModelStatus.Text = "Modell: wird geladen...";
        ModelStatus.Foreground = System.Windows.Media.Brushes.Orange;

        try
        {
            _ocrEngine = new OcrEngine();
            await Task.Run(() => _ocrEngine.LoadModel());
            _modelLoaded = true;
            ModelStatus.Text = $"Modell: geladen ✓ ({_ocrEngine.ModelName})";
            ModelStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
            StatusText.Text = "Bereit – schreibe und klicke 🔤 oder 🧮";
        }
        catch (Exception ex)
        {
            _modelLoaded = false;
            ModelStatus.Text = $"Modell: Fehler – {ex.Message}";
            ModelStatus.Foreground = System.Windows.Media.Brushes.Red;
            StatusText.Text = "KI nicht verfügbar – Stift funktioniert trotzdem";
        }
    }

    private async Task RecognizeText()
    {
        if (!_modelLoaded || _ocrEngine == null)
        {
            StatusText.Text = "⚠️ Modell nicht geladen!";
            return;
        }

        if (MainCanvas.Strokes.Count == 0)
        {
            StatusText.Text = "⚠️ Nichts zu erkennen – zuerst schreiben!";
            return;
        }

        StatusText.Text = "🔤 Erkenne Text...";
        RecognizedText.Text = "";

        try
        {
            var bitmap = RenderStrokesToBitmap();
            var text = await Task.Run(() => _ocrEngine.Recognize(bitmap));
            RecognizedText.Text = text;
            StatusText.Text = $"✓ {text.Length} Zeichen erkannt";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✗ Fehler: {ex.Message}";
        }
    }

    private async Task RecognizeAndCalculate()
    {
        if (!_modelLoaded || _ocrEngine == null)
        {
            StatusText.Text = "⚠️ Modell nicht geladen!";
            return;
        }

        if (MainCanvas.Strokes.Count == 0)
        {
            StatusText.Text = "⚠️ Nichts zu berechnen – zuerst schreiben!";
            return;
        }

        StatusText.Text = "🧮 Erkenne und berechne...";
        MathResult.Text = "";

        try
        {
            var bitmap = RenderStrokesToBitmap();
            var text = await Task.Run(() => _ocrEngine.Recognize(bitmap));
            RecognizedText.Text = text;

            // Try to find and solve math expressions
            var results = MathEvaluator.Evaluate(text);
            MathResult.Text = results;
            StatusText.Text = string.IsNullOrEmpty(results) ? "Kein Mathe-Ausdruck gefunden" : "✓ Berechnet";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✗ Fehler: {ex.Message}";
        }
    }

    private System.Drawing.Bitmap RenderStrokesToBitmap()
    {
        // Render InkCanvas strokes to bitmap for OCR
        var width = (int)MainCanvas.ActualWidth;
        var height = (int)MainCanvas.ActualHeight;
        if (width <= 0 || height <= 0) { width = 800; height = 600; }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(MainCanvas);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;

        return new System.Drawing.Bitmap(stream);
    }

    private void SaveNote()
    {
        var config = App.Config;
        var saveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "InkNote");
        Directory.CreateDirectory(saveDir);

        var filename = $"note_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var filepath = Path.Combine(saveDir, filename);

        try
        {
            var bitmap = RenderStrokesToBitmap();
            bitmap.Save(filepath, ImageFormat.Png);
            StatusText.Text = $"✓ Gespeichert: {filename}";

            // Also save strokes as JSON for re-editing
            var jsonPath = filepath.Replace(".png", ".json");
            var strokesData = MainCanvas.Strokes.Select(s => new
            {
                Points = s.StylusPoints.Select(p => new { X = p.X, Y = p.Y, PressureFactor = p.PressureFactor })
            });
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(strokesData));
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✗ Speichern fehlgeschlagen: {ex.Message}";
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Ctrl+Z = Undo
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (MainCanvas.Strokes.Count > 0)
                MainCanvas.Strokes.Remove(MainCanvas.Strokes.Last());
            e.Handled = true;
        }
        // Ctrl+Shift+R = Recognize
        if (e.Key == Key.R && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            _ = RecognizeText();
            e.Handled = true;
        }
        // Ctrl+Shift+M = Math
        if (e.Key == Key.M && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            _ = RecognizeAndCalculate();
            e.Handled = true;
        }
    }
}