// FlipsiInk - Image Manager
// Copyright (C) 2025 FlipsiInk Contributors
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlipsiInk
{
    /// <summary>
    /// Datenklasse für ein eingefügtes Bild auf dem Canvas.
    /// </summary>
    public class CanvasImage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public Point Position { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public bool IsBackground { get; set; }
        public double OriginalWidth { get; set; }
        public double OriginalHeight { get; set; }
    }

    /// <summary>
    /// Sticker-Element mit XAML-Pfad-Geometrie.
    /// </summary>
    public class StickerItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        /// <summary>XAML Geometry-Pfad (z.B. "M10,0 L13,7 L20,7 ...")</summary>
        public string Path { get; set; } = string.Empty;
    }

    /// <summary>
    /// Verwaltet eingefügte Bilder und Sticker auf dem InkCanvas.
    /// </summary>
    public class ImageManager
    {
        #region Eigenschaften

        /// <summary>Alle eingefügten Bilder</summary>
        public Dictionary<string, CanvasImage> Images { get; } = new();

        /// <summary>Aktuell ausgewähltes Bild (null wenn keines)</summary>
        public string? SelectedImageId { get; set; }

        #endregion

        #region Bild einfügen

        /// <summary>Bild aus Datei an Position einfügen</summary>
        public void InsertImageFromFile(string filePath, Point position, InkCanvas canvas)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Bilddatei nicht gefunden", filePath);

            using var bitmap = new System.Drawing.Bitmap(filePath);
            InsertImage(bitmap, position, canvas);

            // FilePath nachträglich setzen
            var lastKey = GetLastInsertedId();
            if (lastKey != null)
            {
                Images[lastKey].FilePath = filePath;
            }
        }

        /// <summary>Bild aus Zwischenablage einfügen</summary>
        public void InsertImageFromClipboard(InkCanvas canvas)
        {
            if (!System.Windows.Clipboard.ContainsImage())
                throw new InvalidOperationException("Zwischenablage enthält kein Bild");

            var clipboardImage = System.Windows.Clipboard.GetImage();
            if (clipboardImage == null)
                throw new InvalidOperationException("Bild aus Zwischenablage konnte nicht gelesen werden");

            // Wpf BitmapSource → System.Drawing.Bitmap konvertieren
            var bitmap = BitmapSourceToDrawingBitmap(clipboardImage);
            var position = new Point(50, 50); // Standardposition
            InsertImage(bitmap, position, canvas);
        }

        /// <summary>System.Drawing.Bitmap an Position einfügen</summary>
        public void InsertImage(System.Drawing.Bitmap bitmap, Point position, InkCanvas canvas)
        {
            var canvasImage = new CanvasImage
            {
                FilePath = string.Empty,
                Position = position,
                Width = bitmap.Width,
                Height = bitmap.Height,
                Rotation = 0,
                IsBackground = false,
                OriginalWidth = bitmap.Width,
                OriginalHeight = bitmap.Height
            };

            Images[canvasImage.Id] = canvasImage;

            // WPF Image-Element erstellen und auf Canvas setzen
            var wpfImage = CreateWpfImage(bitmap, canvasImage);
            InkCanvas.SetLeft(wpfImage, position.X);
            InkCanvas.SetTop(wpfImage, position.Y);

            // Als Child auf dem Canvas hinzufügen
            canvas.Children.Add(wpfImage);
        }

        #endregion

        #region Bild-Manipulation

        /// <summary>Bild skalieren</summary>
        public void ScaleImage(string imageId, double scaleFactor)
        {
            if (!Images.TryGetValue(imageId, out var img)) return;

            img.Width = img.OriginalWidth * scaleFactor;
            img.Height = img.OriginalHeight * scaleFactor;
        }

        /// <summary>Bild drehen</summary>
        public void RotateImage(string imageId, double angle)
        {
            if (!Images.TryGetValue(imageId, out var img)) return;
            img.Rotation = (img.Rotation + angle) % 360;
        }

        /// <summary>Bild verschieben</summary>
        public void MoveImage(string imageId, Point newPosition)
        {
            if (!Images.TryGetValue(imageId, out var img)) return;
            img.Position = newPosition;
        }

        /// <summary>Bild entfernen</summary>
        public void RemoveImage(string imageId)
        {
            Images.Remove(imageId);
            if (SelectedImageId == imageId)
                SelectedImageId = null;
        }

        /// <summary>Bild als Hintergrund setzen</summary>
        public void SetImageAsBackground(string filePath, InkCanvas canvas)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Bilddatei nicht gefunden", filePath);

            using var bitmap = new System.Drawing.Bitmap(filePath);
            var canvasImage = new CanvasImage
            {
                FilePath = filePath,
                Position = new Point(0, 0),
                Width = canvas.ActualWidth,
                Height = canvas.ActualHeight,
                Rotation = 0,
                IsBackground = true,
                OriginalWidth = bitmap.Width,
                OriginalHeight = bitmap.Height
            };

            Images[canvasImage.Id] = canvasImage;

            var brush = new ImageBrush(BitmapToBitmapSource(bitmap))
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };

            canvas.Background = brush;
        }

        /// <summary>Bild zuschneiden</summary>
        public System.Drawing.Bitmap CropImage(string imageId, Rect cropRect)
        {
            if (!Images.TryGetValue(imageId, out var img))
                throw new ArgumentException("Bild nicht gefunden", nameof(imageId));

            var sourcePath = img.FilePath;
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                throw new InvalidOperationException("Quelldatei für Zuschneiden nicht verfügbar");

            using var original = new System.Drawing.Bitmap(sourcePath);
            var cropArea = new System.Drawing.Rectangle(
                (int)cropRect.X, (int)cropRect.Y,
                (int)cropRect.Width, (int)cropRect.Height);

            var cropped = original.Clone(cropArea, original.PixelFormat);
            return cropped;
        }

        /// <summary>Bild speichern</summary>
        public string SaveImage(string imageId, string outputPath)
        {
            if (!Images.TryGetValue(imageId, out var img))
                throw new ArgumentException("Bild nicht gefunden", nameof(imageId));

            var sourcePath = img.FilePath;
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                throw new InvalidOperationException("Quelldatei zum Speichern nicht verfügbar");

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.Copy(sourcePath, outputPath, overwrite: true);
            return outputPath;
        }

        #endregion

        #region Sticker-Bibliothek

        /// <summary>Liefert eingebaute Sticker (Pfeile, Sterne, Symbole) als XAML-Geometrien</summary>
        public List<StickerItem> GetBuiltInStickers()
        {
            return new List<StickerItem>
            {
                // Pfeil nach rechts
                new()
                {
                    Id = "arrow-right", Name = "Pfeil rechts", Category = "Pfeile",
                    Path = "M0,10 L15,10 L10,5 L20,10 L10,15 L15,10 Z"
                },
                // Pfeil nach links
                new()
                {
                    Id = "arrow-left", Name = "Pfeil links", Category = "Pfeile",
                    Path = "M20,10 L5,10 L10,5 L0,10 L10,15 L5,10 Z"
                },
                // Pfeil nach oben
                new()
                {
                    Id = "arrow-up", Name = "Pfeil oben", Category = "Pfeile",
                    Path = "M10,0 L10,15 L5,10 L10,20 L15,10 L10,15 Z"
                },
                // Pfeil nach unten
                new()
                {
                    Id = "arrow-down", Name = "Pfeil unten", Category = "Pfeile",
                    Path = "M10,20 L10,5 L5,10 L10,0 L15,10 L10,5 Z"
                },
                // Stern
                new()
                {
                    Id = "star", Name = "Stern", Category = "Symbole",
                    Path = "M10,0 L12.5,7.5 L20,7.5 L14,12 L16,20 L10,15 L4,20 L6,12 L0,7.5 L7.5,7.5 Z"
                },
                // Herz
                new()
                {
                    Id = "heart", Name = "Herz", Category = "Symbole",
                    Path = "M10,18 L2,10 Q0,5 5,3 Q10,0 10,6 Q10,0 15,3 Q20,5 18,10 Z"
                },
                // Häkchen
                new()
                {
                    Id = "check", Name = "Häkchen", Category = "Symbole",
                    Path = "M2,10 L8,16 L18,4"
                },
                // Kreuz
                new()
                {
                    Id = "cross", Name = "Kreuz", Category = "Symbole",
                    Path = "M4,4 L16,16 M16,4 L4,16"
                },
                // Ausrufezeichen
                new()
                {
                    Id = "exclaim", Name = "Achtung", Category = "Symbole",
                    Path = "M10,2 L10,12 M10,16 L10,18"
                },
                // Fragezeichen
                new()
                {
                    Id = "question", Name = "Frage", Category = "Symbole",
                    Path = "M6,6 Q6,2 10,2 Q14,2 14,6 Q14,10 10,10 L10,13 M10,16 L10,18"
                },
                // Daumen hoch
                new()
                {
                    Id = "thumb-up", Name = "Daumen hoch", Category = "Symbole",
                    Path = "M4,10 L4,18 L7,18 L7,10 Z M7,10 L10,2 L12,2 L12,8 L17,8 L16,18 L7,18"
                },
                // Blitz
                new()
                {
                    Id = "lightning", Name = "Blitz", Category = "Symbole",
                    Path = "M12,0 L4,10 L9,10 L8,20 L16,10 L11,10 Z"
                }
            };
        }

        #endregion

        #region Private Hilfsmethoden

        /// <summary>System.Drawing.Bitmap → BitmapSource für WPF</summary>
        private static BitmapSource BitmapToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);

            var format = bitmap.PixelFormat switch
            {
                System.Drawing.Imaging.PixelFormat.Format32bppArgb => PixelFormats.Bgra32,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb => PixelFormats.Bgr24,
                _ => PixelFormats.Bgr24
            };

            var bitmapSource = BitmapSource.Create(
                bitmap.Width, bitmap.Height,
                96, 96,
                format, null,
                bmpData.Scan0, bmpData.Stride * bitmap.Height, bmpData.Stride);

            bitmap.UnlockBits(bmpData);
            bitmapSource.Freeze();
            return bitmapSource;
        }

        /// <summary>BitmapSource → System.Drawing.Bitmap konvertieren</summary>
        private static System.Drawing.Bitmap BitmapSourceToDrawingBitmap(BitmapSource bitmapSource)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            return new System.Drawing.Bitmap(stream);
        }

        /// <summary>WPF-Image-Element aus Bitmap erstellen</summary>
        private static System.Windows.Controls.Image CreateWpfImage(System.Drawing.Bitmap bitmap, CanvasImage canvasImage)
        {
            var bitmapSource = BitmapToBitmapSource(bitmap);
            var image = new System.Windows.Controls.Image
            {
                Source = bitmapSource,
                Width = canvasImage.Width,
                Height = canvasImage.Height,
                Tag = canvasImage.Id,
                Stretch = Stretch.Uniform
            };

            // Drehung anwenden
            if (canvasImage.Rotation != 0)
            {
                image.RenderTransform = new RotateTransform(canvasImage.Rotation);
                image.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            return image;
        }

        /// <summary>ID des zuletzt eingefügten Bildes ermitteln</summary>
        private string? GetLastInsertedID()
        {
            string? lastId = null;
            foreach (var kvp in Images)
            {
                lastId = kvp.Key;
            }
            return lastId;
        }

        #endregion
    }
}