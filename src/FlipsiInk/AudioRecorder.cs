// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.IO;
using System.Timers;
using System.Windows;

namespace FlipsiInk;

/// <summary>
/// Audio-Aufnahme & Playback für Notizen (wie Notability).
/// Nutzt NAudio für Audio-Aufnahme und Wiedergabe.
/// </summary>
public class AudioRecorder
{
    private string? _currentFilePath;
    private System.Timers.Timer? _positionTimer;
    private bool _isRecording = false;
    private bool _isPaused = false;
    private bool _isPlaying = false;
    private DateTime _recordingStart;
    private TimeSpan _pausedDuration = TimeSpan.Zero;
    private DateTime _pauseStart;

    // NAudio-Objekte (später aktivieren wenn NuGet verfügbar)
    // private WaveIn? _waveIn;
    // private WaveFileWriter? _waveWriter;
    // private WaveOut? _waveOut;
    // private AudioFileReader? _audioFileReader;

    /// <summary>Aufnahme läuft gerade.</summary>
    public bool IsRecording => _isRecording;

    /// <summary>Aufnahme ist pausiert.</summary>
    public bool IsPaused => _isPaused;

    /// <summary>Playback läuft gerade.</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>Aktuelle Playback-Position.</summary>
    public TimeSpan CurrentPosition { get; private set; } = TimeSpan.Zero;

    /// <summary>Wiedergabe-Geschwindigkeit (0.5x, 1.0x, 1.5x, 2.0x).</summary>
    public double PlaybackSpeed { get; set; } = 1.0;

    /// <summary>Wird ausgelöst wenn Aufnahme startet.</summary>
    public event EventHandler? RecordingStarted;

    /// <summary>Wird ausgelöst wenn Aufnahme stoppt.</summary>
    public event EventHandler? RecordingStopped;

    /// <summary>Wird ausgelöst wenn sich die Playback-Position ändert.</summary>
    public event EventHandler<PlaybackPositionEventArgs>? PlaybackPositionChanged;

    /// <summary>Wird ausgelöst wenn Playback endet.</summary>
#pragma warning disable CS0067
    public event EventHandler? PlaybackFinished;
#pragma warning restore CS0067

    /// <summary>
    /// Startet Audio-Aufnahme und speichert als WAV-Datei.
    /// </summary>
    public void StartRecording(string outputPath)
    {
        if (_isRecording) return;

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _currentFilePath = outputPath;
        _isRecording = true;
        _isPaused = false;
        _pausedDuration = TimeSpan.Zero;
        _recordingStart = DateTime.Now;

        // TODO: NAudio WaveIn aktivieren
        // _waveIn = new WaveIn { WaveFormat = new WaveFormat(44100, 16, 1) };
        // _waveWriter = new WaveFileWriter(outputPath, _waveIn.WaveFormat);
        // _waveIn.DataAvailable += (s, e) => _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
        // _waveIn.StartRecording();

        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stoppt die Aufnahme.
    /// </summary>
    public void StopRecording()
    {
        if (!_isRecording) return;

        _isRecording = false;
        _isPaused = false;

        // TODO: NAudio Stop
        // _waveIn?.StopRecording();
        // _waveWriter?.Dispose();
        // _waveIn?.Dispose();
        // _waveIn = null;
        // _waveWriter = null;

        RecordingStopped?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pausiert die Aufnahme.
    /// </summary>
    public void PauseRecording()
    {
        if (!_isRecording || _isPaused) return;

        _isPaused = true;
        _pauseStart = DateTime.Now;

        // TODO: NAudio Pause
        // _waveIn?.StopRecording();
    }

    /// <summary>
    /// Setzt die pausierte Aufnahme fort.
    /// </summary>
    public void ResumeRecording()
    {
        if (!_isRecording || !_isPaused) return;

        _isPaused = false;
        _pausedDuration += DateTime.Now - _pauseStart;

        // TODO: NAudio Resume
        // _waveIn?.StartRecording();
    }

    /// <summary>
    /// Spielt eine Audio-Datei ab.
    /// </summary>
    public void PlayRecording(string filePath)
    {
        if (!File.Exists(filePath)) return;
        if (_isPlaying) StopPlayback();

        _isPlaying = true;
        CurrentPosition = TimeSpan.Zero;

        // TODO: NAudio Playback
        // _audioFileReader = new AudioFileReader(filePath);
        // _waveOut = new WaveOut();
        // _waveOut.Init(_audioFileReader);
        // _waveOut.PlaybackStopped += (s, e) => { _isPlaying = false; PlaybackFinished?.Invoke(this, EventArgs.Empty); };
        // _waveOut.Play();

        // Position-Timer
        _positionTimer = new System.Timers.Timer(100);
        _positionTimer.Elapsed += (s, e) =>
        {
            if (_isPlaying)
            {
                // TODO: CurrentPosition = _audioFileReader.CurrentTime;
                PlaybackPositionChanged?.Invoke(this, new PlaybackPositionEventArgs(CurrentPosition));
            }
        };
        _positionTimer.Start();

        PlaybackPositionChanged?.Invoke(this, new PlaybackPositionEventArgs(TimeSpan.Zero));
    }

    /// <summary>
    /// Stoppt die Wiedergabe.
    /// </summary>
    public void StopPlayback()
    {
        _isPlaying = false;
        _positionTimer?.Stop();
        _positionTimer?.Dispose();
        _positionTimer = null;

        // TODO: NAudio Stop
        // _waveOut?.Stop();
        // _waveOut?.Dispose();
        // _audioFileReader?.Dispose();
        // _waveOut = null;
        // _audioFileReader = null;
    }

    /// <summary>
    /// Gibt die Dauer einer Audio-Datei zurück.
    /// </summary>
    public TimeSpan GetRecordingDuration(string filePath)
    {
        if (!File.Exists(filePath)) return TimeSpan.Zero;

        // WAV-Header-Fallback: Lese Dauer aus WAV-Header
        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);
            // WAV-Header: Bytes 4-7 = Dateigröße - 8
            reader.ReadBytes(4); // "RIFF"
            int fileSize = reader.ReadInt32();
            reader.ReadBytes(4 + 4 + 4 + 2 + 2 + 4 + 4 + 2 + 2); // restlicher Header
            int dataRate = reader.ReadInt32();
            if (dataRate > 0)
                return TimeSpan.FromSeconds((double)(fileSize - 36) / dataRate);
        }
        catch { }

        // TODO: NAudio Duration
        // using var reader = new AudioFileReader(filePath);
        // return reader.TotalTime;

        return TimeSpan.Zero;
    }
}

/// <summary>
/// Event-Args für Playback-Position-Änderungen.
/// </summary>
public class PlaybackPositionEventArgs : EventArgs
{
    public TimeSpan Position { get; }
    public PlaybackPositionEventArgs(TimeSpan position) => Position = position;
}