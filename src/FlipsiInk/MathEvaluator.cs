// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FlipsiInk;

/// <summary>
/// Evaluates math expressions found in recognized text.
/// Supports: basic arithmetic, parentheses, exponents, variables.
/// </summary>
public static class MathEvaluator
{
    public static string Evaluate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var results = new System.Text.StringBuilder();

        // Simple arithmetic: 3 + 5, 12 * 4
        var simplePattern = @"(\d+[\.\,]?\d*)\s*([+\-*/×÷^])\s*(\d+[\.\,]?\d*)";
        var matches = Regex.Matches(text, simplePattern);

        foreach (Match m in matches)
        {
            try
            {
                var left = double.Parse(m.Groups[1].Value.Replace(',', '.'));
                var op = m.Groups[2].Value;
                var right = double.Parse(m.Groups[3].Value.Replace(',', '.'));

                var result = op switch
                {
                    "+" => left + right,
                    "-" => left - right,
                    "*" or "×" => left * right,
                    "/" or "÷" => right != 0 ? left / right : double.NaN,
                    "^" => Math.Pow(left, right),
                    _ => double.NaN
                };

                if (!double.IsNaN(result))
                {
                    var displayOp = op switch { "×" => "*", "÷" => "/", _ => op };
                    results.AppendLine($"{left} {displayOp} {right} = {FormatNumber(result)}");
                }
            }
            catch { }
        }

        // Equations: 3x + 5 = 20 → x = 5
        var equationPattern = @"(\d*)\s*([a-zA-Z])\s*([+\-])\s*(\d+[\.\,]?\d*)\s*=\s*(\d+[\.\,]?\d*)";
        var eqMatches = Regex.Matches(text, equationPattern);

        foreach (Match m in eqMatches)
        {
            try
            {
                var coefStr = m.Groups[1].Value;
                var variable = m.Groups[2].Value;
                var op = m.Groups[3].Value;
                var constant = double.Parse(m.Groups[4].Value.Replace(',', '.'));
                var equals = double.Parse(m.Groups[5].Value.Replace(',', '.'));

                var coef = string.IsNullOrEmpty(coefStr) ? 1 : double.Parse(coefStr);

                double x;
                if (op == "+")
                    x = (equals - constant) / coef;
                else
                    x = (equals + constant) / coef;

                results.AppendLine($"{variable} = {FormatNumber(x)}");
            }
            catch { }
        }

        // NCalc fallback for complex expressions
        try
        {
            var expr = NCalc.Async.AsyncExpression.Evaluate(text).Result;
            if (expr is double d && !double.IsNaN(d))
            {
                results.AppendLine($"= {FormatNumber(d)}");
            }
        }
        catch { }

        return results.ToString().Trim();
    }

    private static string FormatNumber(double n)
    {
        if (n == Math.Floor(n) && Math.Abs(n) < 1e15)
            return ((long)n).ToString();
        return n.ToString("G6");
    }
}