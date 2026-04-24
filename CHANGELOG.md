# FlipsiInk – CHANGELOG

Alle bemerkenswerten Änderungen werden hier dokumentiert.

Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/),
die Versionierung folgt [Semantic Versioning](https://semver.org/lang/de/).

## [0.2.0] - 2026-04-24

### Hinzugefügt
- ✨ **Intelligenter Mathe-Trigger (=)** – Auto-Berechnung bei "=" Eingabe (Issue #28)
- ✨ **Komplexe Rechen-Logik** – Punkt-vor-Strich, Klammern, Potenzen, Wurzeln, Kettenrechnung (Issue #29)
- ✨ **Offline-Integrität** – Regex-Heuristik unterscheidet Mathe von Text (Issue #31)
- ✨ **Auto-Tidy** – Layout-Reinigung, Formen geradeziehen, Abstände gleichmäßig (Issue #34)
- ✨ **Kontext-Sensitive Aktionsleiste** – Schwebendes Popup bei Selektion (Issue #35)
- ✨ **OCR-Mehrwert** – Smart-Detection (E-Mail/Telefon/URL klickbar), Formel-Konverter, Volltextsuche (Issue #30)
- ✨ **Mathe-Datenbank** – 40 Konstanten (π, e, φ, physikalisch), 55 Formeln (Geometrie, Algebra, Trigonom., Finanz, Physik)
- ✨ **Erweiterte Rechenregeln** – Prozentrechnung, Fakultät, Betrag, Deg/Rad, Bruchrechnung
- ✨ **Auto-Updater** – 15-Min-Prüfung + manueller Button (Issue #7)
- ✨ **Layout-Switcher** – Modern (horizontal) + Klassisch (vertikal), umschaltbar per Button
- 🌐 **Repo öffentlich** mit Alpha-Warnung und Badges

### Behoben
- 🐛 NullReferenceException beim Start – TemplateCombo_SelectionChanged feuerte vor InitializeComponent()
- 🐛 Namespace-Ambiguities (Point, Size, Rect) zwischen System.Drawing und System.Windows

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