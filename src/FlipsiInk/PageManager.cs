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

using System.Collections.ObjectModel;
using DBitmap = System.Drawing.Bitmap;
using System.IO;
using System.Windows.Ink;

namespace FlipsiInk;

/// <summary>
/// EventArgs für den Seitenwechsel.
/// </summary>
public class PageChangedEventArgs : EventArgs
{
    /// <summary>Seitennummer vor dem Wechsel.</summary>
    public int OldPageNumber { get; }
    /// <summary>Seitennummer nach dem Wechsel.</summary>
    public int NewPageNumber { get; }

    public PageChangedEventArgs(int oldPage, int newPage)
    {
        OldPageNumber = oldPage;
        NewPageNumber = newPage;
    }
}

/// <summary>
/// EventArgs für Änderungen der Seitenanzahl (Hinzufügen/Löschen).
/// </summary>
public class PageCountChangedEventArgs : EventArgs
{
    /// <summary>Alte Seitenanzahl.</summary>
    public int OldCount { get; }
    /// <summary>Neue Seitenanzahl.</summary>
    public int NewCount { get; }
    /// <summary>Art der Änderung (hinzugefügt oder gelöscht).</summary>
    public string ChangeType { get; }

    public PageCountChangedEventArgs(int oldCount, int newCount, string changeType)
    {
        OldCount = oldCount;
        NewCount = newCount;
        ChangeType = changeType;
    }
}

/// <summary>
/// Verwaltet die Seiten eines Notizbuchs – Navigation, Hinzufügen, Löschen,
/// Speichern und Laden von Stroke-Daten.
/// </summary>
public class PageManager
{
    private readonly Notebook _notebook;
    private int _currentPageNumber = 1;

    /// <summary>Wird ausgelöst, wenn die aktuelle Seite gewechselt wird.</summary>
    public event EventHandler<PageChangedEventArgs>? PageChanged;

    /// <summary>Wird ausgelöst, wenn Seiten hinzugefügt oder gelöscht werden.</summary>
    public event EventHandler<PageCountChangedEventArgs>? PageCountChanged;

    /// <summary>
    /// Erstellt einen neuen PageManager für das angegebene Notizbuch.
    /// </summary>
    /// <param name="notebook">Das zu verwaltende Notizbuch.</param>
    public PageManager(Notebook notebook)
    {
        _notebook = notebook;
        // Stelle sicher, dass mindestens eine Seite existiert
        if (_notebook.Pages.Count == 0)
        {
            _notebook.Pages.Add(new NotePage
            {
                PageNumber = 1,
                Template = notebook.Template
            });
            _notebook.PageCount = 1;
        }
    }

    /// <summary>Anzahl der Seiten im Notizbuch.</summary>
    public int PageCount => _notebook.Pages.Count;

    /// <summary>Aktuelle Seitennummer (1-basiert).</summary>
    public int CurrentPageNumber
    {
        get => _currentPageNumber;
        private set => _currentPageNumber = Math.Clamp(value, 1, PageCount);
    }

    /// <summary>Gibt an, ob eine nächste Seite existiert.</summary>
    public bool HasNextPage => CurrentPageNumber < PageCount;

    /// <summary>Gibt an, ob eine vorherige Seite existiert.</summary>
    public bool HasPreviousPage => CurrentPageNumber > 1;

    /// <summary>
    /// Fügt eine neue Seite mit der angegebenen Vorlage hinzu.
    /// </summary>
    /// <param name="template">Die Seitenvorlage (Standard: Blank).</param>
    /// <returns>Die neu erstellte Seite.</returns>
    public NotePage AddPage(PageTemplateType template = PageTemplateType.Blank)
    {
        int oldCount = PageCount;
        int newPageNumber = _notebook.Pages.Count > 0
            ? _notebook.Pages.Max(p => p.PageNumber) + 1
            : 1;

        var page = new NotePage
        {
            PageNumber = newPageNumber,
            Template = template
        };

        _notebook.Pages.Add(page);
        _notebook.PageCount = _notebook.Pages.Count;

        PageCountChanged?.Invoke(this, new PageCountChangedEventArgs(oldCount, PageCount, "added"));

        return page;
    }

    /// <summary>
    /// Löscht die Seite mit der angegebenen Seitennummer.
    /// Die letzte verbleibende Seite kann nicht gelöscht werden.
    /// </summary>
    /// <param name="pageNumber">Die zu löschende Seitennummer.</param>
    /// <returns>True, wenn die Seite gelöscht wurde.</returns>
    public bool RemovePage(int pageNumber)
    {
        if (PageCount <= 1)
            return false; // Letzte Seite darf nicht gelöscht werden

        var page = _notebook.Pages.Find(p => p.PageNumber == pageNumber);
        if (page == null)
            return false;

        int oldCount = PageCount;
        _notebook.Pages.Remove(page);

        // Seitennummern neu sortieren
        RenumberPages();

        // Aktuelle Seite anpassen, falls nötig
        if (CurrentPageNumber > PageCount)
            CurrentPageNumber = PageCount;

        _notebook.PageCount = _notebook.Pages.Count;
        PageCountChanged?.Invoke(this, new PageCountChangedEventArgs(oldCount, PageCount, "removed"));

        return true;
    }

    /// <summary>
    /// Ändert die Reihenfolge der Seiten.
    /// </summary>
    /// <param name="from">Aktuelle Position (0-basiert).</param>
    /// <param name="to">Neue Position (0-basiert).</param>
    /// <returns>True, wenn die Verschiebung erfolgreich war.</returns>
    public bool MovePage(int from, int to)
    {
        if (from < 0 || from >= PageCount || to < 0 || to >= PageCount || from == to)
            return false;

        var page = _notebook.Pages[from];
        _notebook.Pages.RemoveAt(from);
        _notebook.Pages.Insert(to, page);

        RenumberPages();
        return true;
    }

    /// <summary>
    /// Lädt die Seite mit der angegebenen Seitennummer.
    /// </summary>
    /// <param name="pageNumber">Die gewünschte Seitennummer.</param>
    /// <returns>Die angeforderte Seite oder null, falls nicht gefunden.</returns>
    public NotePage? GetPage(int pageNumber)
    {
        return _notebook.Pages.Find(p => p.PageNumber == pageNumber);
    }

    /// <summary>
    /// Gibt alle Seiten des Notizbuchs zurück.
    /// </summary>
    /// <returns>Liste aller Seiten.</returns>
    public List<NotePage> GetAllPages()
    {
        return new List<NotePage>(_notebook.Pages);
    }

    /// <summary>
    /// Navigiert zur angegebenen Seitennummer.
    /// </summary>
    /// <param name="pageNumber">Die Ziel-Seitennummer.</param>
    public void GoToPage(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > PageCount)
            return;

        int oldPage = CurrentPageNumber;
        CurrentPageNumber = pageNumber;

        if (oldPage != CurrentPageNumber)
            PageChanged?.Invoke(this, new PageChangedEventArgs(oldPage, CurrentPageNumber));
    }

    /// <summary>
    /// Navigiert zur nächsten Seite.
    /// </summary>
    /// <returns>Die nächste Seite.</returns>
    /// <exception cref="InvalidOperationException">Wenn keine nächste Seite existiert.</exception>
    public NotePage NextPage()
    {
        if (!HasNextPage)
            throw new InvalidOperationException("Keine nächste Seite vorhanden.");

        GoToPage(CurrentPageNumber + 1);
        return GetPage(CurrentPageNumber)!;
    }

    /// <summary>
    /// Navigiert zur vorherigen Seite.
    /// </summary>
    /// <returns>Die vorherige Seite.</returns>
    /// <exception cref="InvalidOperationException">Wenn keine vorherige Seite existiert.</exception>
    public NotePage PreviousPage()
    {
        if (!HasPreviousPage)
            throw new InvalidOperationException("Keine vorherige Seite vorhanden.");

        GoToPage(CurrentPageNumber - 1);
        return GetPage(CurrentPageNumber)!;
    }

    /// <summary>
    /// Speichert die Stroke-Daten der aktuellen Seite.
    /// </summary>
    /// <param name="strokes">Die zu speichernden Strokes.</param>
    public void SaveCurrentPage(StrokeCollection strokes)
    {
        var page = GetPage(CurrentPageNumber);
        if (page != null)
        {
            // Serialisiere Strokes als Base64-ISF (Ink Serialized Format)
            using var ms = new MemoryStream();
            strokes.Save(ms);
            page.StrokesJson = Convert.ToBase64String(ms.ToArray());
        }
    }

    /// <summary>
    /// Lädt die Stroke-Daten der angegebenen Seite.
    /// </summary>
    /// <param name="pageNumber">Die Seitennummer.</param>
    /// <returns>Die geladenen Strokes oder eine leere StrokeCollection.</returns>
    public StrokeCollection LoadPage(int pageNumber)
    {
        var page = GetPage(pageNumber);
        if (page == null || string.IsNullOrEmpty(page.StrokesJson))
            return new StrokeCollection();

        try
        {
            var data = Convert.FromBase64String(page.StrokesJson);
            using var ms = new MemoryStream(data);
            return new StrokeCollection(ms);
        }
        catch
        {
            // Bei Fehler leere Collection zurückgeben
            return new StrokeCollection();
        }
    }

    /// <summary>
    /// Aktualisiert das Thumbnail der angegebenen Seite.
    /// </summary>
    /// <param name="pageNumber">Die Seitennummer.</param>
    /// <param name="thumbnail">Das neue Thumbnail-Bild.</param>
    public void UpdateThumbnail(int pageNumber, DBitmap thumbnail)
    {
        var page = GetPage(pageNumber);
        if (page != null)
        {
            // Speichere Thumbnail-Pfad (eigentliche Speicherung über NoteFileManager)
            page.Thumbnail = $"page_{pageNumber}_thumb.png";
        }
    }

    /// <summary>
    /// Nummeriert die Seiten neu durch (1-basiert, fortlaufend).
    /// </summary>
    private void RenumberPages()
    {
        for (int i = 0; i < _notebook.Pages.Count; i++)
        {
            _notebook.Pages[i].PageNumber = i + 1;
        }
    }
}