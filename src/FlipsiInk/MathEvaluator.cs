// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FlipsiInk;

/// <summary>
/// Erweiterte Mathe-Auswertung: Punkt-vor-Strich, Klammern, Potenzen, Wurzeln, Kettenrechnung.
/// Issue #28 (Auto-Trigger), #29 (Komplexe Logik), #31 (Mathe vs. Text).
/// Erweitert: Lokale mathematische Datenbank mit Konstanten, Formeln und Rechenregeln.
/// </summary>
public static class MathEvaluator
{
    // =====================================================================
    // LOKALE MATEMATISCHE DATENBANK – Konstanten, Formeln, Rechenregeln
    // =====================================================================

    /// <summary>
    /// Mathematische und physikalische Konstanten (mindestens 30).
    /// Key = Name/Alias, Value = (Wert, Beschreibung)
    /// </summary>
    private static readonly Dictionary<string, (double value, string description)> Constants = new(StringComparer.OrdinalIgnoreCase)
    {
        // --- Mathematische Konstanten ---
        {"pi", (3.141592653589793, "Kreiszahl π")},
        {"π", (3.141592653589793, "Kreiszahl π")},
        {"e", (2.718281828459045, "Eulersche Zahl e")},
        {"phi", (1.618033988749895, "Goldener Schnitt φ")},
        {"φ", (1.618033988749895, "Goldener Schnitt φ")},
        {"sqrt2", (1.4142135623730951, "Quadratwurzel aus 2")},
        {"√2", (1.4142135623730951, "Quadratwurzel aus 2")},
        {"sqrt3", (1.7320508075688772, "Quadratwurzel aus 3")},
        {"√3", (1.7320508075688772, "Quadratwurzel aus 3")},
        {"sqrt5", (2.23606797749979, "Quadratwurzel aus 5")},
        {"√5", (2.23606797749979, "Quadratwurzel aus 5")},
        {"ln2", (0.6931471805599453, "Natürlicher Logarithmus von 2")},
        {"ln10", (2.302585092994046, "Natürlicher Logarithmus von 10")},
        {"euler_mascheroni", (0.5772156649015329, "Euler-Mascheroni-Konstante γ")},
        {"gamma", (0.5772156649015329, "Euler-Mascheroni-Konstante γ")},
        {"catalan", (0.915965594177219, "Catalansche Konstante G")},
        {"apery", (1.202056903159594, "Apéry-Konstante ζ(3)")},

        // --- Physikalische Konstanten (CODATA 2018/2022) ---
        {"c", (299792458.0, "Lichtgeschwindigkeit m/s")},
        {"h_planck", (6.62607015e-34, "Planck-Konstante J·s")},
        {"hbar", (1.054571817e-34, "Reduzierte Planck-Konstante ℏ J·s")},
        {"k_B", (1.380649e-23, "Boltzmann-Konstante J/K")},
        {"N_A", (6.02214076e23, "Avogadro-Konstante 1/mol")},
        {"R_gas", (8.314462618, "Universelle Gaskonstante J/(mol·K)")},
        {"g_erde", (9.80665, "Erdbeschleunigung m/s²")},
        {"G_grav", (6.67430e-11, "Gravitationskonstante m³/(kg·s²)")},
        {"sigma_sb", (5.670374419e-8, "Stefan-Boltzmann-Konstante W/(m²·K⁴)")},
        {"mu0", (1.25663706212e-6, "Magnetische Feldkonstante μ₀ T·m/A")},
        {"epsilon0", (8.8541878128e-12, "Elektrische Feldkonstante ε₀ F/m")},
        {"e_charge", (1.602176634e-19, "Elementarladung C")},
        {"m_e", (9.1093837015e-31, "Elektronenmasse kg")},
        {"m_p", (1.67262192369e-27, "Protonenmasse kg")},

        // --- Umrechnungsfaktoren ---
        {"inch_cm", (2.54, "Zoll → cm (1 Zoll = 2.54 cm)")},
        {"ft_m", (0.3048, "Fuß → m (1 ft = 0.3048 m)")},
        {"mile_km", (1.609344, "Meile → km (1 Meile = 1.609344 km)")},
        {"lb_kg", (0.45359237, "Pfund → kg (1 lb = 0.45359237 kg)")},
        {"oz_g", (28.349523125, "Unze → g (1 oz = 28.349523125 g)")},
        {"gal_L", (3.785411784, "US-Gallone → Liter")},
        {"deg_rad", (0.017453292519943295, "Grad → Radiant (π/180)")},
        {"rad_deg", (57.29577951308232, "Radiant → Grad (180/π)")},
        {"lbf_N", (4.4482216152605, "Kraft: 1 lbf = 4.448... N")},
        {"atm_Pa", (101325.0, "Atmosphäre → Pascal (1 atm = 101325 Pa)")},
    };

    /// <summary>
    /// Formelsammlung (mindestens 50 Formeln).
    /// Key = Name, Value = (Formel in NCalc-Syntax, Beschreibung, Parameterliste)
    /// </summary>
    private static readonly Dictionary<string, (string formula, string description, string[] parameters)> Formulas = new(StringComparer.OrdinalIgnoreCase)
    {
        // --- Geometrie: Flächen ---
        {"kreis_flaeche", ("Pi * r * r", "Kreisfläche A = π·r²", new[]{"r"})},
        {"rechteck_flaeche", ("a * b", "Rechteckfläche A = a·b", new[]{"a", "b"})},
        {"dreieck_flaeche", ("0.5 * g * h", "Dreiecksfläche A = ½·g·h", new[]{"g", "h"})},
        {"trapez_flaeche", ("0.5 * (a + c) * h", "Trapezfläche A = ½·(a+c)·h", new[]{"a", "c", "h"})},
        {"parallelogramm_flaeche", ("a * h", "Parallelogrammfläche A = a·h", new[]{"a", "h"})},
        {"sechseck_flaeche", ("(3 * Sqrt(3) / 2) * a * a", "Regelmäßiges Sechseck A = (3√3/2)·a²", new[]{"a"})},
        {"ellipse_flaeche", ("Pi * a * b", "Ellipsenfläche A = π·a·b", new[]{"a", "b"})},

        // --- Geometrie: Umfang ---
        {"kreis_umfang", ("2 * Pi * r", "Kreisumfang U = 2·π·r", new[]{"r"})},
        {"rechteck_umfang", ("2 * (a + b)", "Rechteckumfang U = 2·(a+b)", new[]{"a", "b"})},

        // --- Geometrie: Volumen ---
        {"kugel_volumen", ("(4.0/3.0) * Pi * r * r * r", "Kugelvolumen V = ⁴⁄₃πr³", new[]{"r"})},
        {"kugel_oberflaeche", ("4 * Pi * r * r", "Kugeloberfläche O = 4πr²", new[]{"r"})},
        {"zylinder_volumen", ("Pi * r * r * h", "Zylindervolumen V = πr²h", new[]{"r", "h"})},
        {"zylinder_oberflaeche", ("2 * Pi * r * (r + h)", "Zylinderoberfläche O = 2πr(r+h)", new[]{"r", "h"})},
        {"kegel_volumen", ("(1.0/3.0) * Pi * r * r * h", "Kegelvolumen V = ⅓πr²h", new[]{"r", "h"})},
        {"kegel_mantel", ("Pi * r * Sqrt(r*r + h*h)", "Kegelmantelfläche M = πr·√(r²+h²)", new[]{"r", "h"})},
        {"pyramide_volumen", ("(1.0/3.0) * G * h", "Pyramiden volumen V = ⅓Gh", new[]{"G", "h"})},
        {"quader_volumen", ("a * b * c", "Quadervolumen V = a·b·c", new[]{"a", "b", "c"})},

        // --- Algebra ---
        {"pq_formel", ("-p/2 + Sqrt((p/2)*(p/2) - q)", "PQ-Formel x₁ = -p/2 + √((p/2)²-q)", new[]{"p", "q"})},
        {"abc_formel_x1", ("(-b + Sqrt(b*b - 4*a*c)) / (2*a)", "Mitternachtsformel x₁ = (-b+√(b²-4ac))/(2a)", new[]{"a", "b", "c"})},
        {"abc_formel_x2", ("(-b - Sqrt(b*b - 4*a*c)) / (2*a)", "Mitternachtsformel x₂ = (-b-√(b²-4ac))/(2a)", new[]{"a", "b", "c"})},
        {"binom1", ("(a+b)*(a+b)", "1. Binomische Formel (a+b)²", new[]{"a", "b"})},
        {"binom2", ("(a-b)*(a-b)", "2. Binomische Formel (a-b)²", new[]{"a", "b"})},
        {"binom3", ("(a+b)*(a-b)", "3. Binomische Formel (a+b)(a-b)", new[]{"a", "b"})},
        {"potenz_summe", ("a*a + 2*a*b + b*b", "(a+b)² = a²+2ab+b²", new[]{"a", "b"})},
        {"potenz_differenz", ("a*a - 2*a*b + b*b", "(a-b)² = a²-2ab+b²", new[]{"a", "b"})},

        // --- Trigonometrie ---
        {"pythagoras", ("Sqrt(a*a + b*b)", "Satz des Pythagoras c = √(a²+b²)", new[]{"a", "b"})},
        {"pythagoras_kathete", ("Sqrt(c*c - a*a)", "Kathete b = √(c²-a²)", new[]{"c", "a"})},
        {"sin_deg", ("Sin(a * Pi / 180.0)", "sin(a°) mit Gradangabe", new[]{"a"})},
        {"cos_deg", ("Cos(a * Pi / 180.0)", "cos(a°) mit Gradangabe", new[]{"a"})},
        {"tan_deg", ("Tan(a * Pi / 180.0)", "tan(a°) mit Gradangabe", new[]{"a"})},
        {"asin_deg", ("Asin(x) * 180.0 / Pi", "arcsin(x) → Grad", new[]{"x"})},
        {"acos_deg", ("Acos(x) * 180.0 / Pi", "arccos(x) → Grad", new[]{"x"})},
        {"atan_deg", ("Atan(x) * 180.0 / Pi", "arctan(x) → Grad", new[]{"x"})},
        {"bogenmass", ("grad * Pi / 180.0", "Grad → Radiant", new[]{"grad"})},
        {"gradmass", ("rad * 180.0 / Pi", "Radiant → Grad", new[]{"rad"})},

        // --- Finanzmathematik ---
        {"zinseszins", ("K * Pow(1 + p/100.0, n)", "Zinseszins Kₙ = K·(1+p/100)ⁿ", new[]{"K", "p", "n"})},
        {"annuitaet", ("K * (p/100.0) * Pow(1 + p/100.0, n) / (Pow(1 + p/100.0, n) - 1)", "Annuität A = K·qⁿ·(qⁿ-1)·p/100", new[]{"K", "p", "n"})},
        {"abschreibung_linear", ("(A - R) / n", "Lineare Abschreibung a = (A-R)/n", new[]{"A", "R", "n"})},
        {"abschreibung_degressiv", ("A * (1 - p/100.0)", "Degressive Abschreibung Rₙ = A·(1-p/100)ⁿ", new[]{"A", "p"})},
        {"barwert", ("z / (1 + i)", "Barwert BW = z/(1+i)", new[]{"z", "i"})},
        {"endwert", ("z * Pow(1 + i, n)", "Endwert EW = z·(1+i)ⁿ", new[]{"z", "i", "n"})},

        // --- Physik ---
        {"weg", ("v * t", "s = v·t", new[]{"v", "t"})},
        {"geschwindigkeit", ("s / t", "v = s/t", new[]{"s", "t"})},
        {"kraft", ("m * a", "F = m·a", new[]{"m", "a"})},
        {"arbeit", ("F * s", "W = F·s", new[]{"F", "s"})},
        {"leistung", ("W / t", "P = W/t", new[]{"W", "t"})},
        {"kinetische_energie", ("0.5 * m * v * v", "E_kin = ½mv²", new[]{"m", "v"})},
        {"potentielle_energie", ("m * 9.80665 * h", "E_pot = m·g·h (g=9.80665)", new[]{"m", "h"})},
        {"relativitaet", ("m * 299792458.0 * 299792458.0", "E = mc² (c=299792458)", new[]{"m"})},
        {"ohm", ("U / R", "I = U/R (Ohmsches Gesetz)", new[]{"U", "R"})},
        {"leistung_elektrisch", ("U * I", "P = U·I", new[]{"U", "I"})},
        {"widerstand_par", ("1 / (1/R1 + 1/R2)", "Parallelschaltung R = 1/(1/R₁+1/R₂)", new[]{"R1", "R2"})},
        {"dichte", ("m / V", "ρ = m/V", new[]{"m", "V"})},
        {"druck", ("F / A", "p = F/A", new[]{"F", "A"})},
        {"ideales_gas", ("(P * V) / (8.314462618 * T)", "n = PV/(RT) (R=8.314...)", new[]{"P", "V", "T"})},

        // --- Statistik ---
        {"mittelwert", ("summe / n", "Arithmetisches Mittel μ = Σx/n", new[]{"summe", "n"})},
        {"varianz", ("summe_q / n - (summe/n)*(summe/n)", "Varianz σ² = Σx²/n - μ² (Verschiebungssatz)", new[]{"summe", "summe_q", "n"})},
        {"standardabweichung", ("Sqrt(summe_q / n - (summe/n)*(summe/n))", "Standardabweichung σ = √(Varianz)", new[]{"summe", "summe_q", "n"})},

        // --- Sonstiges ---
        {"celsius_fahrenheit", ("c * 9.0/5.0 + 32", "°C → °F", new[]{"c"})},
        {"fahrenheit_celsius", ("(f - 32) * 5.0/9.0", "°F → °C", new[]{"f"})},
        {"kmh_mps", ("kmh / 3.6", "km/h → m/s", new[]{"kmh"})},
        {"mps_kmh", ("ms * 3.6", "m/s → km/h", new[]{"ms"})},
    };

    /// <summary>
    /// Listet alle verfügbaren Konstanten auf (für UI-Anzeige).
    /// </summary>
    public static IEnumerable<(string name, double value, string description)> ListConstants()
        => Constants.Select(kvp => (kvp.Key, kvp.Value.value, kvp.Value.description));

    /// <summary>
    /// Listet alle verfügbaren Formeln auf (für UI-Anzeige).
    /// </summary>
    public static IEnumerable<(string name, string formula, string description, string[] parameters)> ListFormulas()
        => Formulas.Select(kvp => (kvp.Key, kvp.Value.formula, kvp.Value.description, kvp.Value.parameters));

    /// <summary>
    /// Berechnet eine Formel anhand ihres Namens und der Parameterwerte.
    /// Parameter werden den Formelvariablen in Reihenfolge zugeordnet.
    /// </summary>
    public static double? ResolveFormula(string name, double[] args)
    {
        if (!Formulas.TryGetValue(name, out var entry)) return null;
        if (args.Length < entry.parameters.Length) return null;

        try
        {
            var expr = entry.formula;
            var ncalc = new NCalc.Expression(expr, NCalc.EvaluateOptions.IgnoreCase);
            for (int i = 0; i < entry.parameters.Length; i++)
                ncalc.Parameters[entry.parameters[i]] = args[i];
            var result = ncalc.Evaluate();
            if (result is double d) return d;
            if (result is int ii) return (double)ii;
            if (result is long l) return (double)l;
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Ersetzt Konstantennamen in einem Ausdruck durch ihre Werte
    /// und erkennt Sonderoperationen (%, !, |x|, °).
    /// </summary>
    private static string PreprocessExpression(string expr)
    {
        var result = expr;

        // Grad-Symbol bei trigonometrischen Funktionen → Radiant-Umrechnung
        // sin(30°) → sin(30 * pi / 180)
        result = Regex.Replace(result, @"(?i)(sin|cos|tan)\(([^)]+?)°\)", m =>
            $"{m.Groups[1].Value}(({m.Groups[2].Value}) * Pi / 180)");
        result = result.Replace('°', ' '); // verbleibende ° entfernen

        // Prozentrechnung: X% → (X/100)
        result = Regex.Replace(result, @"(\d+[\.\,]?\d*)%", m => $"(({m.Groups[1].Value})/100)");

        // Betrag: |ausdruck| → Abs(ausdruck) – einfache Fälle
        result = Regex.Replace(result, @"\|([^|]+)\|", m => $"Abs({m.Groups[1].Value})");

        // Fakultät: zahl! → Fakultät berechnen
        result = Regex.Replace(result, @"(\d+)!", m =>
        {
            int n = int.Parse(m.Groups[1].Value);
            double f = 1;
            for (int k = 2; k <= n; k++) f *= k;
            return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });

        // Konstanten ersetzen – längste Namen zuerst, um Teil-Überschneidungen zu vermeiden
        foreach (var kvp in Constants.OrderByDescending(k => k.Key.Length))
        {
            result = Regex.Replace(result, $@"\b{Regex.Escape(kvp.Key)}\b",
                kvp.Value.value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        }

        return result;
    }

    // Issue #31: Regex-Heuristik – prüft ob der Text vor "=" eine mathematische Formel ist
    // Mindestens eine Zahl gefolgt von Operatoren/Zahlen/Klammern
    private static readonly Regex MathExpressionPattern = new(
        @"^[\s]*[\d\(][\d\s+\-*/×÷^().,sqrtpiPIeE]+\s*=\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Alternative: Zahl direkt vor "="
    private static readonly Regex NumberBeforeEquals = new(
        @"\d\s*=\s*$",
        RegexOptions.Compiled);

    // Kettenrechnung: Zeile beginnt mit Operator
    private static readonly Regex ChainPattern = new(
        @"^\s*([+\-*/×÷^])\s*(.+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Prüft ob eine Textzeile eine mathematische Formel enthält (Issue #31).
    /// "Das ist = richtig" → false
    /// "3 + 5 =" → true
    /// </summary>
    public static bool IsMathExpression(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        // Muss ein "=" enthalten
        if (!trimmed.Contains('=')) return false;
        // Darf keine Buchstaben enthalten (außer sqrt, pi, e, und Konstantennamen)
        var beforeEquals = trimmed.Substring(0, trimmed.LastIndexOf('='));
        // Erlaubte Buchstaben: s, q, r, t, p, i, e, P, I, E, S, Q, R, T (für sqrt, pi, e)
        var withoutAllowed = Regex.Replace(beforeEquals, @"(?i)sqrt|pi|e", "");
        if (Regex.IsMatch(withoutAllowed, @"[a-zA-ZäöüßÄÖÜ]")) return false;
        // Muss mindestens eine Zahl vor dem "=" haben
        if (!Regex.IsMatch(beforeEquals, @"\d")) return false;
        return true;
    }

    /// <summary>
    /// Hauptauswertung: Erkennt Mathe in mehrzeiligem Text und berechnet Ergebnisse.
    /// </summary>
    public static string Evaluate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var lines = text.Split('\n');
        var results = new StringBuilder();
        double? previousResult = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Kettenrechnung (Issue #29): Zeile beginnt mit +,-,*,/ → vorheriges Ergebnis als Basis
            var chainMatch = ChainPattern.Match(line);
            if (chainMatch.Success && previousResult.HasValue)
            {
                var op = chainMatch.Groups[1].Value;
                var rest = chainMatch.Groups[2].Value.Trim();
                var chainExpr = $"{FormatNumber(previousResult.Value)}{op}{rest}";
                var result = EvaluateExpression(chainExpr);
                if (result.HasValue)
                {
                    results.AppendLine($"{chainExpr} = {FormatNumber(result.Value)}");
                    previousResult = result;
                    continue;
                }
            }

            // Standard-Mathe-Ausdruck mit "="
            if (line.Contains('=') && IsMathExpression(line))
            {
                var beforeEquals = line.Substring(0, line.IndexOf('=')).Trim();
                var result = EvaluateExpression(beforeEquals);
                if (result.HasValue)
                {
                    results.AppendLine($"{beforeEquals} = {FormatNumber(result.Value)}");
                    previousResult = result;
                    continue;
                }
            }

            // Gleichungen: 3x + 5 = 20 → x = 5
            var equationResult = TrySolveEquation(line);
            if (equationResult != null)
            {
                results.AppendLine(equationResult);
                continue;
            }

            // NCalc-Fallback für komplexe Ausdrücke ohne "="
            var ncalcResult = TryNCalc(line);
            if (ncalcResult != null)
            {
                results.AppendLine($"= {FormatNumber(ncalcResult.Value)}");
                previousResult = ncalcResult;
                continue;
            }
        }

        return results.ToString().Trim();
    }

    /// <summary>
    /// Issue #28: Berechnet das Ergebnis für eine Auto-Calc-Eingabe.
    /// Gibt (Formel, Ergebnis) zurück, oder null wenn kein Mathe erkannt.
    /// </summary>
    public static (string formula, string result)? TryAutoCalc(string line, double? previousResult = null)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var trimmed = line.Trim();

        // Kettenrechnung
        var chainMatch = ChainPattern.Match(trimmed);
        if (chainMatch.Success && previousResult.HasValue)
        {
            var op = chainMatch.Groups[1].Value;
            var rest = chainMatch.Groups[2].Value.Trim();
            var chainExpr = $"{FormatNumber(previousResult.Value)}{op}{rest}";
            var result = EvaluateExpression(chainExpr);
            if (result.HasValue)
                return (chainExpr, FormatNumber(result.Value));
        }

        if (!IsMathExpression(trimmed)) return null;

        var beforeEquals = trimmed.Substring(0, trimmed.IndexOf('=')).Trim();
        var evalResult = EvaluateExpression(beforeEquals);
        if (evalResult.HasValue)
            return (beforeEquals, FormatNumber(evalResult.Value));

        return null;
    }

    /// <summary>
    /// Kern-Berechnung: Punkt-vor-Strich, Klammern, ^, sqrt (Issue #29).
    /// Verwendet NCalc für präzise Ergebnisse.
    /// </summary>
    private static double? EvaluateExpression(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return null;

        try
        {
            // Normalisierung: ×→*, ÷→/, Komma→Punkt
            var normalized = expr.Replace('×', '*').Replace('÷', '/').Replace(',', '.');

            // Konstanten, %, !, |x|, ° vorverarbeiten
            normalized = PreprocessExpression(normalized);

            // sqrt → Sqrt für NCalc
            normalized = Regex.Replace(normalized, @"\bsqrt\b", "Sqrt", RegexOptions.IgnoreCase);
            // pi → Pi (falls nicht schon durch Preprocess ersetzt)
            normalized = Regex.Replace(normalized, @"\bpi\b", "Pi", RegexOptions.IgnoreCase);

            // NCalc: a^b → Pow(a,b)
            normalized = Regex.Replace(normalized, @"(\d+\.?\d*)\s*\^\s*(\d+\.?\d*)", "Pow($1,$2)");

            var ncalc = new NCalc.Expression(normalized);
            var result = ncalc.Evaluate();
            if (result is double d && !double.IsNaN(d) && !double.IsInfinity(d))
                return d;
            if (result is int i)
                return (double)i;
            if (result is long l)
                return (double)l;
            if (result is float f)
                return (double)f;
        }
        catch { }

        // Fallback: Eigener simpler Parser für grundlegende Arithmetik
        try
        {
            return SimpleEval(expr);
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Einfacher arithmetischer Parser als Fallback (Punkt-vor-Strich, Klammern).
    /// </summary>
    private static double SimpleEval(string expr)
    {
        var normalized = expr.Replace('×', '*').Replace('÷', '/').Replace(',', '.').Trim();
        var pos = 0;
        double result = ParseAddSub(normalized, ref pos);
        if (pos < normalized.Length) throw new FormatException($"Unerwartetes Zeichen: {normalized[pos]}");
        return result;
    }

    private static double ParseAddSub(string s, ref int pos)
    {
        double left = ParseMulDiv(s, ref pos);
        while (pos < s.Length)
        {
            SkipSpaces(s, ref pos);
            if (pos >= s.Length) break;
            if (s[pos] == '+') { pos++; left += ParseMulDiv(s, ref pos); }
            else if (s[pos] == '-') { pos++; left -= ParseMulDiv(s, ref pos); }
            else break;
        }
        return left;
    }

    private static double ParseMulDiv(string s, ref int pos)
    {
        double left = ParsePower(s, ref pos);
        while (pos < s.Length)
        {
            SkipSpaces(s, ref pos);
            if (pos >= s.Length) break;
            if (s[pos] == '*' || s[pos] == '×') { pos++; left *= ParsePower(s, ref pos); }
            else if (s[pos] == '/' || s[pos] == '÷') { pos++; double divisor = ParsePower(s, ref pos); if (divisor == 0) return double.NaN; left /= divisor; }
            else break;
        }
        return left;
    }

    private static double ParsePower(string s, ref int pos)
    {
        double basenum = ParsePrimary(s, ref pos);
        SkipSpaces(s, ref pos);
        if (pos < s.Length && s[pos] == '^')
        {
            pos++;
            double exponent = ParsePower(s, ref pos); // Rechtsassoziativ
            return Math.Pow(basenum, exponent);
        }
        return basenum;
    }

    private static double ParsePrimary(string s, ref int pos)
    {
        SkipSpaces(s, ref pos);
        if (pos >= s.Length) throw new FormatException("Unerwartetes Ende");

        // Unäres Minus
        if (s[pos] == '-')
        {
            pos++;
            return -ParsePrimary(s, ref pos);
        }
        if (s[pos] == '+')
        {
            pos++;
            return ParsePrimary(s, ref pos);
        }

        // Klammern
        if (s[pos] == '(')
        {
            pos++;
            double result = ParseAddSub(s, ref pos);
            SkipSpaces(s, ref pos);
            if (pos < s.Length && s[pos] == ')') pos++;
            return result;
        }

        // sqrt()
        if (pos + 4 <= s.Length && s.Substring(pos, 4).ToLower() == "sqrt")
        {
            pos += 4;
            SkipSpaces(s, ref pos);
            if (pos < s.Length && s[pos] == '(') { pos++; double inner = ParseAddSub(s, ref pos); SkipSpaces(s, ref pos); if (pos < s.Length && s[pos] == ')') pos++; return Math.Sqrt(inner); }
            return Math.Sqrt(ParsePrimary(s, ref pos));
        }

        // Zahl
        int start = pos;
        bool hasDot = false;
        while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.' || (s[pos] == ',' && !hasDot))) { if (s[pos] == '.' || s[pos] == ',') hasDot = true; pos++; }
        if (pos == start) throw new FormatException($"Zahl erwartet bei Position {pos}");
        return double.Parse(s.Substring(start, pos - start).Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void SkipSpaces(string s, ref int pos)
    {
        while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
    }

    /// <summary>
    /// Versucht eine einfache Gleichung zu lösen (z.B. 3x + 5 = 20 → x = 5)
    /// </summary>
    private static string? TrySolveEquation(string line)
    {
        var match = Regex.Match(line, @"^(\d*)\s*([a-zA-Z])\s*([+\-])\s*(\d+[\.\,]?\d*)\s*=\s*(\d+[\.\,]?\d*)$");
        if (!match.Success) return null;

        try
        {
            var coefStr = match.Groups[1].Value;
            var variable = match.Groups[2].Value;
            var op = match.Groups[3].Value;
            var constant = double.Parse(match.Groups[4].Value.Replace(',', '.'));
            var equals = double.Parse(match.Groups[5].Value.Replace(',', '.'));
            var coef = string.IsNullOrEmpty(coefStr) ? 1 : double.Parse(coefStr);

            double x = op == "+" ? (equals - constant) / coef : (equals + constant) / coef;
            return $"{variable} = {FormatNumber(x)}";
        }
        catch { return null; }
    }

    /// <summary>
    /// NCalc-Fallback für Ausdrücke ohne "=".
    /// </summary>
    private static double? TryNCalc(string expr)
    {
        try
        {
            var normalized = expr.Replace('×', '*').Replace('÷', '/').Replace(',', '.');
            // Konstanten, %, !, |x|, ° vorverarbeiten
            normalized = PreprocessExpression(normalized);
            normalized = Regex.Replace(normalized, @"\bsqrt\b", "Sqrt", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"\bpi\b", "Pi", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"(\d+\.?\d*)\s*\^\s*(\d+\.?\d*)", "Pow($1,$2)");

            var ncalc = new NCalc.Expression(normalized);
            var result = ncalc.Evaluate();
            if (result is double d && !double.IsNaN(d) && !double.IsInfinity(d)) return d;
            if (result is int i) return (double)i;
            if (result is long l) return (double)l;
        }
        catch { }
        return null;
    }

    private static string FormatNumber(double n)
    {
        if (n == Math.Floor(n) && Math.Abs(n) < 1e15)
            return ((long)n).ToString();
        return n.ToString("G6");
    }
}