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

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Ink;

namespace FlipsiInk;

/// <summary>

/// Repräsentiert eine einzelne Seite in einem Notizbuch.
/// </summary>
public class NotePage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PageNumber { get; set; }
    public PageTemplateType Template { get; set; } = PageTemplateType.Blank;
    /// <summary>Pfad zur .json Datei mit den Stroke-Daten.</summary>
    public string StrokesJson { get; set; } = string.Empty;
    /// <summary>Pfad zur .png Thumbnail-Datei.</summary>
    public string Thumbnail { get; set; } = string.Empty;
    /// <summary>Stroke-Daten der Seite.</summary>
    public StrokeCollection Strokes { get; set; } = [];
    /// <summary>Zoom-Faktor der Seite.</summary>
    public double Zoom { get; set; } = 1.0;
    /// <summary>Theme der Seite (z.B. "Light", "Dark", "system").</summary>
    public string Theme { get; set; } = "system";
}

/// <summary>
/// Repräsentiert ein Notizbuch mit Seiten und Metadaten.
/// </summary>
public class Notebook
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid? FolderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Zeitstempel der letzten Aktualisierung.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int PageCount { get; set; }
    public PageTemplateType Template { get; set; } = PageTemplateType.LinedWide;
    public DateTime LastOpened { get; set; } = DateTime.UtcNow;
    /// <summary>Cover-Farbe als Hex-String (z.B. "#FF9500", "#34C759").</summary>
    public string Color { get; set; } = "#FF9500";
    public List<NotePage> Pages { get; set; } = [];
    /// <summary>Markierung für Favoriten.</summary>
    public bool IsFavorite { get; set; }
}

/// <summary>
/// Repräsentiert einen Ordner im Notizbuch-Baum.
/// </summary>
public class NotebookFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public List<NotebookFolder> Children { get; set; } = [];
}

/// <summary>
/// Index-Datei-Struktur für die Ordnerhierarchie und Notizbuch-Metadaten.
/// </summary>
internal class NoteIndex
{
    public List<NotebookFolder> Folders { get; set; } = [];
    /// <summary>Flache Liste aller Notizbuch-Metadaten (ohne Pages).</summary>
    public List<Notebook> Notebooks { get; set; } = [];
}

/// <summary>
/// Verwaltet Notizbücher und Ordner lokal (JSON-basiert, kein Cloud).
/// Thread-safe durch Lock-Objekt für alle Dateizugriffe.
/// </summary>
public class NoteManager
{
    private readonly string _basePath;
    private readonly string _indexPath;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Erstellt einen neuen NoteManager.
    /// </summary>
    /// <param name="basePath">Basispfad für alle Notizdaten (z.B. %AppData%/FlipsiInk/Notes/)</param>
    public NoteManager(string basePath)
    {
        _basePath = basePath;
        _indexPath = Path.Combine(basePath, "index.json");
        Directory.CreateDirectory(basePath);
    }

    // ─── Ordner-Operationen ─────────────────────────────────────────────

    /// <summary>Gibt alle Root-Ordner zurück (Ordner ohne Parent).</summary>
    public List<NotebookFolder> GetRootFolders()
    {
        lock (_lock)
        {
            var index = LoadIndex();
            return index.Folders.Where(f => f.ParentId is null).ToList();
        }
    }

    /// <summary>Gibt alle Notizbücher im angegebenen Ordner zurück (null = Root-Ebene).</summary>
    public List<Notebook> GetNotebooksInFolder(Guid? folderId)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            return index.Notebooks.Where(n => n.FolderId == folderId).ToList();
        }
    }

    /// <summary>Erstellt einen neuen Ordner.</summary>
    public NotebookFolder CreateFolder(string name, Guid? parentId)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            var folder = new NotebookFolder
            {
                Name = name,
                ParentId = parentId
            };

            if (parentId is null)
            {
                index.Folders.Add(folder);
            }
            else
            {
                var parent = FindFolder(index.Folders, parentId.Value)
                    ?? throw new InvalidOperationException($"Ordner mit ID {parentId.Value} nicht gefunden.");
                parent.Children.Add(folder);
            }

            SaveIndex(index);
            return folder;
        }
    }

    /// <summary>Löscht einen Ordner rekursiv (inkl. aller Notizbücher und Unterordner).</summary>
    public void DeleteFolder(Guid id)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            var folder = FindFolder(index.Folders, id);

            if (folder is null)
                throw new InvalidOperationException($"Ordner mit ID {id} nicht gefunden.");

            // Alle Notizbuch-IDs im Ordner (rekursiv) sammeln und löschen
            var allFolderIds = CollectFolderIds(folder);
            foreach (var notebook in index.Notebooks.Where(n => allFolderIds.Contains(n.FolderId ?? Guid.Empty) || n.FolderId == id).ToList())
            {
                DeleteNotebookInternal(index, notebook.Id);
            }

            // Ordner aus der Hierarchie entfernen
            RemoveFolder(index.Folders, id);
            SaveIndex(index);
        }
    }

    /// <summary>Benennt einen Ordner um.</summary>
    public void RenameFolder(Guid id, string newName)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            var folder = FindFolder(index.Folders, id)
                ?? throw new InvalidOperationException($"Ordner mit ID {id} nicht gefunden.");
            folder.Name = newName;
            folder.ModifiedAt = DateTime.UtcNow;
            SaveIndex(index);
        }
    }

    // ─── Notizbuch-Operationen ───────────────────────────────────────────

    /// <summary>Erstellt ein neues Notizbuch mit einer ersten Seite.</summary>
    public Notebook CreateNotebook(string name, Guid? folderId, PageTemplateType template)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            var notebook = new Notebook
            {
                Name = name,
                FolderId = folderId,
                Template = template,
                PageCount = 1
            };

            // Notizbuch-Verzeichnis und erste Seite anlegen
            var nbPath = GetNotebookPathInternal(notebook.Id);
            Directory.CreateDirectory(nbPath);

            var page = new NotePage
            {
                PageNumber = 1,
                Template = template,
                StrokesJson = Path.Combine(nbPath, "page_001.json"),
                Thumbnail = Path.Combine(nbPath, "page_001.png")
            };
            notebook.Pages.Add(page);

            // Leere Stroke-Datei anlegen
            File.WriteAllText(page.StrokesJson, "[]");

            index.Notebooks.Add(notebook);
            SaveIndex(index);
            SaveNotebookInternal(notebook);
            return notebook;
        }
    }

    /// <summary>Löscht ein Notizbuch inkl. aller Dateien.</summary>
    public void DeleteNotebook(Guid id)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            DeleteNotebookInternal(index, id);
            SaveIndex(index);
        }
    }

    /// <summary>Benennt ein Notizbuch um.</summary>
    public void RenameNotebook(Guid id, string newName)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            var notebook = index.Notebooks.FirstOrDefault(n => n.Id == id)
                ?? throw new InvalidOperationException($"Notizbuch mit ID {id} nicht gefunden.");
            notebook.Name = newName;
            notebook.ModifiedAt = DateTime.UtcNow;
            SaveIndex(index);

            // Auch die notebook.json aktualisieren
            var full = LoadNotebookInternal(id);
            full.Name = newName;
            full.ModifiedAt = DateTime.UtcNow;
            SaveNotebookInternal(full);
        }
    }

    /// <summary>Verschiebt ein Notizbuch in einen anderen Ordner.</summary>
    public void MoveNotebook(Guid notebookId, Guid? targetFolderId)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            var notebook = index.Notebooks.FirstOrDefault(n => n.Id == notebookId)
                ?? throw new InvalidOperationException($"Notizbuch mit ID {notebookId} nicht gefunden.");
            notebook.FolderId = targetFolderId;
            notebook.ModifiedAt = DateTime.UtcNow;
            SaveIndex(index);

            var full = LoadNotebookInternal(notebookId);
            full.FolderId = targetFolderId;
            full.ModifiedAt = DateTime.UtcNow;
            SaveNotebookInternal(full);
        }
    }

    /// <summary>Lädt ein Notizbuch inkl. aller Seiten aus der notebook.json.</summary>
    public Notebook LoadNotebook(Guid id)
    {
        lock (_lock)
        {
            return LoadNotebookInternal(id);
        }
    }

    /// <summary>Speichert ein Notizbuch (aktualisiert notebook.json und den Index).</summary>
    public void SaveNotebook(Notebook notebook)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            var existing = index.Notebooks.FirstOrDefault(n => n.Id == notebook.Id);
            if (existing is not null)
            {
                // Metadaten im Index aktualisieren (ohne Pages)
                existing.Name = notebook.Name;
                existing.FolderId = notebook.FolderId;
                existing.PageCount = notebook.Pages.Count;
                existing.Template = notebook.Template;
                existing.LastOpened = notebook.LastOpened;
                existing.Color = notebook.Color;
                existing.ModifiedAt = DateTime.UtcNow;
                existing.IsFavorite = notebook.IsFavorite;
            }
            SaveIndex(index);
            SaveNotebookInternal(notebook);
        }
    }

    /// <summary>Gibt den Pfad zum Notizbuch-Verzeichnis zurück.</summary>
    public string GetNotebookPath(Guid id)
    {
        return GetNotebookPathInternal(id);
    }

    /// <summary>Sucht Notizbücher anhand des Namens (case-insensitive).</summary>
    public List<Notebook> SearchNotebooks(string query)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            return index.Notebooks
                .Where(n => n.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.ModifiedAt)
                .ToList();
        }
    }

    /// <summary>Gibt die zuletzt geöffneten Notizbücher zurück.</summary>
    public List<Notebook> GetRecentNotebooks(int count = 10)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            return index.Notebooks
                .OrderByDescending(n => n.LastOpened)
                .Take(count)
                .ToList();
        }
    }

    /// <summary>Gibt alle als Favorit markierten Notizbücher zurück.</summary>
    public List<Notebook> GetFavoriteNotebooks()
    {
        lock (_lock)
        {
            var index = LoadIndex();
            return index.Notebooks.Where(n => n.IsFavorite).ToList();
        }
    }

    // ─── Interne Hilfsmethoden ──────────────────────────────────────────

    private string GetNotebookPathInternal(Guid id)
    {
        return Path.Combine(_basePath, id.ToString());
    }

    /// <summary>Lädt den Index aus der index.json.</summary>
    private NoteIndex LoadIndex()
    {
        if (!File.Exists(_indexPath))
            return new NoteIndex();

        var json = File.ReadAllText(_indexPath);
        return JsonSerializer.Deserialize<NoteIndex>(json, JsonOptions) ?? new NoteIndex();
    }

    /// <summary>Speichert den Index in die index.json.</summary>
    private void SaveIndex(NoteIndex index)
    {
        var json = JsonSerializer.Serialize(index, JsonOptions);
        File.WriteAllText(_indexPath, json);
    }

    /// <summary>Lädt ein Notizbuch aus seiner notebook.json.</summary>
    private Notebook LoadNotebookInternal(Guid id)
    {
        var path = Path.Combine(GetNotebookPathInternal(id), "notebook.json");
        if (!File.Exists(path))
            throw new InvalidOperationException($"Notizbuch-Datei nicht gefunden: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Notebook>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Fehler beim Deserialisieren von {path}");
    }

    /// <summary>Speichert ein Notizbuch in seine notebook.json.</summary>
    private void SaveNotebookInternal(Notebook notebook)
    {
        var nbPath = GetNotebookPathInternal(notebook.Id);
        Directory.CreateDirectory(nbPath);
        var filePath = Path.Combine(nbPath, "notebook.json");
        var json = JsonSerializer.Serialize(notebook, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>Sucht einen Ordner rekursiv in der Hierarchie.</summary>
    private static NotebookFolder? FindFolder(List<NotebookFolder> folders, Guid id)
    {
        foreach (var folder in folders)
        {
            if (folder.Id == id) return folder;
            var found = FindFolder(folder.Children, id);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>Entfernt einen Ordner rekursiv aus der Hierarchie.</summary>
    private static bool RemoveFolder(List<NotebookFolder> folders, Guid id)
    {
        for (int i = 0; i < folders.Count; i++)
        {
            if (folders[i].Id == id)
            {
                folders.RemoveAt(i);
                return true;
            }
            if (RemoveFolder(folders[i].Children, id))
                return true;
        }
        return false;
    }

    /// <summary>Sammelt alle Ordner-IDs rekursiv (inkl. des Ordners selbst).</summary>
    private static List<Guid> CollectFolderIds(NotebookFolder folder)
    {
        var ids = new List<Guid> { folder.Id };
        foreach (var child in folder.Children)
            ids.AddRange(CollectFolderIds(child));
        return ids;
    }

    /// <summary>Löscht ein Notizbuch aus dem Index und vom Dateisystem.</summary>
    private void DeleteNotebookInternal(NoteIndex index, Guid id)
    {
        var notebook = index.Notebooks.FirstOrDefault(n => n.Id == id);
        if (notebook is null) return;

        index.Notebooks.Remove(notebook);

        // Verzeichnis mit allen Seiten löschen
        var nbPath = GetNotebookPathInternal(id);
        if (Directory.Exists(nbPath))
            Directory.Delete(nbPath, recursive: true);
    }
}