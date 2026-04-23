// FlipsiInk - Laser Pointer Tool
// Copyright (C) 2025 FlipsiInk Contributors
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FlipsiInk
{
    /// <summary>
    /// Laser-Pointer und Präsentationsmodus.
    /// Erzeugt einen leuchtenden Punkt mit Spur, der dem Stift folgt.
    /// Im Präsentationsmodus werden alle Stift-Eingaben als Laser interpretiert.
    /// </summary>
    public class LaserPointerTool
    {
        #region Eigenschaften

        /// <summary>Laser-Modus aktiv</summary>
        public bool IsActive { get; set; } = false;

        /// <summary>Farbe des Laserpunkts (Standard: Rot)</summary>
        public Color LaserColor { get; set; } = Colors.Red;

        /// <summary>Wie viele Punkte die Spur lang bleibt (Standard: 15)</summary>
        public int TrailLength { get; set; } = 15;

        /// <summary>Spur verschwindet nach X ms (Standard: 1500ms)</summary>
        public int TrailDuration { get; set; } = 1500;

        /// <summary>Präsentationsmodus – nur Laser, keine versehentlichen Markierungen</summary>
        public bool IsPresentationMode { get; set; } = false;

        /// <summary>Verfügbare Laser-Farben</summary>
        public static readonly Color[] AvailableColors = { Colors.Red, Colors.Blue, Colors.Green };

        #endregion

        #region Private Felder

        private readonly List<TrailPoint> _trailPoints = new();
        private readonly DispatcherTimer _fadeTimer;
        private Point? _currentPosition;
        private bool _isLaserActive;

        #endregion

        #region Konstruktor

        public LaserPointerTool()
        {
            _fadeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _fadeTimer.Tick += OnFadeTimerTick;
        }

        #endregion

        #region Öffentliche Methoden

        /// <summary>Laser-Punkt an Position setzen</summary>
        public void StartLaser(Point position)
        {
            _isLaserActive = true;
            _currentPosition = position;
            _trailPoints.Clear();
            AddTrailPoint(position);
            _fadeTimer.Start();
        }

        /// <summary>Laser bewegen – neuen Punkt zur Spur hinzufügen</summary>
        public void MoveLaser(Point position)
        {
            if (!_isLaserActive) return;
            _currentPosition = position;
            AddTrailPoint(position);
        }

        /// <summary>Laser ausblenden</summary>
        public void EndLaser()
        {
            _isLaserActive = false;
            _currentPosition = null;
            // Spur bleibt noch sichtbar und faded aus
        }

        /// <summary>Spur komplett löschen</summary>
        public void ClearTrail()
        {
            _trailPoints.Clear();
            _fadeTimer.Stop();
        }

        /// <summary>Erstellt den visuellen Laser-Punkt und Spur als DrawingVisual</summary>
        public DrawingVisual CreateLaserVisual()
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // Spur zeichnen – Punkte mit abnehmender Deckkraft
                for (int i = 0; i < _trailPoints.Count; i++)
                {
                    var tp = _trailPoints[i];
                    double opacity = tp.GetOpacity();
                    double size = 4 + (6 * opacity); // Größer wenn neuer

                    var brush = new SolidColorBrush(Color.FromArgb(
                        (byte)(255 * opacity),
                        LaserColor.R,
                        LaserColor.G,
                        LaserColor.B));

                    // Äußerer Leuchteffekt
                    var glowBrush = new SolidColorBrush(Color.FromArgb(
                        (byte)(80 * opacity),
                        LaserColor.R,
                        LaserColor.G,
                        LaserColor.B));

                    context.DrawEllipse(glowBrush, null, tp.Position, size + 8, size + 8);
                    context.DrawEllipse(brush, null, tp.Position, size, size);

                    // Heller Kern
                    var coreBrush = new SolidColorBrush(Color.FromArgb(
                        (byte)(200 * opacity), 255, 255, 255));
                    context.DrawEllipse(coreBrush, null, tp.Position, size * 0.3, size * 0.3);
                }

                // Aktueller Laser-Punkt (größer und heller)
                if (_currentPosition.HasValue && _isLaserActive)
                {
                    var glowOuter = new SolidColorBrush(Color.FromArgb(60, LaserColor.R, LaserColor.G, LaserColor.B));
                    var glowInner = new SolidColorBrush(LaserColor);
                    var core = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));

                    context.DrawEllipse(glowOuter, null, _currentPosition.Value, 20, 20);
                    context.DrawEllipse(glowInner, null, _currentPosition.Value, 8, 8);
                    context.DrawEllipse(core, null, _currentPosition.Value, 3, 3);
                }
            }
            return visual;
        }

        /// <summary>Gibt die aktuellen Spur-Punkte zurück</summary>
        public List<Point> GetTrailPoints()
        {
            var points = new List<Point>(_trailPoints.Count);
            foreach (var tp in _trailPoints)
            {
                points.Add(tp.Position);
            }
            return points;
        }

        #endregion

        #region Private Methoden

        private void AddTrailPoint(Point position)
        {
            _trailPoints.Add(new TrailPoint(position, TrailDuration));
            // Spur auf maximale Länge begrenzen
            while (_trailPoints.Count > TrailLength)
            {
                _trailPoints.RemoveAt(0);
            }
        }

        private void OnFadeTimerTick(object? sender, EventArgs e)
        {
            // Abgelaufene Punkte entfernen
            _trailPoints.RemoveAll(tp => tp.IsExpired);

            if (_trailPoints.Count == 0 && !_isLaserActive)
            {
                _fadeTimer.Stop();
            }
        }

        #endregion

        #region Innere Klasse

        /// <summary>Ein Punkt in der Laser-Spur mit Ablaufzeit</summary>
        private class TrailPoint
        {
            public Point Position { get; }
            private readonly DateTime _createdAt;
            private readonly int _durationMs;

            public TrailPoint(Point position, int durationMs)
            {
                Position = position;
                _createdAt = DateTime.Now;
                _durationMs = durationMs;
            }

            /// <summary>Ob der Punkt bereits abgelaufen ist</summary>
            public bool IsExpired =>
                (DateTime.Now - _createdAt).TotalMilliseconds > _durationMs;

            /// <summary>Deckkraft basierend auf verbleibender Lebensdauer (1.0 → 0.0)</summary>
            public double GetOpacity()
            {
                var elapsed = (DateTime.Now - _createdAt).TotalMilliseconds;
                var remaining = 1.0 - Math.Min(elapsed / _durationMs, 1.0);
                return Math.Max(remaining, 0.0);
            }
        }

        #endregion
    }
}