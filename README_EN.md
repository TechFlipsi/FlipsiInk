# FlipsiInk v0.5.0

[![Build](https://img.shields.io/github/actions/workflow/status/TechFlipsi/FlipsiInk/build.yml?branch=main&label=Build)](https://github.com/TechFlipsi/FlipsiInk/actions)
[![Version](https://img.shields.io/github/v/release/TechFlipsi/FlipsiInk?label=Version)](https://github.com/TechFlipsi/FlipsiInk/releases/latest)
[![License](https://img.shields.io/github/license/TechFlipsi/FlipsiInk?label=License)](https://github.com/TechFlipsi/FlipsiInk/LICENSE)
[![Downloads](https://img.shields.io/github/downloads/TechFlipsi/FlipsiInk/total?label=Downloads)](https://github.com/TechFlipsi/FlipsiInk/releases)
[![Discord](https://img.shields.io/discord/1496261911677894867?label=Discord)](https://discord.gg/HnCZY54U7)

🖊️ **AI-powered Handwriting & Math Notes App** – by [TechFlipsi](https://github.com/TechFlipsi)

📖 **[Manual](MANUAL.md)** – Installation, Features, Math System, Shortcuts & more

> ⚠️ **EARLY ALPHA – Not a fully functional version yet!** The app is under active development. Features may be incomplete or buggy. Testers and feedback welcome!

## Features
- ✏️ Pen input with pressure sensitivity (Surface Pen, HP, Dell, Wacom etc.)
- 🎨 Colors: Black, Blue, Red, Green + pen sizes (thin/medium/thick)
- 🖍️ Highlighter mode (semi-transparent)
- 🧹 Eraser (stroke-based)
- ↩️ Undo/Redo (50 steps)
- 🔤 **Handwriting recognition** via ONNX Runtime
- 🧮 **Smart Math Trigger** – type "=" → auto-calculate
- 📐 **55 Formulas & 40 Constants** – Geometry, Algebra, Physics, Finance, Trigonometry
- ⛓️ **Chain Calculation** – line starts with +,-,*,/ → uses previous result as base
- 🛡️ **Offline Integrity** – Distinguishes math from text ("This is = right" → no calculation)
- ✨ **Auto-Tidy** – Straighten lines, perfect shapes, even spacing
- 🖱️ **Context-Sensitive Bar** – Selection → Summarize, Todo, Graph, Currency
- 🔍 **Smart Detection** – Email, phone, URL auto-detected and clickable
- 🔎 **Full-text Search** – SQLite FTS5 searches all handwritten notes
- 🌓 Dark/Light Mode (System/Light/Dark)
- 💾 Save as PNG + JSON
- 📐 **Layout Switcher** – Modern (horizontal) + Classic (vertical)
- 🔄 **Auto-Update** – checks every 15 min + manual button (🔄)
- 🔌 Fully offline – no internet required (except for updates)
- 🇩🇪🇬🇧 German & English

## System Requirements
- Windows 10/11 (x64)
- 8 GB RAM minimum
- Touchscreen with pen recommended (Surface, HP 2-in-1, Dell etc.)
- ~500 MB storage (incl. AI model)

## Installation
1. Download latest `FlipsiInk_Setup_x.x.x.exe` from [Releases](https://github.com/TechFlipsi/FlipsiInk/releases/latest)
2. Run installer
3. Done! 🎉

> ⚠️ **Alpha Notice:** This is an early development version. Data loss possible – make regular backups!

## Keyboard Shortcuts
| Shortcut | Function |
|---|---|
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+Shift+R | Recognize text |
| Ctrl+Shift+M | Calculate math |

## License
GPL v3 – Copyright © 2026 Fabian Kirchweger