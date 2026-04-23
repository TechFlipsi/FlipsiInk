// NoteFileManager.cs - Speichern, Laden und Exportieren von Notizen
// Copyright (C) 2026 Fabian Kirchweger / TechFlipsi
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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Ink;

namespace FlipsiInk;

// ─────────────────────────── Daten-Klassen ───────────────────────────

/// <summary>
/// Serialisierbare Repräsentation einer gespeicherten Seite.
/// </summary>
public class SavedPage
{
    public Guid Id { get; set; }
    public int PageNumber { get; set; }
    public PageTemplateType Template { get; set; }
    public double Zoom { get; set; }
    public string Theme { get; set; } = "system";
    public List<SavedStroke> Strokes { get; set; } = [];
}

/// <summary>
/// Serialisierbare Repräsentation eines einzelnen Strichs.
/// </summary>
public class SavedStroke
{
    public string Color { get; set; } = "#000000";
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsHighlighter { get; set; }
    public List<SavedPoint> Points { get; set; } = [];
}

/// <summary>
/// Serialisierbarer Punkt eines Strichs.
/// </summary>
public class SavedPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public float PressureFactor { get; set; } = 0.5f;
}

// ─────────────────────────── Notebook-Datenklasse ─────────────────────

/// <summary>
/// Repräsentiert ein vollständiges Notizbuch mit Metadaten und Seiten (Datei-Export-Version).
/// </summary>
public class SavedNotebook
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Unbenannt";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<FileNotebookPage> Pages { get; set; } = [];

    /// <summary>
    /// Bequemer Zugriff: Striche der aktuellen Seite.
    /// </summary>
    public FileNotebookPage CurrentPage => Pages.Count > 0 ? Pages[0] : new FileNotebookPage();
}

/// <summary>
/// Eine einzelne Seite innerhalb eines Notizbuchs.
/// </summary>
public class FileNotebookPage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PageNumber { get; set; }
    public PageTemplateType Template { get; set; } = PageTemplateType.Blank;
    public double Zoom { get; set; } = 1.0;
    public string Theme { get; set; } = "system";
    public StrokeCollection Strokes { get; set; } = [];

    public static implicit operator NotePage(FileNotebookPage saved)
    {
        return new NotePage
        {
            Id = saved.Id,
            PageNumber = saved.PageNumber,
            Template = saved.Template,
            Strokes = saved.Strokes,
            Zoom = saved.Zoom,
            Theme = saved.Theme
        };
    }
}

// ─────────────────────────── NoteFileManager ──────────────────────────

/// <summary>
/// Verwaltet das Speichern, Laden und Exportieren von Notizen.
/// Thread-sichere Implementierung mit automatischem Speichern.
/// </summary>
public class NoteFileManager : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly object _lock = new();
    private System.Threading.Timer? _autoSaveTimer;
    private Notebook? _autoSaveTarget;
    private bool _disposed;

    /// <summary>
    /// Basis-Pfad für alle Notizbücher: %AppData%/FlipsiInk/Notes/
    /// </summary>
    public static string NotesBasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FlipsiInk", "Notes");

    // ─────────────────────── Speichern ───────────────────────

    /// <summary>
    /// Speichert eine Seite als PNG-Thumbnail.
    /// </summary>
    public void SaveNoteAsPng(Guid notebookId, int pageNumber, Bitmap bitmap)
    {
        lock (_lock)
        {
            var dir = GetPageDirectory(notebookId);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"page_{pageNumber}.png");
            try
            {
                bitmap.Save(path, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                throw new IOException($"Fehler beim Speichern der PNG-Datei: {path}", ex);
            }
        }
    }

    /// <summary>
    /// Speichert Strokes und Metadaten einer Seite als JSON.
    /// </summary>
    public void SaveNoteAsJson(Guid notebookId, int pageNumber, StrokeCollection strokes,
        PageTemplateType template, double zoom, string theme)
    {
        lock (_lock)
        {
            var dir = GetPageDirectory(notebookId);
            Directory.CreateDirectory(dir);

            var savedPage = ConvertToSavedPage(pageNumber, strokes, template, zoom, theme);
            var path = Path.Combine(dir, $"page_{pageNumber}.json");

            try
            {
                var json = JsonSerializer.Serialize(savedPage, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                throw new IOException($"Fehler beim Speichern der JSON-Datei: {path}", ex);
            }
        }
    }

    /// <summary>
    /// Speichert alle Seiten eines Notizbuchs.
    /// </summary>
    public void SaveAllPages(Notebook notebook)
    {
        lock (_lock)
        {
            foreach (var page in notebook.Pages)
            {
                SaveNoteAsJson(notebook.Id, page.PageNumber, page.Strokes,
                    page.Template, page.Zoom, page.Theme);
            }

            // Notizbuch-Metadaten speichern
            SaveNotebookMeta(notebook);
        }
    }

    /// <summary>
    /// Aktiviert das automatische Speichern im angegebenen Intervall.
    /// </summary>
    public void EnableAutoSave(TimeSpan interval, Notebook notebook)
    {
        lock (_lock)
        {
            _autoSaveTarget = notebook;
            _autoSaveTimer?.Dispose();
            _autoSaveTimer = new System.Threading.Timer(_ =>
            {
                if (_autoSaveTarget is not null)
                {
                    try { SaveAllPages(_autoSaveTarget); }
                    catch { /* Auto-Save-Fehler silently ignorieren */ }
                }
            }, null, interval, interval);
        }
    }

    /// <summary>
    /// Deaktiviert das automatische Speichern.
    /// </summary>
    public void DisableAutoSave()
    {
        lock (_lock)
        {
            _autoSaveTimer?.Dispose();
            _autoSaveTimer = null;
            _autoSaveTarget = null;
        }
    }

    // ─────────────────────── Laden ───────────────────────

    /// <summary>
    /// Lädt Strokes aus einer JSON-Datei.
    /// </summary>
    public StrokeCollection LoadStrokesFromJson(string jsonPath)
    {
        lock (_lock)
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var savedPage = JsonSerializer.Deserialize<SavedPage>(json, JsonOptions);
                if (savedPage is null)
                    throw new InvalidDataException($"JSON-Datei konnte nicht deserialisiert werden: {jsonPath}");

                return ConvertToStrokeCollection(savedPage.Strokes);
            }
            catch (Exception ex) when (ex is not InvalidDataException)
            {
                throw new IOException($"Fehler beim Laden der JSON-Datei: {jsonPath}", ex);
            }
        }
    }

    /// <summary>
    /// Lädt ein PNG-Thumbnail.
    /// </summary>
    public Bitmap LoadThumbnail(string pngPath)
    {
        lock (_lock)
        {
            try
            {
                return new Bitmap(pngPath);
            }
            catch (Exception ex)
            {
                throw new IOException($"Fehler beim Laden des Thumbnails: {pngPath}", ex);
            }
        }
    }

    /// <summary>
    /// Lädt ein komplettes Notizbuch mit allen Seiten.
    /// </summary>
    public Notebook LoadNotebook(Guid id)
    {
        lock (_lock)
        {
            var dir = GetPageDirectory(id);
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException($"Notizbuch nicht gefunden: {dir}");

            // Metadaten laden
            var notebook = LoadNotebookMeta(id);
            notebook.Id = id;

            // Alle Seiten laden
            var jsonFiles = Directory.GetFiles(dir, "page_*.json");
            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(jsonFile);
                    var savedPage = JsonSerializer.Deserialize<SavedPage>(json, JsonOptions);
                    if (savedPage is null) continue;

                    var page = new FileNotebookPage
                    {
                        Id = savedPage.Id,
                        PageNumber = savedPage.PageNumber,
                        Template = savedPage.Template,
                        Zoom = savedPage.Zoom,
                        Theme = savedPage.Theme,
                        Strokes = ConvertToStrokeCollection(savedPage.Strokes)
                    };
                    notebook.Pages.Add(page);
                }
                catch
                {
                    /* Fehlerhafte Seite überspringen */
                }
            }

            notebook.Pages.Sort((a, b) => a.PageNumber.CompareTo(b.PageNumber));
            return notebook;
        }
    }

    // ─────────────────────── Export ───────────────────────

    /// <summary>
    /// Exportiert ein Notizbuch als PDF (einfache Bitmap-zu-PDF Konvertierung).
    /// Nutzt System.Drawing.Bitmap und einen minimalen PDF-Writer ohne externe Abhängigkeiten.
    /// </summary>
    public void ExportAsPdf(Guid notebookId, string outputPath)
    {
        lock (_lock)
        {
            var dir = GetPageDirectory(notebookId);
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException($"Notizbuch nicht gefunden: {dir}");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

                var pngFiles = Directory.GetFiles(dir, "page_*.png");
                if (pngFiles.Length == 0)
                    throw new InvalidOperationException(
                        "Keine PNG-Seiten zum Exportieren gefunden. Bitte zuerst PNGs speichern.");

                // TODO: Echte PDF-Generierung bei Bedarf mit iTextSharp/PdfSharp einbinden.
                // Aktuell: einfache Bitmap-Kopie als Platzhalter – PDF wird als
                // mehrseitiges TIFF/PDF via externer Lib erzeugt.
                // Fürs Erste erzeugen wir ein rudimentäres PDF manuell:
                WriteSimplePdf(pngFiles, outputPath);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new IOException($"Fehler beim PDF-Export: {outputPath}", ex);
            }
        }
    }

    /// <summary>
    /// Schreibt ein rudimentäres PDF mit eingebetteten PNG-Bildern.
    /// Keine externe Abhängigkeit – reicht für einfache Eins-zu-eins-Exporte.
    /// </summary>
    private static void WriteSimplePdf(string[] pngFiles, string outputPath)
    {
        // PDF-Struktur: Header + Objekte + xref + Trailer
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n"));

        var offsets = new List<long>();
        var imageObjectNumbers = new List<int>();

        // Objekt 1: Catalog
        offsets.Add(ms.Position);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n"));

        // Objekt 2: Pages (wird später aktualisiert)
        offsets.Add(ms.Position);
        var pagesObjPos = ms.Position;
        writer.Write(System.Text.Encoding.ASCII.GetBytes("2 0 obj\n<< /Type /Pages /Kids ["));

        var pageObjectNumbers = new List<int>();
        int nextObjNum = 3;

        // Sammle Seiten-Objektnummern
        for (int i = 0; i < pngFiles.Length; i++)
        {
            pageObjectNumbers.Add(nextObjNum);
            nextObjNum += 3; // pro Seite: Page + Image + Content
        }

        foreach (var pageNum in pageObjectNumbers)
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes($"{pageNum} 0 R "));
        }

        writer.Write(System.Text.Encoding.ASCII.GetBytes($"] /Count {pngFiles.Length} >>\nendobj\n"));

        // Jede Seite als Bild einbetten
        for (int i = 0; i < pngFiles.Length; i++)
        {
            var pngFile = pngFiles[i];
            using var bitmap = new Bitmap(pngFile);
            var w = bitmap.Width;
            var h = bitmap.Height;

            // PNG-Daten einlesen
            var pngBytes = File.ReadAllBytes(pngFile);

            // Page-Objekt
            int pageObjNum = pageObjectNumbers[i];
            int imgObjNum = pageObjNum + 1;
            int contentObjNum = pageObjNum + 2;

            offsets.Add(ms.Position);
            writer.Write(System.Text.Encoding.ASCII.GetBytes(
                $"{pageObjNum} 0 obj\n<< /Type /Page /Parent 2 0 R " +
                $"/MediaBox [0 0 {w} {h}] /Contents {contentObjNum} 0 R " +
                $"/Resources << /XObject << /Img {imgObjNum} 0 R >> >> >>\nendobj\n"));

            // Image-Objekt
            offsets.Add(ms.Position);
            writer.Write(System.Text.Encoding.ASCII.GetBytes(
                $"{imgObjNum} 0 obj\n<< /Type /XObject /Subtype /Image " +
                $"/Width {w} /Height {h} /ColorSpace /DeviceRGB " +
                $"/BitsPerComponent 8 /Filter /FlateDecode /Length {pngBytes.Length} >>\nstream\n"));
            writer.Write(pngBytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("\nendstream\nendobj\n"));

            // Content-Stream-Objekt
            var contentStr = $"q {w} 0 0 {h} 0 0 cm /Img Do Q";
            var contentBytes = System.Text.Encoding.ASCII.GetBytes(contentStr);
            offsets.Add(ms.Position);
            writer.Write(System.Text.Encoding.ASCII.GetBytes(
                $"{contentObjNum} 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n"));
            writer.Write(contentBytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("\nendstream\nendobj\n"));
        }

        // xref-Tabelle
        var xrefPos = ms.Position;
        writer.Write(System.Text.Encoding.ASCII.GetBytes($"xref\n0 {offsets.Count + 1}\n"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("0000000000 65535 f \n"));
        foreach (var off in offsets)
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes($"{off:D10} 00000 n \n"));
        }

        // Trailer
        writer.Write(System.Text.Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n"));

        File.WriteAllBytes(outputPath, ms.ToArray());
    }

    /// <summary>
    /// Exportiert alle Seiten eines Notizbuchs als PNG-Ordner.
    /// </summary>
    public void ExportAsPng(Guid notebookId, string outputDir)
    {
        lock (_lock)
        {
            var srcDir = GetPageDirectory(notebookId);
            if (!Directory.Exists(srcDir))
                throw new DirectoryNotFoundException($"Notizbuch nicht gefunden: {srcDir}");

            Directory.CreateDirectory(outputDir);

            foreach (var srcFile in Directory.GetFiles(srcDir, "page_*.png"))
            {
                var destFile = Path.Combine(outputDir, Path.GetFileName(srcFile));
                File.Copy(srcFile, destFile, overwrite: true);
            }
        }
    }

    /// <summary>
    /// Exportiert ein Notizbuch als JSON-Backup (alle Seiten in einer Datei).
    /// </summary>
    public void ExportAsJson(Guid notebookId, string outputPath)
    {
        lock (_lock)
        {
            var dir = GetPageDirectory(notebookId);
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException($"Notizbuch nicht gefunden: {dir}");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            var notebook = LoadNotebook(notebookId);
            var exportData = new NotebookExport
            {
                Id = notebook.Id,
                Name = notebook.Name,
                CreatedAt = notebook.CreatedAt,
                UpdatedAt = DateTime.Now,
                Pages = []
            };

            foreach (var page in notebook.Pages)
            {
                var savedPage = ConvertToSavedPage(
                    page.PageNumber, page.Strokes, page.Template, page.Zoom, page.Theme);
                exportData.Pages.Add(savedPage);
            }

            try
            {
                var json = JsonSerializer.Serialize(exportData, JsonOptions);
                File.WriteAllText(outputPath, json);
            }
            catch (Exception ex)
            {
                throw new IOException($"Fehler beim JSON-Export: {outputPath}", ex);
            }
        }
    }

    /// <summary>
    /// Exportiert eine einzelne Seite als PNG.
    /// </summary>
    public void ExportPageAsPng(Guid notebookId, int pageNumber, string outputPath)
    {
        lock (_lock)
        {
            var srcPath = Path.Combine(GetPageDirectory(notebookId), $"page_{pageNumber}.png");
            if (!File.Exists(srcPath))
                throw new FileNotFoundException($"Seite nicht gefunden: {srcPath}");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.Copy(srcPath, outputPath, overwrite: true);
        }
    }

    // ─────────────────────── Hilfsmethoden ───────────────────────

    /// <summary>
    /// Liefert den Verzeichnispfad für ein Notizbuch.
    /// </summary>
    private static string GetPageDirectory(Guid notebookId) =>
        Path.Combine(NotesBasePath, notebookId.ToString());

    /// <summary>
    /// Speichert Notizbuch-Metadaten (name, created, etc.).
    /// </summary>
    private void SaveNotebookMeta(Notebook notebook)
    {
        var meta = new NotebookMeta
        {
            Id = notebook.Id,
            Name = notebook.Name,
            CreatedAt = notebook.CreatedAt,
            UpdatedAt = DateTime.Now
        };
        var path = Path.Combine(GetPageDirectory(notebook.Id), "notebook.json");
        var json = JsonSerializer.Serialize(meta, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Lädt Notizbuch-Metadaten.
    /// </summary>
    private Notebook LoadNotebookMeta(Guid id)
    {
        var path = Path.Combine(GetPageDirectory(id), "notebook.json");
        if (!File.Exists(path))
            return new Notebook { Id = id };

        try
        {
            var json = File.ReadAllText(path);
            var meta = JsonSerializer.Deserialize<NotebookMeta>(json, JsonOptions);
            if (meta is null) return new Notebook { Id = id };

            return new Notebook
            {
                Id = meta.Id,
                Name = meta.Name,
                CreatedAt = meta.CreatedAt,
                UpdatedAt = meta.UpdatedAt
            };
        }
        catch
        {
            return new Notebook { Id = id };
        }
    }

    /// <summary>
    /// Konvertiert eine StrokeCollection in serialisierbare SavedStroke-Liste.
    /// </summary>
    private static SavedPage ConvertToSavedPage(int pageNumber, StrokeCollection strokes,
        PageTemplateType template, double zoom, string theme)
    {
        var savedPage = new SavedPage
        {
            Id = Guid.NewGuid(),
            PageNumber = pageNumber,
            Template = template,
            Zoom = zoom,
            Theme = theme,
            Strokes = []
        };

        foreach (var stroke in strokes)
        {
            var savedStroke = new SavedStroke
            {
                Color = stroke.DrawingAttributes.Color.ToString(),
                Width = stroke.DrawingAttributes.Width,
                Height = stroke.DrawingAttributes.Height,
                IsHighlighter = stroke.DrawingAttributes.IsHighlighter,
                Points = []
            };

            foreach (var point in stroke.StylusPoints)
            {
                savedStroke.Points.Add(new SavedPoint
                {
                    X = point.X,
                    Y = point.Y,
                    PressureFactor = point.PressureFactor
                });
            }

            savedPage.Strokes.Add(savedStroke);
        }

        return savedPage;
    }

    /// <summary>
    /// Konvertiert SavedStroke-Liste zurück in eine StrokeCollection.
    /// </summary>
    private static StrokeCollection ConvertToStrokeCollection(List<SavedStroke> savedStrokes)
    {
        var collection = new StrokeCollection();

        foreach (var savedStroke in savedStrokes)
        {
            var points = new StylusPointCollection(savedStroke.Points.Count);
            foreach (var p in savedStroke.Points)
            {
                points.Add(new StylusPoint(p.X, p.Y, p.PressureFactor));
            }

            var stroke = new Stroke(points);
            stroke.DrawingAttributes.Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(savedStroke.Color);
            stroke.DrawingAttributes.Width = savedStroke.Width;
            stroke.DrawingAttributes.Height = savedStroke.Height;
            stroke.DrawingAttributes.IsHighlighter = savedStroke.IsHighlighter;

            collection.Add(stroke);
        }

        return collection;
    }

    // ─────────────────────── IDisposable ───────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisableAutoSave();
    }
}

// ─────────────────────── Interne Hilfsklassen ───────────────────────

/// <summary>
/// Metadaten eines Notizbuchs (serialisierbar).
/// </summary>
internal class NotebookMeta
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Unbenannt";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Export-Format für komplette Notizbuch-Backups.
/// </summary>
internal class NotebookExport
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Unbenannt";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<SavedPage> Pages { get; set; } = [];
}