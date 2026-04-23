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
using System.Text.RegularExpressions;
using NCalc;

namespace FlipsiInk;

/// <summary>
/// Formel-Konverter (Issue #30): Wandelt OCR-erkannte mathematische Ausdrücke
/// (z.B. "3x + 5 = 20") in NCalc-kompatible Strings um und berechnet diese.
/// Integriert mit dem existierenden MathEvaluator.
/// </summary>
public static class FormulaConverter
{
    // Variablemuster: Buchstabe oder Buchstabenfolge mit optionalem Koeffizienten
    private static readonly Regex VariablePattern = new(
        @"(\d*)\s*([a-zA-Z])",
        RegexOptions.Compiled);

    // Gleichungsmuster: Ausdruck = Ausdruck
    private static readonly Regex EquationPattern = new(
        @"^(.+?)\s*=\s*(.+)$",
        RegexOptions.Compiled);

    // Multiplikation implizit: 2(3) → 2*(3), 3x → 3*x
    private static readonly Regex ImplicitMultPattern = new(
        @"(\d)\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// Analysiert den OCR-Text auf mathematische Formeln und gibt die Ergebnisse zurück.
    /// </summary>
    public static string ConvertAndCalculate(string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText)) return "";

        var lines = ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // 1. Prüfe ob es eine Gleichung mit Variablen ist (z.B. "3x + 5 = 20")
            var equationResult = TryConvertEquation(trimmed);
            if (equationResult != null)
            {
                results.Add(equationResult);
                continue;
            }

            // 2. Prüfe auf implizite Multiplikation (z.B. "2(3+4)")
            var convertedExpr = ConvertImplicitMultiplication(trimmed);
            var ncalcResult = TryEvaluateNCalc(convertedExpr);
            if (ncalcResult != null)
            {
                results.Add($"{trimmed} = {ncalcResult}");
                continue;
            }

            // 3. Fallback: existierenden MathEvaluator nutzen
            var mathResult = MathEvaluator.Evaluate(trimmed);
            if (!string.IsNullOrEmpty(mathResult))
            {
                results.Add(mathResult);
            }
        }

        return string.Join("\n", results);
    }

    /// <summary>
    /// Versucht eine Gleichung mit Variablen zu lösen (z.B. "3x + 5 = 20" → "x = 5").
    /// </summary>
    private static string? TryConvertEquation(string line)
    {
        var eqMatch = EquationPattern.Match(line);
        if (!eqMatch.Success) return null;

        var left = eqMatch.Groups[1].Value.Trim();
        var right = eqMatch.Groups[2].Value.Trim();

        // Prüfe ob Variable(n) enthalten sind
        var variables = VariablePattern.Matches(left + " " + right);
        if (variables.Count == 0) return null;

        // Einfache lineare Gleichung: ax + b = c
        // Pattern: Koeffizient * Variable ± Konstante = Ergebnis
        var simpleEq = Regex.Match(line,
            @"^(\d*)\s*([a-zA-Z])\s*([+\-])\s*(\d+[\.\,]?\d*)\s*=\s*(\d+[\.\,]?\d*)$");
        if (simpleEq.Success)
        {
            var coefStr = simpleEq.Groups[1].Value;
            var variable = simpleEq.Groups[2].Value;
            var op = simpleEq.Groups[3].Value;
            var constant = double.Parse(simpleEq.Groups[4].Value.Replace(',', '.'));
            var equals = double.Parse(simpleEq.Groups[5].Value.Replace(',', '.'));
            var coef = string.IsNullOrEmpty(coefStr) ? 1.0 : double.Parse(coefStr);

            double x = op == "+" ? (equals - constant) / coef : (equals + constant) / coef;
            return $"{variable} = {FormatNumber(x)}";
        }

        // Erweiterte Gleichung: Versuche NCalc-basierte Lösung
        // z.B. "2x + 3 = 7" → x = 2
        var complexEq = Regex.Match(line,
            @"^([\d.]*)\s*([a-zA-Z])\s*([+\-*/×÷])\s*(\d+[\.\,]?\d*)\s*=\s*(\d+[\.\,]?\d*)$");
        if (complexEq.Success)
        {
            var coefStr2 = complexEq.Groups[1].Value;
            var variable2 = complexEq.Groups[2].Value;
            var op2 = complexEq.Groups[3].Value;
            var const2 = double.Parse(complexEq.Groups[4].Value.Replace(',', '.'));
            var equals2 = double.Parse(complexEq.Groups[5].Value.Replace(',', '.'));
            var coef2 = string.IsNullOrEmpty(coefStr2) ? 1.0 : double.Parse(coefStr2);

            double result2 = op2 switch
            {
                "+" => (equals2 - const2) / coef2,
                "-" => (equals2 + const2) / coef2,
                "*" or "×" => equals2 / (coef2 * const2),
                "/" or "÷" => (equals2 * const2) / coef2,
                _ => double.NaN
            };

            if (!double.IsNaN(result2))
                return $"{variable2} = {FormatNumber(result2)}";
        }

        return null;
    }

    /// <summary>
    /// Konvertiert implizite Multiplikation: "2(3+4)" → "2*(3+4)", "3x" → "3*x"
    /// </summary>
    private static string ConvertImplicitMultiplication(string expr)
    {
        var result = ImplicitMultPattern.Replace(expr, "$1*(");

        // Zahl gefolgt von Variable: "3x" → "3*x"
        result = Regex.Replace(result, @"(\d)\s*([a-zA-Z])", "$1*$2");

        // Schließende Klammer gefolgt von Zahl/Variable: ")3" → ")*3"
        result = Regex.Replace(result, @"\)\s*(\d)", ")*$1");
        result = Regex.Replace(result, @"\)\s*([a-zA-Z])", ")*$1");

        // Normalisierung für NCalc
        result = result.Replace('×', '*').Replace('÷', '/').Replace(',', '.');

        return result;
    }

    /// <summary>
    /// Versucht einen Ausdruck mit NCalc zu berechnen.
    /// </summary>
    private static string? TryEvaluateNCalc(string expr)
    {
        try
        {
            var normalized = Regex.Replace(expr, @"\bsqrt\b", "Sqrt", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"\bpi\b", "Pi", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"(\d+\.?\d*)\s*\^\s*(\d+\.?\d*)", "Pow($1,$2)");

            var ncalc = new Expression(normalized);
            var result = ncalc.Evaluate();
            if (result is double d && !double.IsNaN(d) && !double.IsInfinity(d))
                return FormatNumber(d);
            if (result is int i)
                return FormatNumber((double)i);
            if (result is long l)
                return FormatNumber((double)l);
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