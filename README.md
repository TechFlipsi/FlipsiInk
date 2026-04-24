# FlipsiInk v0.2.0

[![Build](https://img.shields.io/github/actions/workflow/status/TechFlipsi/FlipsiInk/build.yml?branch=main&label=Build)](https://github.com/TechFlipsi/FlipsiInk/actions)
[![Version](https://img.shields.io/github/v/release/TechFlipsi/FlipsiInk?label=Version)](https://github.com/TechFlipsi/FlipsiInk/releases/latest)
[![License](https://img.shields.io/github/license/TechFlipsi/FlipsiInk?label=License)](https://github.com/TechFlipsi/FlipsiInk/LICENSE)
[![Downloads](https://img.shields.io/github/downloads/TechFlipsi/FlipsiInk/total?label=Downloads)](https://github.com/TechFlipsi/FlipsiInk/releases)
[![Discord](https://img.shields.io/discord/1496261911677894867?label=Discord)](https://discord.gg/HnCZY54U7)

🖊️ **KI-gestützte Handschrift- & Mathe-Notiz-App** – von [TechFlipsi](https://github.com/TechFlipsi)

📖 **[Betriebsanleitung / Manual](MANUAL.md)** – Installation, Features, Mathe-System, Tastenkürzel & mehr

> ⚠️ **EARLY ALPHA – Noch keine voll funktionsfähige Version!** Die App befindet sich in aktiver Entwicklung. Features können unvollständig oder fehlerhaft sein. Für Tests und Feedback willkommen!

## Features
- ✏️ Stift-Eingabe mit Druckempfindlichkeit (Surface Pen, HP, Dell, Wacom etc.)
- 🎨 Farben: Schwarz, Blau, Rot, Grün + Stiftstärken (dünn/mittel/dick)
- 🖍️ Textmarker-Modus (halbdtransparent)
- 🧹 Radierer (Stroke-basiert)
- ↩️ Undo/Redo (50 Schritte)
- 🔤 **Handschrifterkennung** via ONNX Runtime
- 🧮 **Intelligenter Mathe-Trigger** – "=" tippen → automatisch berechnen
- 📐 **55 Formeln & 40 Konstanten** – Geometrie, Algebra, Physik, Finanz, Trigonometrie
- ⛓️ **Kettenrechnung** – Zeile beginnt mit +,-,*,/ → vorheriges Ergebnis als Basis
- 🛡️ **Offline-Integrität** – Unterscheidet Mathe von Text ("Das ist = richtig" → keine Berechnung)
- ✨ **Auto-Tidy** – Krumme Zeilen gerade, Formen perfekt, Abstände gleichmäßig
- 🖱️ **Kontext-Sensitive Leiste** – Markierung → Zusammenfassen, Todo, Graph, Währung
- 🔍 **Smart-Detection** – E-Mail, Telefon, URL automatisch klickbar
- 🔎 **Volltextsuche** – SQLite FTS5 sucht in allen handschriftlichen Notizen
- 🌓 Dark/Light Mode (System/Hell/Dunkel)
- 💾 Speichern als PNG + JSON
- 📐 **Layout-Switcher** – Modern (horizontal) + Klassisch (vertikal)
- 🔄 **Auto-Update** – prüft alle 15 Min + manueller Button (🔄)
- 🔌 Komplett offline – kein Internet nötig (außer für Updates)
- 🇩🇪🇬🇧 Deutsch & Englisch

## Systemanforderungen
- Windows 10/11 (x64)
- 8 GB RAM Minimum
- Touchscreen mit Stift empfohlen (Surface, HP 2-in-1, Dell etc.)
- ~500 MB Speicher (inkl. KI-Modell)

## Installation
1. Neueste `FlipsiInk_Setup_x.x.x.exe` von [Releases](https://github.com/TechFlipsi/FlipsiInk/releases/latest) herunterladen
2. Installer ausführen
3. Fertig! 🎉

> ⚠️ **Alpha-Hinweis:** Dies ist eine frühe Entwicklungsversion. Datenverlust möglich – regelmäßig Backup machen!

## Tastenkürzel
| Shortcut | Funktion |
|---|---|
| Ctrl+Z | Rückgängig |
| Ctrl+Y | Wiederholen |
| Ctrl+Shift+R | Text erkennen |
| Ctrl+Shift+M | Mathe berechnen |

## Lizenz
GPL v3 – Copyright © 2026 Fabian Kirchweger