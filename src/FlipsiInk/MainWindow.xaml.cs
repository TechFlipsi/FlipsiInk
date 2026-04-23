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
    private System.Windows.Media.Color _currentColor = System.Windows.Media.Colors.Black;
    private double _currentSize = 2;

    // Initialization flag – prevents events firing before UI is ready
    private bool _initialized = false;

    // Auto-Updater
    private readonly AutoUpdater _autoUpdater = new();
    private System.Threading.Timer? _updateCheckTimer;

    // Undo/Redo stacks (Issue #24)
    private readonly Stack<StrokeCollection> _undoStack = new();
    private readonly Stack<StrokeCollection> _redoStack = new();
    private const int MaxUndoSteps = 50;
    private bool _isUndoRedoing = false;

    // Zoom (Issue #25)
    private readonly ZoomManager _zoomManager = new();

    // Input Mode (Issue #27)
    private InputModeManager? _inputModeManager;

    // Theme (Issue #8)
    private readonly ThemeManager _themeManager = new();
    private Theme _currentTheme = Theme.System;

    // Page template (Issue #17)
    private PageTemplateType _currentTemplate = PageTemplateType.Blank;

    // OCR
    private OcrEngine? _ocrEngine;
    private bool _modelLoaded = false;

    public MainWindow()
    {
        InitializeComponent();
        VersionLabel.Text = $"v{App.Version}";

        try { SetupToolButtons(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SetupToolButtons: {ex}"); }
        try { SetupCanvas(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SetupCanvas: {ex}"); }
        try { SetupZoom(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SetupZoom: {ex}"); }
        try { SetupTheme(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SetupTheme: {ex}"); }
        try { SetupTemplateCombo(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SetupTemplateCombo: {ex}"); }
        try { SetupInputMode(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SetupInputMode: {ex}"); }
        try { LoadModelAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadModelAsync: {ex}"); }

        // Track strokes for undo
        MainCanvas.StrokeCollected += OnStrokeCollected;
        MainCanvas.StrokeErasing += (s, e) => { /* stroke removed */ };

        // UI is now fully initialized – enable event handlers
        _initialized = true;

        // Setup Auto-Updater
        try { SetupAutoUpdater(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SetupAutoUpdater: {ex}"); }
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
        if (_isUndoRedoing) return;
        if (_strokesBeforeChange != null)
        {
            _undoStack.Push(_strokesBeforeChange.Clone());
            _redoStack.Clear();
            if (_undoStack.Count > MaxUndoSteps)
            {
                var temp = _undoStack.ToList();
                temp.RemoveAt(0);
                _undoStack.Clear();
                foreach (var item in temp.AsEnumerable().Reverse())
                    _undoStack.Push(item);
            }
        }
        _strokesBeforeChange = MainCanvas.Strokes.Clone();
    }

    private void OnStrokesChanged(object? sender, EventArgs e)
    {
        // Track changes for undo
    }

    private void Undo()
    {
        if (_undoStack.Count > 0)
        {
            _isUndoRedoing = true;
            _redoStack.Push(MainCanvas.Strokes.Clone());
            var previous = _undoStack.Pop();
            MainCanvas.Strokes.Clear();
            MainCanvas.Strokes.Add(previous);
            _isUndoRedoing = false;
            StatusText.Text = "↩ Rückgängig";
        }
    }

    private void Redo()
    {
        if (_redoStack.Count > 0)
        {
            _isUndoRedoing = true;
            _undoStack.Push(MainCanvas.Strokes.Clone());
            var next = _redoStack.Pop();
            MainCanvas.Strokes.Clear();
            MainCanvas.Strokes.Add(next);
            _isUndoRedoing = false;
            StatusText.Text = "↪ Wiederholt";
        }
    }

    #endregion

    #region Zoom (Issue #25)

    private void SetupZoom()
    {
        _zoomManager.ZoomChanged += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                ZoomLabel.Text = $"{e.NewZoomPercentage:F0}%";
                _zoomManager.ApplyZoom(MainCanvas);
            });
        };

        BtnZoomIn.Click += (s, e) => _zoomManager.ZoomIn();
        BtnZoomOut.Click += (s, e) => _zoomManager.ZoomOut();
        BtnZoomReset.Click += (s, e) => _zoomManager.ResetZoom();

        // Ctrl+MouseWheel zoom
        _zoomManager.AttachMouseWheelZoom(MainCanvas);
    }

    #endregion

    #region Theme (Issue #8)

    private void SetupTheme()
    {
        _currentTheme = App.Config.Theme == "dark" ? Theme.Dark
                      : App.Config.Theme == "light" ? Theme.Light
                      : Theme.System;
        ApplyTheme(_currentTheme);

        BtnTheme.Click += (s, e) =>
        {
            // Cycle: System → Light → Dark → System
            _currentTheme = _currentTheme switch
            {
                Theme.System => Theme.Light,
                Theme.Light => Theme.Dark,
                _ => Theme.System
            };
            ApplyTheme(_currentTheme);
            App.Config.Theme = _currentTheme.ToString().ToLower();
            App.Config.Save();
        };
    }

    private void ApplyTheme(Theme theme)
    {
        var colors = ThemeManager.GetCurrentColors(theme);
        _themeManager.ApplyTheme(this, theme);

        // Apply to specific named elements
        TopBar.Background = new SolidColorBrush(colors.TopBarBg);
        LeftToolbar.Background = new SolidColorBrush(colors.PanelBg);
        RightPanel.Background = new SolidColorBrush(colors.PanelBg);
        AppTitle.Foreground = new SolidColorBrush(colors.Foreground);
        ZoomLabel.Foreground = new SolidColorBrush(colors.Foreground);

        // Canvas stays white always (better for writing)
        MainCanvas.Background = System.Windows.Media.Brushes.White;

        // Update all ToolButtons
        foreach (var btn in FindVisualChildren<Button>(this))
        {
            if (btn.Style == (Style)FindResource("ToolButton"))
            {
                btn.Foreground = new SolidColorBrush(colors.Foreground);
            }
        }

        // Update TextBoxes
        foreach (var tx in FindVisualChildren<TextBox>(this))
        {
            if (tx.Name == "RecognizedText" || tx.Name == "MathResult")
            {
                tx.Background = new SolidColorBrush(colors.Background);
                tx.Foreground = tx.Name == "MathResult"
                    ? new SolidColorBrush(colors.Foreground == System.Windows.Media.Colors.White ? System.Windows.Media.Colors.LightGreen : System.Windows.Media.Colors.DarkGreen)
                    : new SolidColorBrush(colors.Foreground);
                tx.BorderBrush = new SolidColorBrush(colors.Border);
            }
        }

        // Version label
        VersionLabel.Foreground = new SolidColorBrush(colors.Foreground == System.Windows.Media.Colors.White ? System.Windows.Media.Colors.Gray : System.Windows.Media.Colors.DarkGray);

        // Status texts
        StatusText.Foreground = new SolidColorBrush(colors.Foreground == System.Windows.Media.Colors.White ? System.Windows.Media.Colors.Gray : System.Windows.Media.Colors.DarkGray);
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

    #endregion

    #region Page Templates (Issue #17)

    private void SetupTemplateCombo()
    {
        TemplateCombo.SelectedIndex = 0; // Blank
        TemplateCombo.SelectionChanged += TemplateCombo_SelectionChanged;
    }

    private void TemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        if (TemplateCombo.SelectedIndex < 0) return;

        _currentTemplate = (PageTemplateType)TemplateCombo.SelectedIndex;
        var brush = PageTemplate.GetBackgroundBrush(_currentTemplate);
        MainCanvas.Background = brush;

        StatusText.Text = _currentTemplate switch
        {
            PageTemplateType.Blank => "📄 Blanko",
            PageTemplateType.LinedWide => "📄 Liniert (breit)",
            PageTemplateType.LinedNarrow => "📄 Liniert (schmal)",
            PageTemplateType.GridSmall => "📄 Kariert (klein)",
            PageTemplateType.GridMedium => "📄 Kariert (mittel)",
            PageTemplateType.GridLarge => "📄 Kariert (groß)",
            PageTemplateType.DotGridSmall => "📄 Punktiert (klein)",
            PageTemplateType.DotGridMedium => "📄 Punktiert (mittel)",
            PageTemplateType.DotGridLarge => "📄 Punktiert (groß)",
            PageTemplateType.CornellNotes => "📄 Cornell Notes",
            PageTemplateType.Isometric => "📄 Isometrisch",
            _ => "📄 Blanko"
        };
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
        BtnBlack.Click += (s, e) => SetColor(System.Windows.Media.Colors.Black, BtnBlack);
        BtnBlue.Click += (s, e) => SetColor(System.Windows.Media.Colors.Blue, BtnBlue);
        BtnRed.Click += (s, e) => SetColor(System.Windows.Media.Colors.Red, BtnRed);
        BtnGreen.Click += (s, e) => SetColor(System.Windows.Media.Colors.Green, BtnGreen);

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
                _redoStack.Clear();
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
        MainCanvas.EditingMode = mode;
        if (mode == InkCanvasEditingMode.Ink)
        {
            MainCanvas.DefaultDrawingAttributes.Color = _currentColor;
            MainCanvas.DefaultDrawingAttributes.Width = _currentSize;
            MainCanvas.DefaultDrawingAttributes.Height = _currentSize;
            MainCanvas.DefaultDrawingAttributes.IsHighlighter = false;
        }
        var allToolBtns = new[] { BtnPen, BtnHighlighter, BtnEraser, BtnSelect, BtnLine, BtnRect, BtnCircle };
        foreach (var btn in allToolBtns)
            btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45));
        activeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));
        _currentTool = mode;
    }

    private void SetHighlighter(Button activeBtn)
    {
        MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
        MainCanvas.DefaultDrawingAttributes.Color = System.Windows.Media.Colors.Yellow;
        MainCanvas.DefaultDrawingAttributes.Width = 16;
        MainCanvas.DefaultDrawingAttributes.Height = 16;
        MainCanvas.DefaultDrawingAttributes.IsHighlighter = true;
        var allToolBtns = new[] { BtnPen, BtnHighlighter, BtnEraser, BtnSelect, BtnLine, BtnRect, BtnCircle };
        foreach (var btn in allToolBtns)
            btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45));
        activeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));
    }

    private void SetColor(System.Windows.Media.Color color, Button activeBtn)
    {
        _currentColor = color;
        MainCanvas.DefaultDrawingAttributes.Color = color;
        MainCanvas.DefaultDrawingAttributes.IsHighlighter = false;
        MainCanvas.DefaultDrawingAttributes.Width = _currentSize;
        MainCanvas.DefaultDrawingAttributes.Height = _currentSize;
        foreach (var btn in new[] { BtnBlack, BtnBlue, BtnRed, BtnGreen })
            btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45));
        activeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));
    }

    private void SetSize(double size, Button activeBtn)
    {
        _currentSize = size;
        MainCanvas.DefaultDrawingAttributes.Width = size;
        MainCanvas.DefaultDrawingAttributes.Height = size;
        MainCanvas.DefaultDrawingAttributes.IsHighlighter = false;
        foreach (var btn in new[] { BtnThin, BtnMedium, BtnThick })
            btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45));
        activeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));
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

    #region Input Mode (Issue #27)

    private void SetupInputMode()
    {
        _inputModeManager = new InputModeManager(MainCanvas, CanvasScroll);
        _inputModeManager.ModeChanged += (s, e) =>
        {
            BtnInputMode.Content = _inputModeManager.GetModeEmoji();
            BtnInputMode.ToolTip = _inputModeManager.GetModeDescription();
            StatusText.Text = _inputModeManager.GetModeDescription();
        };

        // Initial button state
        BtnInputMode.Content = _inputModeManager.GetModeEmoji();
        BtnInputMode.ToolTip = _inputModeManager.GetModeDescription();

        // Cycle input mode on button click
        BtnInputMode.Click += (s, e) => _inputModeManager.CycleMode();
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
            var bitmap = RenderStrokesToBitmap();
            bitmap.Save(pngPath, ImageFormat.Png);

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
            var noteData = new
            {
                Version = App.Version,
                Template = _currentTemplate.ToString(),
                Theme = _currentTheme.ToString(),
                Zoom = _zoomManager.ZoomLevel,
                Strokes = strokesData
            };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(noteData, new JsonSerializerOptions { WriteIndented = true }));

            StatusText.Text = $"✓ Gespeichert: {filename}.png + .json";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✗ Speichern fehlgeschlagen: {ex.Message}";
        }
    }

    #endregion

    #region Auto-Update

    private void SetupAutoUpdater()
    {
        _autoUpdater.Channel = "prerelease"; // Alpha phase – include prereleases

        // Manual check button
        BtnCheckUpdate.Click += async (s, e) =>
        {
            StatusText.Text = "🔄 Prüfe auf Updates...";
            try
            {
                await _autoUpdater.CheckForUpdatesAsync();
                if (_autoUpdater.UpdateAvailable && _autoUpdater.DownloadUrl != null)
                {
                    StatusText.Text = $"📥 Update verfügbar: v{_autoUpdater.LatestVersion} – Lade herunter...";
                    var tempPath = Path.Combine(Path.GetTempPath(), $"FlipsiInk_Setup_{_autoUpdater.LatestVersion}.exe");
                    await _autoUpdater.DownloadUpdateAsync(_autoUpdater.DownloadUrl, tempPath, new Progress<double>(p =>
                    {
                        StatusText.Text = $"📥 Download: {p:P0}";
                    }));
                    StatusText.Text = "📦 Installiere Update...";
                    _autoUpdater.InstallUpdate(tempPath);
                }
                else
                {
                    StatusText.Text = "✅ FlipsiInk ist aktuell.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"✗ Update-Prüfung fehlgeschlagen: {ex.Message}";
            }
        };

        // Auto-check on startup
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(5000); // Wait 5s after startup
                await _autoUpdater.CheckForUpdatesAsync();
                if (_autoUpdater.UpdateAvailable)
                {
                    Dispatcher.Invoke(() => StatusText.Text = $"🔄 Update verfügbar: v{_autoUpdater.LatestVersion}");
                }
            }
            catch { }
        });

        // Periodic check every 15 minutes
        _updateCheckTimer = new System.Threading.Timer(async _ =>
        {
            try
            {
                await _autoUpdater.CheckForUpdatesAsync();
                if (_autoUpdater.UpdateAvailable && _autoUpdater.DownloadUrl != null)
                {
                    Dispatcher.Invoke(() => StatusText.Text = $"🔄 Update verfügbar: v{_autoUpdater.LatestVersion}");
                    // Auto-download and install
                    var tempPath = Path.Combine(Path.GetTempPath(), $"FlipsiInk_Setup_{_autoUpdater.LatestVersion}.exe");
                    await _autoUpdater.DownloadUpdateAsync(_autoUpdater.DownloadUrl, tempPath, null);
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = $"📦 Installiere v{_autoUpdater.LatestVersion}...";
                        _autoUpdater.InstallUpdate(tempPath);
                    });
                }
            }
            catch { }
        }, null, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
    }

    #endregion

    #region Settings

    private void OpenSettings()
    {
        // TODO: Issue #6 – Full settings window
        StatusText.Text = "⚙️ Einstellungen kommen bald...";
    }

    #endregion

    #region Keyboard Shortcuts

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            Undo(); e.Handled = true;
        }
        else if ((e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) ||
                 (e.Key == Key.Z && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
        {
            Redo(); e.Handled = true;
        }
        else if (e.Key == Key.R && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            _ = RecognizeText(); e.Handled = true;
        }
        else if (e.Key == Key.M && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            _ = RecognizeAndCalculate(); e.Handled = true;
        }
        else if (e.Key == Key.Add && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _zoomManager.ZoomIn(); e.Handled = true;
        }
        else if (e.Key == Key.Subtract && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _zoomManager.ZoomOut(); e.Handled = true;
        }
        else if (e.Key == Key.D0 && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _zoomManager.ResetZoom(); e.Handled = true;
        }
    }

    #endregion
}