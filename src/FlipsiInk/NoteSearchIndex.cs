// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace FlipsiInk;

/// <summary>
/// Handschrift-Index (Issue #30): SQLite FTS5 Volltextsuche für OCR-Ergebnisse.
/// Indiziert erkannten Text bei jedem Speichern und ermöglicht schnelle Suche.
/// </summary>
public class NoteSearchIndex : IDisposable
{
    private SqliteConnection? _connection;
    private readonly string _dbPath;

    public NoteSearchIndex()
    {
        // Datenbank im FlipsiInk-Datenverzeichnis
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FlipsiInk");
        Directory.CreateDirectory(dataDir);
        _dbPath = Path.Combine(dataDir, "searchindex.db");
    }

    /// <summary>
    /// Initialisiert die Datenbank und erstellt die Tabellen falls nötig.
    /// </summary>
    public void Initialize()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        // Haupttabelle für Notizen
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS notes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    filename TEXT NOT NULL,
                    text TEXT NOT NULL,
                    timestamp TEXT NOT NULL
                )";
            cmd.ExecuteNonQuery();
        }

        // FTS5 virtuelle Tabelle für Volltextsuche
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE VIRTUAL TABLE IF NOT EXISTS notes_fts USING fts5(
                    text,
                    content='notes',
                    content_rowid='id'
                )";
            cmd.ExecuteNonQuery();
        }

        // Trigger: FTS-Index automatisch aktualisieren bei INSERT
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TRIGGER IF NOT EXISTS notes_ai AFTER INSERT ON notes BEGIN
                    INSERT INTO notes_fts(rowid, text) VALUES (new.id, new.text);
                END";
            cmd.ExecuteNonQuery();
        }

        // Trigger: FTS-Index automatisch aktualisieren bei DELETE
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TRIGGER IF NOT EXISTS notes_ad AFTER DELETE ON notes BEGIN
                    INSERT INTO notes_fts(notes_fts, rowid, text) VALUES('delete', old.id, old.text);
                END";
            cmd.ExecuteNonQuery();
        }

        // Trigger: FTS-Index automatisch aktualisieren bei UPDATE
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TRIGGER IF NOT EXISTS notes_au AFTER UPDATE ON notes BEGIN
                    INSERT INTO notes_fts(notes_fts, rowid, text) VALUES('delete', old.id, old.text);
                    INSERT INTO notes_fts(rowid, text) VALUES (new.id, new.text);
                END";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Indiziert eine Notiz: OCR-Text wird in die Datenbank geschrieben.
    /// </summary>
    /// <param name="filename">Dateiname der Notiz (z.B. "note_20260101_120000")</param>
    /// <param name="text">Erkannter OCR-Text</param>
    /// <returns>ID des eingefügten Eintrags</returns>
    public long IndexNote(string filename, string text)
    {
        EnsureInitialized();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO notes (filename, text, timestamp)
            VALUES (@filename, @text, @timestamp);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@filename", filename);
        cmd.Parameters.AddWithValue("@text", text ?? "");
        cmd.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("o"));

        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : -1;
    }

    /// <summary>
    /// Aktualisiert den OCR-Text einer bestehenden Notiz.
    /// </summary>
    public void UpdateNote(long id, string text)
    {
        EnsureInitialized();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = @"
            UPDATE notes SET text = @text, timestamp = @timestamp
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@text", text ?? "");
        cmd.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Sucht im Index nach Notizen die den Suchbegriff enthalten.
    /// </summary>
    /// <param name="query">Suchbegriff (unterstützt FTS5-Syntax)</param>
    /// <returns>Liste von Treffern mit ID, Dateiname, Text und Zeitstempel</returns>
    public List<SearchResult> SearchNotes(string query)
    {
        EnsureInitialized();

        var results = new List<SearchResult>();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = @"
            SELECT n.id, n.filename, n.text, n.timestamp, snippet(notes_fts, '«', '»', '...', 1, 32) as snippet
            FROM notes_fts f
            JOIN notes n ON n.id = f.rowid
            WHERE notes_fts MATCH @query
            ORDER BY rank
            LIMIT 50";
        cmd.Parameters.AddWithValue("@query", query);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SearchResult(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? "" : reader.GetString(4)
            ));
        }

        return results;
    }

    /// <summary>
    /// Löscht eine Notiz aus dem Index.
    /// </summary>
    public void DeleteNote(long id)
    {
        EnsureInitialized();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "DELETE FROM notes WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Löscht eine Notiz anhand des Dateinamens.
    /// </summary>
    public void DeleteNoteByFilename(string filename)
    {
        EnsureInitialized();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "DELETE FROM notes WHERE filename = @filename";
        cmd.Parameters.AddWithValue("@filename", filename);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Sucht eine Notiz anhand des Dateinamens und gibt die ID zurück (oder -1).
    /// </summary>
    public long FindByFilename(string filename)
    {
        EnsureInitialized();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT id FROM notes WHERE filename = @filename LIMIT 1";
        cmd.Parameters.AddWithValue("@filename", filename);

        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : -1;
    }

    /// <summary>
    /// Gibt die Anzahl der indizierten Notizen zurück.
    /// </summary>
    public int GetNoteCount()
    {
        EnsureInitialized();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM notes";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private void EnsureInitialized()
    {
        if (_connection == null)
            Initialize();
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
    }
}

/// <summary>
/// Suchergebnis für die Volltextsuche.
/// </summary>
public record SearchResult(long Id, string Filename, string Text, string Timestamp, string Snippet);