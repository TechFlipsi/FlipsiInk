# InkNote v0.1.0

📝 **KI-gestützte Handschrift- & Mathe-Notiz-App**

⚠️ **PRIVATE REPO – Noch in Entwicklung!**

## Features
- ✏️ Stift-Eingabe mit Druckempfindlichkeit (jeder Stift: Surface Pen, HP, Dell, Wacom etc.)
- 🎨 Farben: Schwarz, Blau, Rot, Grün + Stiftstärken
- 🖍️ Textmarker-Modus
- 🧹 Radierer
- 🔤 **Handschrifterkennung** via ONNX Runtime (TrOCR large oder kompatibles Modell)
- 🧮 **Mathe-Ausdrücke berechnen** – schreibe "3x + 5 = 20" → App berechnet x = 5
- 📏 Linien/Formen (geplant)
- 💾 Speichern als PNG + JSON (für erneutes Bearbeiten)
- 🔌 Komplett offline – kein Internet nötig
- 🔄 Auto-Update von GitHub Releases

## Systemanforderungen
- Windows 10/11 (x64)
- 8 GB RAM Minimum (6 GB funktionieren, aber langsamer)
- Touchscreen mit Stift empfohlen
- ~500 MB Speicher (inkl. KI-Modell)

## KI-Modell
Das Modell muss manuell im Ordner `Models/` abgelegt werden:
- `model.onnx` – TrOCR large oder kompatibles ONNX-Modell
- Das Modell wird mit der App gebündelt (für Auto-Update)

## Tastenkürzel
| Shortcut | Funktion |
|---|---|
| Ctrl+Z | Rückgängig |
| Ctrl+Shift+R | Text erkennen |
| Ctrl+Shift+M | Mathe berechnen |

## Lizenz
GPL v3 – Copyright © 2026 Fabian Kirchweger