// FlipsiInk - Handschrift-Notizen-App für WPF .NET 8
// Copyright (C) 2025 Fabian Kirchweger
//
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

using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace FlipsiInk;

/// <summary>
/// Werkzeug für Lasso-Auswahl und Verschieben von Strokes auf dem InkCanvas.
/// Bietet Methoden zum Auswählen, Kopieren, Verschieben, Skalieren und
/// Modifizieren von Stroke-Auswahlen.
/// </summary>
public class LassoSelectionTool
{
    /// <summary>Gibt an, ob der Lasso-Auswahlmodus aktiv ist.</summary>
    public bool IsActive { get; set; }

    /// <summary>Aktuelle Lasso-Punkte, die den Auswahlbereich definieren.</summary>
    public Point[] LassoPoints { get; set; } = [];

    /// <summary>Aktuell ausgewählte Strokes.</summary>
    public StrokeCollection SelectedStrokes { get; set; } = [];

    /// <summary>
    /// Findet alle Strokes, deren StylusPoints vollständig innerhalb des
    /// Lasso-Polygons liegen.
    /// </summary>
    /// <param name="allStrokes">Alle Strokes auf dem Canvas.</param>
    /// <param name="lassoPoints">Punkte des Lasso-Polygons.</param>
    /// <returns>Strokes innerhalb des Lasso-Bereichs.</returns>
    public StrokeCollection GetSelectedStrokes(StrokeCollection allStrokes, Point[] lassoPoints)
    {
        var result = new StrokeCollection();
        if (lassoPoints.Length < 3)
            return result;

        foreach (Stroke stroke in allStrokes)
        {
            // Prüfe ob mindestens ein Punkt des Strokes im Lasso liegt
            bool anyInside = false;
            foreach (StylusPoint sp in stroke.StylusPoints)
            {
                if (IsPointInLasso(new Point(sp.X, sp.Y), lassoPoints))
                {
                    anyInside = true;
                    break;
                }
            }
            if (anyInside)
                result.Add(stroke);
        }
        return result;
    }

    /// <summary>
    /// Wählt alle Strokes aus, die sich innerhalb des angegebenen Rechtecks befinden.
    /// </summary>
    /// <param name="allStrokes">Alle Strokes auf dem Canvas.</param>
    /// <param name="rect">Auswahlrechteck.</param>
    /// <returns>Strokes innerhalb des Rechtecks.</returns>
    public StrokeCollection GetStrokesInRect(StrokeCollection allStrokes, Rect rect)
    {
        var result = new StrokeCollection();
        foreach (Stroke stroke in allStrokes)
        {
            Rect bounds = stroke.GetBounds();
            if (rect.Contains(bounds) || rect.IntersectsWith(bounds))
                result.Add(stroke);
        }
        return result;
    }

    /// <summary>
    /// Erstellt eine Kopie der ausgewählten Strokes.
    /// </summary>
    /// <param name="selected">Die zu kopierenden Strokes.</param>
    /// <returns>Eine tiefe Kopie der Strokes.</returns>
    public StrokeCollection CopySelection(StrokeCollection selected)
    {
        var result = new StrokeCollection();
        foreach (Stroke stroke in selected)
        {
            result.Add(stroke.Clone());
        }
        return result;
    }

    /// <summary>
    /// Fügt Strokes aus der Zwischenablage mit einem Offset ein.
    /// </summary>
    /// <param name="clipboard">Die einzufügenden Strokes.</param>
    /// <param name="offset">Versatz für die Einfügeposition.</param>
    /// <returns>Die eingefügten Strokes mit angewandtem Offset.</returns>
    public StrokeCollection PasteSelection(StrokeCollection clipboard, Point offset)
    {
        var result = new StrokeCollection();
        foreach (Stroke stroke in clipboard)
        {
            Stroke copy = stroke.Clone();
            Matrix m = new Matrix();
            m.Translate(offset.X, offset.Y);
            copy.Transform(m, false);
            result.Add(copy);
        }
        return result;
    }

    /// <summary>
    /// Löscht die ausgewählten Strokes aus der gesamten Sammlung.
    /// </summary>
    /// <param name="allStrokes">Alle Strokes auf dem Canvas.</param>
    /// <param name="selected">Die zu löschenden Strokes.</param>
    /// <returns>Die verbleibenden Strokes ohne die Auswahl.</returns>
    public StrokeCollection DeleteSelection(StrokeCollection allStrokes, StrokeCollection selected)
    {
        var result = new StrokeCollection();
        foreach (Stroke stroke in allStrokes)
        {
            if (!selected.Contains(stroke))
                result.Add(stroke);
        }
        return result;
    }

    /// <summary>
    /// Verschiebt die ausgewählten Strokes um den angegebenen Vektor.
    /// </summary>
    /// <param name="selected">Die zu verschiebenden Strokes.</param>
    /// <param name="offset">Der Verschiebevektor.</param>
    /// <returns>Die verschobenen Strokes.</returns>
    public StrokeCollection MoveSelection(StrokeCollection selected, Vector offset)
    {
        var result = new StrokeCollection();
        foreach (Stroke stroke in selected)
        {
            Stroke copy = stroke.Clone();
            Matrix m = new Matrix();
            m.Translate(offset.X, offset.Y);
            copy.Transform(m, false);
            result.Add(copy);
        }
        return result;
    }

    /// <summary>
    /// Skaliert die ausgewählten Strokes um den angegebenen Faktor.
    /// </summary>
    /// <param name="selected">Die zu skalierenden Strokes.</param>
    /// <param name="scaleFactor">Skalierungsfaktor (1.0 = keine Änderung).</param>
    /// <param name="center">Der Mittelpunkt der Skalierung.</param>
    /// <returns>Die skalierten Strokes.</returns>
    public StrokeCollection ScaleSelection(StrokeCollection selected, double scaleFactor, Point center)
    {
        var result = new StrokeCollection();
        foreach (Stroke stroke in selected)
        {
            Stroke copy = stroke.Clone();
            Matrix m = new Matrix();
            m.Translate(-center.X, -center.Y);
            m.Scale(scaleFactor, scaleFactor);
            m.Translate(center.X, center.Y);
            copy.Transform(m, false);
            result.Add(copy);
        }
        return result;
    }

    /// <summary>
    /// Ändert die Zeichenfarbe der ausgewählten Strokes.
    /// </summary>
    /// <param name="selected">Die zu ändernden Strokes.</param>
    /// <param name="newColor">Die neue Farbe.</param>
    /// <returns>Die Strokes mit geänderter Farbe.</returns>
    public StrokeCollection ChangeColor(StrokeCollection selected, Color newColor)
    {
        var result = new StrokeCollection();
        foreach (Stroke stroke in selected)
        {
            Stroke copy = stroke.Clone();
            DrawingAttributes da = copy.DrawingAttributes.Clone();
            da.Color = newColor;
            copy.DrawingAttributes = da;
            result.Add(copy);
        }
        return result;
    }

    /// <summary>
    /// Ändert die Stiftstärke der ausgewählten Strokes.
    /// </summary>
    /// <param name="selected">Die zu ändernden Strokes.</param>
    /// <param name="newSize">Die neue Stiftstärke.</param>
    /// <returns>Die Strokes mit geänderter Stiftstärke.</returns>
    public StrokeCollection ChangeSize(StrokeCollection selected, double newSize)
    {
        var result = new StrokeCollection();
        foreach (Stroke stroke in selected)
        {
            Stroke copy = stroke.Clone();
            DrawingAttributes da = copy.DrawingAttributes.Clone();
            da.Width = newSize;
            da.Height = newSize;
            copy.DrawingAttributes = da;
            result.Add(copy);
        }
        return result;
    }

    /// <summary>
    /// Bestimmt, ob ein Punkt innerhalb des Lasso-Polygons liegt.
    /// Verwendet den Ray-Casting-Algorithmus.
    /// </summary>
    /// <param name="point">Der zu prüfende Punkt.</param>
    /// <param name="lassoPoints">Die Punkte des Lasso-Polygons.</param>
    /// <returns>True, wenn der Punkt im Polygon liegt.</returns>
    public bool IsPointInLasso(Point point, Point[] lassoPoints)
    {
        if (lassoPoints.Length < 3)
            return false;

        int n = lassoPoints.Length;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double yi = lassoPoints[i].Y;
            double yj = lassoPoints[j].Y;
            double xi = lassoPoints[i].X;
            double xj = lassoPoints[j].X;

            if (((yi > point.Y) != (yj > point.Y)) &&
                (point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// Glättet die Lasso-Kurve durch wiederholtes Mitteln benachbarter Punkte.
    /// </summary>
    /// <param name="rawPoints">Die rohen Lasso-Punkte.</param>
    /// <param name="iterations">Anzahl der Glättungs-Durchläufe.</param>
    /// <returns>Die geglätteten Punkte.</returns>
    public Point[] SmoothLasso(Point[] rawPoints, int iterations = 3)
    {
        if (rawPoints.Length < 3 || iterations <= 0)
            return rawPoints;

        var current = (Point[])rawPoints.Clone();

        for (int iter = 0; iter < iterations; iter++)
        {
            var smoothed = new Point[current.Length];
            smoothed[0] = current[0];
            smoothed[current.Length - 1] = current[current.Length - 1];

            for (int i = 1; i < current.Length - 1; i++)
            {
                smoothed[i] = new Point(
                    (current[i - 1].X + current[i].X + current[i + 1].X) / 3.0,
                    (current[i - 1].Y + current[i].Y + current[i + 1].Y) / 3.0);
            }
            current = smoothed;
        }
        return current;
    }

    /// <summary>
    /// Erstellt ein DrawingVisual, das die Lasso-Umrandung als gestrichelte,
    /// animierte Linie darstellt.
    /// </summary>
    /// <param name="points">Die Lasso-Punkte.</param>
    /// <param name="color">Die Farbe der Umrandung.</param>
    /// <returns>Ein DrawingVisual mit der Lasso-Darstellung.</returns>
    public DrawingVisual CreateLassoVisual(Point[] points, Color color)
    {
        var visual = new DrawingVisual();
        if (points.Length < 2)
            return visual;

        var drawingContext = visual.RenderOpen();
        var pen = new Pen(new SolidColorBrush(color), 1.5)
        {
            DashStyle = new DashStyle([4.0, 4.0], 0.0)
        };
        pen.Freeze();

        // Pfadgeometrie für die Lasso-Linie
        var geometry = new PathGeometry();
        var figures = new PathFigureCollection();

        var figure = new PathFigure { StartPoint = points[0] };
        for (int i = 1; i < points.Length; i++)
        {
            figure.Segments.Add(new LineSegment(points[i], true));
        }
        // Polygon schließen
        figure.IsClosed = true;
        figures.Add(figure);
        geometry.Figures = figures;
        geometry.Freeze();

        drawingContext.DrawGeometry(null, pen, geometry);

        // Halbm-transparente Füllung für den Auswahlbereich
        var fillBrush = new SolidColorBrush(color) { Opacity = 0.1 };
        fillBrush.Freeze();
        drawingContext.DrawGeometry(fillBrush, null, geometry);

        drawingContext.Close();
        return visual;
    }
}