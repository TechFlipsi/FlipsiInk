// PageTemplate.cs - Seitenvorlagen für FlipsiInk
// Copyright (C) 2025 Fabian Kirchweger / TechFlipsi
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

#nullable enable

using System;
using System.Windows;
using System.Windows.Media;

namespace FlipsiInk
{
    /// <summary>
    /// Verfügbare Seitenvorlagen-Typen.
    /// </summary>
    public enum PageTemplateType
    {
        /// <summary>Blanko – weiße Seite ohne Muster.</summary>
        Blank,
        /// <summary>Liniert – breite Zeilen (28px Abstand).</summary>
        LinedWide,
        /// <summary>Liniert – schmale Zeilen (18px Abstand).</summary>
        LinedNarrow,
        /// <summary>Kariert – kleines Raster (20px).</summary>
        GridSmall,
        /// <summary>Kariert – mittleres Raster (25px).</summary>
        GridMedium,
        /// <summary>Kariert – großes Raster (30px).</summary>
        GridLarge,
        /// <summary>Punktiert – kleines Dot-Grid (20px).</summary>
        DotGridSmall,
        /// <summary>Punktiert – mittleres Dot-Grid (25px).</summary>
        DotGridMedium,
        /// <summary>Punktiert – großes Dot-Grid (30px).</summary>
        DotGridLarge,
        /// <summary>Cornell Notes – liniert mit Trennlinien.</summary>
        CornellNotes,
        /// <summary>Isometrisches Gitter – 60° Winkel.</summary>
        Isometric
    }

    /// <summary>
    /// Stellt Seitenvorlagen als <see cref="DrawingBrush"/> zur Verfügung.
    /// </summary>
    public static class PageTemplate
    {
        // Standard-Farben
        private const string DefaultLineColor = "#E0E0E0";
        private const string DefaultLinedColor = "#C0D8F0";

        /// <summary>
        /// Erzeugt einen <see cref="DrawingBrush"/> für die gewünschte Seitenvorlage.
        /// </summary>
        /// <param name="type">Die gewünschte Seitenvorlage.</param>
        /// <param name="lineWidth">Linienstärke (Standard: 1).</param>
        /// <param name="lineColor">Linienfarbe als Hex-String (Standard: #E0E0E0 für Gitter, #C0D8F0 für liniert).</param>
        /// <returns>Ein kachelbarer <see cref="DrawingBrush"/> oder <c>null</c> für Blanko.</returns>
        public static Brush GetBackgroundBrush(PageTemplateType type, double lineWidth = 1, string lineColor = "#E0E0E0")
        {
            return type switch
            {
                PageTemplateType.Blank => Brushes.White,
                PageTemplateType.LinedWide => CreateLinedBrush(28, lineWidth, lineColor is "#E0E0E0" ? DefaultLinedColor : lineColor),
                PageTemplateType.LinedNarrow => CreateLinedBrush(18, lineWidth, lineColor is "#E0E0E0" ? DefaultLinedColor : lineColor),
                PageTemplateType.GridSmall => CreateGridBrush(20, lineWidth, lineColor),
                PageTemplateType.GridMedium => CreateGridBrush(25, lineWidth, lineColor),
                PageTemplateType.GridLarge => CreateGridBrush(30, lineWidth, lineColor),
                PageTemplateType.DotGridSmall => CreateDotGridBrush(20, lineColor),
                PageTemplateType.DotGridMedium => CreateDotGridBrush(25, lineColor),
                PageTemplateType.DotGridLarge => CreateDotGridBrush(30, lineColor),
                PageTemplateType.CornellNotes => CreateCornellBrush(lineWidth, lineColor is "#E0E0E0" ? DefaultLinedColor : lineColor),
                PageTemplateType.Isometric => CreateIsometricBrush(lineWidth, lineColor),
                _ => Brushes.White
            };
        }

        /// <summary>
        /// Liniert: horizontale Linien mit gleichem Abstand.
        /// </summary>
        private static DrawingBrush CreateLinedBrush(double spacing, double lineWidth, string color)
        {
            var pen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), lineWidth);
            var geometry = new LineGeometry(new Point(0, spacing), new Point(spacing * 2, spacing));
            var drawing = new GeometryDrawing(null, pen, geometry);
            var brush = new DrawingBrush(drawing)
            {
                Viewport = new Rect(0, 0, spacing * 2, spacing),
                Viewbox = new Rect(0, 0, spacing * 2, spacing),
                TileMode = TileMode.Tile,
                Stretch = Stretch.None
            };
            return brush;
        }

        /// <summary>
        /// Kariert: horizontale und vertikale Linien.
        /// </summary>
        private static DrawingBrush CreateGridBrush(double spacing, double lineWidth, string color)
        {
            var pen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), lineWidth);
            var group = new DrawingGroup();
            // Horizontale Linie
            group.Children.Add(new GeometryDrawing(null, pen,
                new LineGeometry(new Point(0, spacing), new Point(spacing, spacing))));
            // Vertikale Linie
            group.Children.Add(new GeometryDrawing(null, pen,
                new LineGeometry(new Point(spacing, 0), new Point(spacing, spacing))));
            var brush = new DrawingBrush(group)
            {
                Viewport = new Rect(0, 0, spacing, spacing),
                Viewbox = new Rect(0, 0, spacing, spacing),
                TileMode = TileMode.Tile,
                Stretch = Stretch.None
            };
            return brush;
        }

        /// <summary>
        /// Punktiert: kleine Kreise im Raster.
        /// </summary>
        private static DrawingBrush CreateDotGridBrush(double spacing, string color)
        {
            var dotBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            // Kleiner Kreis als Punkt
            var dotGeometry = new EllipseGeometry(new Point(spacing / 2, spacing / 2), 1.5, 1.5);
            var drawing = new GeometryDrawing(dotBrush, null, dotGeometry);
            var brush = new DrawingBrush(drawing)
            {
                Viewport = new Rect(0, 0, spacing, spacing),
                Viewbox = new Rect(0, 0, spacing, spacing),
                TileMode = TileMode.Tile,
                Stretch = Stretch.None
            };
            return brush;
        }

        /// <summary>
        /// Cornell Notes: liniert (28px) mit vertikaler Linie bei ~1/3 Breite
        /// und horizontaler Linie bei ~2/3 Höhe.
        /// Tile-Größe repräsentiert eine DIN-A4-ähnliche Kachel.
        /// </summary>
        private static DrawingBrush CreateCornellBrush(double lineWidth, string color)
        {
            const double tileW = 793; // ~A4 Breite in px bei 96dpi
            const double tileH = 1122; // ~A4 Höhe
            const double lineSpacing = 28;
            const double verticalX = 250; // ~1/3
            const double horizontalY = 700; // ~2/3

            var pen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), lineWidth);
            var redPen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), lineWidth * 1.5);
            var group = new DrawingGroup();

            // Horizontale Linien
            for (double y = lineSpacing; y < tileH; y += lineSpacing)
            {
                group.Children.Add(new GeometryDrawing(null, pen,
                    new LineGeometry(new Point(0, y), new Point(tileW, y))));
            }

            // Vertikale Trennlinie (links)
            group.Children.Add(new GeometryDrawing(null, redPen,
                new LineGeometry(new Point(verticalX, 0), new Point(verticalX, horizontalY))));

            // Horizontale Trennlinie
            group.Children.Add(new GeometryDrawing(null, redPen,
                new LineGeometry(new Point(0, horizontalY), new Point(tileW, horizontalY))));

            var brush = new DrawingBrush(group)
            {
                Viewport = new Rect(0, 0, tileW, tileH),
                Viewbox = new Rect(0, 0, tileW, tileH),
                TileMode = TileMode.Tile,
                Stretch = Stretch.None
            };
            return brush;
        }

        /// <summary>
        /// Isometrisches Gitter: drei Liniengruppen bei 0°, 60° und 120°.
        /// </summary>
        private static DrawingBrush CreateIsometricBrush(double lineWidth, string color)
        {
            const double size = 30; // Seitenlänge der Rauten
            double h = size * Math.Sqrt(3) / 2; // Höhe der Raute

            var pen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), lineWidth);
            var group = new DrawingGroup();

            // Horizontale Linie (0°)
            group.Children.Add(new GeometryDrawing(null, pen,
                new LineGeometry(new Point(0, h), new Point(size * 2, h))));

            // Linie 60° nach oben rechts
            group.Children.Add(new GeometryDrawing(null, pen,
                new LineGeometry(new Point(0, h), new Point(size, 0))));

            // Linie 120° nach oben links (bzw. 60° nach oben links)
            group.Children.Add(new GeometryDrawing(null, pen,
                new LineGeometry(new Point(size * 2, h), new Point(size, 0))));

            // Ergänzungslinien für vollständige Raute
            group.Children.Add(new GeometryDrawing(null, pen,
                new LineGeometry(new Point(0, h), new Point(size, h * 2))));
            group.Children.Add(new GeometryDrawing(null, pen,
                new LineGeometry(new Point(size * 2, h), new Point(size, h * 2))));
            group.Children.Add(new GeometryDrawing(null, pen,
                new LineGeometry(new Point(size, 0), new Point(size * 2, h * 2 + h))));

            var brush = new DrawingBrush(group)
            {
                Viewport = new Rect(0, 0, size * 2, h * 2),
                Viewbox = new Rect(0, 0, size * 2, h * 2),
                TileMode = TileMode.Tile,
                Stretch = Stretch.None
            };
            return brush;
        }
    }
}