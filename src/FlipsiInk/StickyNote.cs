// FlipsiInk - Sticky Notes
// Copyright (C) 2025 FlipsiInk Contributors
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlipsiInk
{
    /// <summary>Farben für Klebezettel</summary>
    public enum StickyNoteColor
    {
        Yellow,
        Blue,
        Green,
        Pink,
        Purple
    }

    /// <summary>
    /// Sticky Note / Klebezettel – erbt von Border für direkte WPF-Darstellung.
    /// </summary>
    public class StickyNote : Border
    {
        #region Konstanten

        /// <summary>Farbwerte für die Klebezettel-Farben</summary>
        private static readonly Dictionary<StickyNoteColor, string> ColorBrushes = new()
        {
            { StickyNoteColor.Yellow, "#FFF9C4" },
            { StickyNoteColor.Blue, "#BBDEFB" },
            { StickyNoteColor.Green, "#C8E6C9" },
            { StickyNoteColor.Pink, "#F8BBD0" },
            { StickyNoteColor.Purple, "#E1BEE7" }
        };

        #endregion

        #region Private Felder

        private readonly TextBox _editTextBox;
        private readonly TextBlock _displayText;
        private readonly StackPanel _contentPanel;
        private readonly Border _titleBar;
        private double _fullWidth = 200;
        private double _fullHeight = 150;

        #endregion

        #region Öffentliche Eigenschaften

        /// <summary>Text auf dem Klebezettel</summary>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(StickyNote),
                new PropertyMetadata(string.Empty, OnTextChanged));

        /// <summary>Farbe des Klebezettels</summary>
        public StickyNoteColor NoteColor
        {
            get => (StickyNoteColor)GetValue(NoteColorProperty);
            set => SetValue(NoteColorProperty, value);
        }

        public static readonly DependencyProperty NoteColorProperty =
            DependencyProperty.Register(nameof(NoteColor), typeof(StickyNoteColor), typeof(StickyNote),
                new PropertyMetadata(StickyNoteColor.Yellow, OnColorChanged));

        /// <summary>Position auf dem Canvas</summary>
        public Point NotePosition
        {
            get => (Point)GetValue(NotePositionProperty);
            set => SetValue(NotePositionProperty, value);
        }

        public static readonly DependencyProperty NotePositionProperty =
            DependencyProperty.Register(nameof(NotePosition), typeof(Point), typeof(StickyNote),
                new PropertyMetadata(new Point(0, 0)));

        /// <summary>Minimiert (nur Titel sichtbar)</summary>
        public bool IsMinimized { get; private set; } = false;

        /// <summary>Erstellungsdatum</summary>
        public DateTime CreatedAt { get; } = DateTime.Now;

        /// <summary>Eindeutige ID</summary>
        public Guid Id { get; } = Guid.NewGuid();

        #endregion

        #region Konstruktor

        public StickyNote()
        {
            _fullWidth = 200;
            _fullHeight = 150;

            // Titelzeile
            _titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                Padding = new Thickness(4, 2, 4, 2),
                CornerRadius = new CornerRadius(4, 4, 0, 0)
            };

            var titlePanel = new DockPanel();
            var titleText = new TextBlock
            {
                Text = "📝 Notiz",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.DarkGray)
            };
            DockPanel.SetDock(titleText, Dock.Left);
            titlePanel.Children.Add(titleText);
            _titleBar.Child = titlePanel;

            // Anzeigetext
            _displayText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Margin = new Thickness(6, 4, 6, 4),
                VerticalAlignment = VerticalAlignment.Top
            };

            // Bearbeitungs-TextBox (anfangs unsichtbar)
            _editTextBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Margin = new Thickness(6, 4, 6, 4),
                Visibility = Visibility.Collapsed,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent
            };

            _editTextBox.LostFocus += (s, e) => EndEdit();
            _editTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                    EndEdit();
            };

            // Inhalt-Panel
            _contentPanel = new StackPanel();
            _contentPanel.Children.Add(_titleBar);
            _contentPanel.Children.Add(_displayText);
            _contentPanel.Children.Add(_editTextBox);

            // Border-Styling
            Child = _contentPanel;
            CornerRadius = new CornerRadius(6);
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
            BorderThickness = new Thickness(1);
            Padding = new Thickness(0);
            Width = _fullWidth;
            Height = _fullHeight;

            ApplyColor();
        }

        #endregion

        #region Öffentliche Methoden

        /// <summary>Text bearbeiten – TextBox im Klebezettel öffnen</summary>
        public void Edit()
        {
            _displayText.Visibility = Visibility.Collapsed;
            _editTextBox.Visibility = Visibility.Visible;
            _editTextBox.Text = Text;
            _editTextBox.Focus();
            _editTextBox.SelectAll();
        }

        /// <summary>Minimieren – nur Titel sichtbar</summary>
        public void Minimize()
        {
            if (IsMinimized) return;
            IsMinimized = true;
            _fullWidth = Width;
            _fullHeight = Height;
            _displayText.Visibility = Visibility.Collapsed;
            _editTextBox.Visibility = Visibility.Collapsed;
            Height = double.NaN; // Auto-Höhe
            Width = _fullWidth;
        }

        /// <summary>Wiederherstellen – volle Größe</summary>
        public void Restore()
        {
            if (!IsMinimized) return;
            IsMinimized = false;
            _displayText.Visibility = Visibility.Visible;
            Height = _fullHeight;
            Width = _fullWidth;
        }

        /// <summary>Größe ändern</summary>
        public void Resize(double width, double height)
        {
            _fullWidth = width;
            _fullHeight = height;
            if (!IsMinimized)
            {
                Width = width;
                Height = height;
            }
        }

        #endregion

        #region Private Methoden

        private void EndEdit()
        {
            Text = _editTextBox.Text;
            _editTextBox.Visibility = Visibility.Collapsed;
            _displayText.Visibility = Visibility.Visible;
        }

        private void ApplyColor()
        {
            var hex = ColorBrushes.GetValueOrDefault(NoteColor, "#FFF9C4");
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StickyNote note)
            {
                note._displayText.Text = (string)e.NewValue;
            }
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StickyNote note)
            {
                note.ApplyColor();
            }
        }

        #endregion
    }

    /// <summary>
    /// Verwaltung aller Klebezettel – erstellen, löschen, speichern, laden.
    /// </summary>
    public class StickyNoteManager
    {
        #region Eigenschaften

        /// <summary>Alle Klebezettel</summary>
        public List<StickyNote> Notes { get; } = new();

        #endregion

        #region Methoden

        /// <summary>Neuen Klebezettel erstellen</summary>
        public StickyNote CreateNote(Point position, string text = "", StickyNoteColor color = StickyNoteColor.Yellow)
        {
            var note = new StickyNote
            {
                Text = text,
                NoteColor = color,
                NotePosition = position
            };

            InkCanvas.SetLeft(note, position.X);
            InkCanvas.SetTop(note, position.Y);

            Notes.Add(note);
            return note;
        }

        /// <summary>Klebezettel löschen</summary>
        public void DeleteNote(Guid id)
        {
            var note = Notes.Find(n => n.Id == id);
            if (note != null)
            {
                Notes.Remove(note);
            }
        }

        /// <summary>Alle Klebezettel als JSON speichern</summary>
        public void SaveNotes(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var data = new List<StickyNoteData>();
            foreach (var note in Notes)
            {
                data.Add(new StickyNoteData
                {
                    Id = note.Id,
                    Text = note.Text,
                    Color = note.NoteColor,
                    PositionX = note.NotePosition.X,
                    PositionY = note.NotePosition.Y,
                    Width = note.Width,
                    Height = note.IsMinimized ? 0 : note.Height,
                    IsMinimized = note.IsMinimized,
                    CreatedAt = note.CreatedAt
                });
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(filePath, json);
        }

        /// <summary>Klebezettel aus JSON-Datei laden</summary>
        public List<StickyNote> LoadNotes(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Notizen-Datei nicht gefunden", filePath);

            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<List<StickyNoteData>>(json);
            if (data == null) return new List<StickyNote>();

            Notes.Clear();

            foreach (var d in data)
            {
                var note = new StickyNote
                {
                    Text = d.Text,
                    NoteColor = d.Color,
                    NotePosition = new Point(d.PositionX, d.PositionY)
                };

                if (d.Width > 0 || d.Height > 0)
                    note.Resize(
                        d.Width > 0 ? d.Width : 200,
                        d.Height > 0 ? d.Height : 150);

                if (d.IsMinimized)
                    note.Minimize();

                InkCanvas.SetLeft(note, d.PositionX);
                InkCanvas.SetTop(note, d.PositionY);

                Notes.Add(note);
            }

            return Notes;
        }

        #endregion
    }

    /// <summary>
    /// Serialisierungs-Daten für Klebezettel (JSON-freundlich).
    /// </summary>
    public class StickyNoteData
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public StickyNoteColor Color { get; set; } = StickyNoteColor.Yellow;
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMinimized { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}