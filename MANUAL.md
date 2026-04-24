# FlipsiInk – Betriebsanleitung / Manual

> ⚠️ **EARLY ALPHA** – Diese Anleitung wird mit jeder Version aktualisiert.

## Installation

1. Neueste `FlipsiInk_Setup_x.x.x.exe` von [Releases](https://github.com/TechFlipsi/FlipsiInk/releases/latest) herunterladen
2. Installer ausführen
3. App startet automatisch
4. Fertig! 🎉

## Stift-Eingabe

FlipsiInk unterstützt alle Stylus-Geräte mit Druckempfindlichkeit:
- Microsoft Surface Pen
- HP 2-in-1 Stylus
- Dell Active Pen
- Wacom Stylus
- Jeder Windows-kompatible Stift

### Eingabe-Modi
- 🖊️ **Stift** – Nur Stifteingabe (Finger wird ignoriert)
- 👆 **Touch** – Nur Fingereingabe
- ✋ **Beides** – Stift und Finger gleichzeitig

## Mathe-System

### Intelligenter Mathe-Trigger (=)
Beim Tippen von "=" wird automatisch der vorherige Ausdruck berechnet:
- `3 + 5 =` → Ergebnis: **8** (in Blau)
- `(10 + 5) * 2 =` → Ergebnis: **30**
- `sqrt(144) =` → Ergebnis: **12**

### Auto-Calc Toggle
Der Button in der Toolbar schaltet die automatische Berechnung ein/aus.

### Kettenrechnung
Beginnt eine neue Zeile mit einem Operator, wird das vorherige Ergebnis als Basis genommen:
```
5 + 3 = 8
* 2 = 16
+ 10 = 26
```

### Konstanten (40 Stück)
Eingabe des Namens wird automatisch ersetzt:
- `pi` → 3.14159265358979
- `e` → 2.71828182845905
- `phi` → 1.61803398874989 (Goldener Schnitt)
- `c` → 299792458 (Lichtgeschwindigkeit)
- `g` → 9.80665 (Erdbeschleunigung)
- Alle physikalischen Konstanten verfügbar

### Formeln (55 Stück)
Aufruf per Name + Parameter:
- `kreis_flaeche(5)` → 78.54
- `kugel_volumen(3)` → 113.1
- `zinseszins(1000, 5, 3)` → 1157.63
- `pythagoras(3, 4)` → 5

### Erweiterte Rechenregeln
- **Prozentrechnung:** `15% von 200` → 30
- **Fakultät:** `5!` → 120
- **Betrag:** `|-5|` → 5
- **Grad/Radiant:** `sin(30°)` → 0.5

## OCR-Features

### Handschrifterkennung
Strg+Umschalt+R – Erkennt handgeschriebenen Text und zeigt ihn im rechten Panel an.

### Smart-Detection
Erkannte E-Mail-Adressen, Telefonnummern und URLs werden automatisch klickbar dargestellt.

### Volltextsuche
🔍-Button im rechten Panel öffnet die Suche über alle gespeicherten Notizen (SQLite FTS5).

## Layout

### Modern-Layout (Default)
Horizontale Toolbar unter der Top-Bar – ähnlich wie professionelle Notiz-Apps.

### Klassisch-Layout
Vertikale Toolbar links – kompakt und platzsparend.

Umschaltbar per 📐-Button in der Top-Bar. Einstellung wird gespeichert.

## Auto-Update
- Prüft automatisch alle 15 Minuten auf neue Versionen
- Manueller Check per 🔄-Button
- Download + Silent-Install bei gefundenem Update

## Auto-Tidy
✨-Button richtet Handschrift auf:
- Krumme Linien werden geradegezogen
- Handgezeichnete Kreise → perfekte Kreise
- Gleichmäßige Abstände zwischen Textblöcken

## Tastenkürzel

| Shortcut | Funktion |
|---|---|
| Ctrl+Z | Rückgängig |
| Ctrl+Y | Wiederholen |
| Ctrl+Shift+R | Text erkennen |
| Ctrl+Shift+M | Mathe berechnen |
| Ctrl++ | Zoom rein |
| Ctrl+- | Zoom raus |
| Ctrl+0 | Zoom zurücksetzen |

## Datenschutzhinweis
Alle Prozesse laufen lokal. Keine Daten verlassen den Rechner. Kein Cloud-Upload. Kein Account nötig.

---

*FlipsiInk – Geschrieben in C# / .NET 8 | Idee: Fabian Kirchweger | Code: GLM-5.1 (via OpenClaw)*