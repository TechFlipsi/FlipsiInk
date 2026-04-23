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
/// </summary>
public static class MathEvaluator
{
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
        // Darf keine Buchstaben enthalten (außer sqrt, pi, e)
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

            // sqrt → Sqrt für NCalc
            normalized = Regex.Replace(normalized, @"\bsqrt\b", "Sqrt", RegexOptions.IgnoreCase);
            // pi → Pi
            normalized = Regex.Replace(normalized, @"\bpi\b", "Pi", RegexOptions.IgnoreCase);

            // NCalc unterstützt ^ nicht nativ → ersetze durch Pow(x,y) ist komplex,
            // aber NCalc kennt 'x ^ y' nicht. Wir konvertieren: a^b → Pow(a,b)
            // Einfache Fälle: Zahl^Zahl
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