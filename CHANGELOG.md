# FlipsiInk – CHANGELOG

Alle bemerkenswerten Änderungen werden hier dokumentiert.

Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/),
die Versionierung folgt [Semantic Versioning](https://semver.org/lang/de/).

## [0.1.1] - 2026-04-23

### Behoben
- 🐛 NullReferenceException beim Start – TemplateCombo_SelectionChanged feuerte vor InitializeComponent()

## [0.1.0] - 2026-04-23

### Hinzugefügt
- Stift-Canvas mit Druckempfindlichkeit (Issue #1)
- Seitenvorlagen / Paper Templates (Issue #17)
- PDF Import & Annotation (Issue #11)
- Ordnerstruktur & Notiz-Verwaltung (Issue #13)
- Undo/Redo (Issue #24)
- Zoom (Issue #25)
- Eingabe-Modus: Stift/Touch/Beides (Issue #27)
- Dark Mode / Theme-System (Issue #8)
- OCR-Erkennung (Issue #2, ONNX-basiert)
- Mathe-Ausdrücke berechnen (Issue #3)
- Linien & Formen erkennen (Issue #4)
- Speichern & Laden von Notizen (Issue #5)
- Inno Setup Installer
- App-Icon
- GPL v3 Lizenz

### Behoben
- Namespace-Mehrdeutigkeiten zwischen System.Drawing und WPF
- Fehlende Icon-Referenz in csproj
- StylusPoint/StylusPointCollection Namespace-Fehler
- Silent-Crash beim Start – jetzt mit Fehlermeldung