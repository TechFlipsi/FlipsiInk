// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlipsiInk;

public partial class MainWindow : Window
{
    // Tools
    private InkCanvasEditingMode _currentTool = InkCanvasEditingMode.Ink;
    private System.Windows.Media.Color _currentColor = Colors.Black;
    private double _currentSize = 2;

    // Undo/Redo stacks
    private readonly Stack<StrokeCollection> _undoStack = new();
    private readonly Stack<StrokeCollection> _undoRedoStack = new();
    private const int MaxUndoSteps = 50;

    // OCR
    private OcrEngine? _ocrEngine;
    private bool _modelLoaded = false;

    // Theme
    private string _currentTheme = "system";

    public MainWindow()
    {
        InitializeComponent();
        VersionLabel.Text = $"v{App.Version}";
        SetupToolButtons();
        SetupCanvas();
        ApplyTheme(App.Config.Theme);
        LoadModelAsync();

        // Track strokes for undo
        MainCanvas.StrokeCollected += OnStrokeCollected;
        MainCanvas.Strokes.StrokesChanged += OnStrokesChanged;
    }

    #region Canvas Setup

    private void SetupCanvas()
    {
        MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
        MainCanvas.DefaultDrawingAttributes.Color = _currentColor;
        MainCanvas.DefaultDrawingAttributes.Width = _currentSize;
        MainCanvas.DefaultDrawingAttributes.Height = _currentSize;
        MainCanvas.DefaultDrawingAttributes.FitToCurve = true;
        MainCanvas.DefaultDrawingAttributes.StylusTip = StylusTip.Ellipse;
        MainCanvas.DefaultDrawingAttributes.IgnorePressure = false;
    }

    #endregion

    #region Undo/Redo (Issue #24)

    private StrokeCollection? _strokesBeforeChange;

    private void OnStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        // Push to undo stack on new stroke
        if (_strokesBeforeChange != null)
        {
            _undoStack.Push(_strokesBeforeChange.Clone());
            _undoRedoStack.Clear();
            if (_undoStack.Count > MaxUndoSteps)
            {
                // Remove oldest entry by converting to list and back
                var temp = _undoStack.ToList();
                temp.RemoveAt(0);
                _undoStack.Clear();
                foreach (var item in temp.AsEnumerable().Reverse())
                    _undoStack.Push(item);
            }
        }
    }

    private void OnStrokesChanged(object sender, StrokeCollectionChangeEventArgs e)
    {
        if (e.Action == StrokeCollectionChangeAction.Add && _strokesBeforeChange == null)
        {
            _strokesBeforeChange = MainCanvas.Strokes.Clone();
        }
    }

    private void Undo()
    {
        if (_undoStack.Count > 0)
        {
            _undoRedoStack.Push(MainCanvas.Strokes.Clone());
            var previous = _undoStack.Pop();
            MainCanvas.Strokes.Clear();
            MainCanvas.Strokes.Add(previous);
            StatusText.Text = "↩ Rückgängig";
        }
    }

    private void Redo()
    {
        if (_undoRedoStack.Count > 0)
        {
            _undoStack.Push(MainCanvas.Strokes.Clone());
            var next = _undoRedoStack.Pop();
            MainCanvas.Strokes.Clear();
            MainCanvas.Strokes.Add(next);
            StatusText.Text = "↪ Wiederholt";
        }
    }

    #endregion

    #region Tool Buttons

    private void SetupToolButtons()
    {
        // Tools
        BtnPen.Click += (s, e) => SetTool(InkCanvasEditingMode.Ink, BtnPen);
        BtnHighlighter.Click += (s, e) => SetHighlighter(BtnHighlighter);
        BtnEraser.Click += (s, e) => SetTool(InkCanvasEditingMode.EraseByStroke, BtnEraser);
        BtnSelect.Click += (s, e) => SetTool(InkCanvasEditingMode.Select, BtnSelect);
        BtnLine.Click += (s, e) => SetTool(InkCanvasEditingMode.Ink, BtnLine);
        BtnRect.Click += (s, e) => SetTool(InkCanvasEditingMode.Ink, BtnRect);
        BtnCircle.Click += (s, e) => SetTool(InkCanvasEditingMode.Ink, BtnCircle);

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
        BtnUndo.Click += (s, e) => Undo();
        BtnRedo.Click += (s, e) => Redo();
        BtnClear.Click += (s, e) =>
        {
            if (MainCanvas.Strokes.Count > 0)
            {
                _undoStack.Push(MainCanvas.Strokes.Clone());
                _undoRedoStack.Clear();
                MainCanvas.Strokes.Clear();
                StatusText.Text = "🗑️ Alles gelöscht";
            }
        };
        BtnRecognize.Click += async (s, e) => await RecognizeText();
        BtnCalc.Click += async (s, e) => await RecognizeAndCalculate();
        BtnSave.Click += (s, e) => SaveNote();
        BtnSettings.Click += (s, e) => OpenSettings();
    }

    private void SetTool(InkCanvasEditingMode mode, Button activeBtn)
    {
        // Save state for undo before switching away from ink
        MainCanvas.EditingMode = mode;

        // Reset highlighter
        if (mode == InkCanvasEditingMode.Ink)
        {
            MainCanvas.DefaultDrawingAttributes.Color = _currentColor;
            MainCanvas.DefaultDrawingAttributes.Width = _currentSize;
            MainCanvas.DefaultDrawingAttributes.Height = _currentSize;
            MainCanvas.DefaultDrawingAttributes.IsHighlighter = false;
        }

        // Highlight active button
        var allToolBtns = new[] { BtnPen, BtnHighlighter, BtnEraser, BtnSelect, BtnLine, BtnRect, BtnCircle };
        foreach (var btn in allToolBtns)
            btn.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
        activeBtn.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
        _currentTool = mode;
    }

    private void SetHighlighter(Button activeBtn)
    {
        MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
        MainCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
        MainCanvas.DefaultDrawingAttributes.Width = 16;
        MainCanvas.DefaultDrawingAttributes.Height = 16;
        MainCanvas.DefaultDrawingAttributes.IsHighlighter = true;

        var allToolBtns = new[] { BtnPen, BtnHighlighter, BtnEraser, BtnSelect, BtnLine, BtnRect, BtnCircle };
        foreach (var btn in allToolBtns)
            btn.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
        activeBtn.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
    }

    private void SetColor(System.Windows.Media.Color color, Button activeBtn)
    {
        _currentColor = color;
        MainCanvas.DefaultDrawingAttributes.Color = color;
        MainCanvas.DefaultDrawingAttributes.IsHighlighter = false;
        MainCanvas.DefaultDrawingAttributes.Width = _currentSize;
        MainCanvas.DefaultDrawingAttributes.Height = _currentSize;

        foreach (var btn in new[] { BtnBlack, BtnBlue, BtnRed, BtnGreen })
            btn.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
        activeBtn.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
    }

    private void SetSize(double size, Button activeBtn)
    {
        _currentSize = size;
        MainCanvas.DefaultDrawingAttributes.Width = size;
        MainCanvas.DefaultDrawingAttributes.Height = size;
        MainCanvas.DefaultDrawingAttributes.IsHighlighter = false;

        foreach (var btn in new[] { BtnThin, BtnMedium, BtnThick })
            btn.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
        activeBtn.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
    }

    #endregion

    #region Theme (Issue #8)

    private void ApplyTheme(string theme)
    {
        _currentTheme = theme;
        bool isDark;

        if (theme == "system")
        {
            // Check Windows registry for system theme
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                isDark = key?.GetValue("AppsUseLightTheme") is int val && val == 0;
            }
            catch
            {
                isDark = false;
            }
        }
        else
        {
            isDark = theme == "dark";
        }

        // Apply colors
        var bg = isDark ? "#1E1E1E" : "#F5F5F5";
        var panelBg = isDark ? "#252525" : "#E8E8E8";
        var topBg = isDark ? "#2D2D2D" : "#DCDCDC";
        var fg = isDark ? "#FFFFFF" : "#1E1E1E";
        var border = isDark ? "#444444" : "#CCCCCC";

        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));

        // Top bar
        var topBar = (DockPanel)((DockPanel)Content).Children[0];
        topBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(topBg));

        // Left toolbar
        var leftBorder = (Border)((DockPanel)Content).Children[1];
        leftBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(panelBg));

        // Right panel
        var rightBorder = (Border)((DockPanel)Content).Children[2];
        rightBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(panelBg));

        // Canvas stays light always (better for writing)
        MainCanvas.Background = new SolidColorBrush(Colors.White);

        // Update text colors
        foreach (var tb in FindVisualChildren<TextBlock>(this))
        {
            if (tb.Name != "VersionLabel")
                tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
        }
        VersionLabel.Foreground = new SolidColorBrush(isDark ? Colors.Gray : Colors.DarkGray);

        // TextBox styling
        foreach (var tx in FindVisualChildren<TextBox>(this))
        {
            tx.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            tx.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
            tx.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(border));
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) yield return typed;
            foreach (var grandChild in FindVisualChildren<T>(child))
                yield return grandChild;
        }
    }

    #endregion

    #region OCR & Math

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

    #endregion

    #region Save

    private void SaveNote()
    {
        var saveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FlipsiInk");
        Directory.CreateDirectory(saveDir);

        var filename = $"note_{DateTime.Now:yyyyMMdd_HHmmss}";
        var pngPath = Path.Combine(saveDir, filename + ".png");
        var jsonPath = Path.Combine(saveDir, filename + ".json");

        try
        {
            // Save as PNG
            var bitmap = RenderStrokesToBitmap();
            bitmap.Save(pngPath, ImageFormat.Png);

            // Save strokes as JSON for re-editing
            var strokesData = MainCanvas.Strokes.Select(s => new
            {
                Color = s.DrawingAttributes.Color.ToString(),
                Width = s.DrawingAttributes.Width,
                Height = s.DrawingAttributes.Height,
                IsHighlighter = s.DrawingAttributes.IsHighlighter,
                Points = s.StylusPoints.Select(p => new
                {
                    X = p.X, Y = p.Y, PressureFactor = p.PressureFactor
                })
            });
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(strokesData, new JsonSerializerOptions { WriteIndented = true }));

            StatusText.Text = $"✓ Gespeichert: {filename}.png + .json";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✗ Speichern fehlgeschlagen: {ex.Message}";
        }
    }

    #endregion

    #region Settings

    private void OpenSettings()
    {
        // TODO: Full settings window (Issue #6)
        StatusText.Text = "⚙️ Einstellungen kommen bald...";
    }

    #endregion

    #region Keyboard Shortcuts

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Ctrl+Z = Undo
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control && !(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
        {
            Undo();
            e.Handled = true;
        }
        // Ctrl+Y or Ctrl+Shift+Z = Redo
        else if ((e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) ||
                 (e.Key == Key.Z && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
        {
            Redo();
            e.Handled = true;
        }
        // Ctrl+Shift+R = Recognize
        else if (e.Key == Key.R && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            _ = RecognizeText();
            e.Handled = true;
        }
        // Ctrl+Shift+M = Math
        else if (e.Key == Key.M && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            _ = RecognizeAndCalculate();
            e.Handled = true;
        }
    }

    #endregion
}