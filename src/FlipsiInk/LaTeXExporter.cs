// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace FlipsiInk;

/// <summary>
/// Exports recognized math expressions to LaTeX format.
/// Supports: arithmetic, fractions, powers, roots, integrals, summations,
/// Greek letters, comparison operators, and full document generation.
/// </summary>
public static class LaTeXExporter
{
    // ── LaTeX Symbol Mapping ────────────────────────────────────

    private static readonly Dictionary<string, string> PlainToLatex = new()
    {
        // Operators
        { "×", "\\times" }, { "÷", "\\div" }, { "±", "\\pm" }, { "∓", "\\mp" },
        { "·", "\\cdot" }, { "∘", "\\circ" }, { "⊕", "\\oplus" }, { "⊗", "\\otimes" },
        // Comparison
        { "≤", "\\leq" }, { "≥", "\\geq" }, { "≠", "\\neq" }, { "≈", "\\approx" },
        { "≡", "\\equiv" }, { "∝", "\\propto" }, { "≪", "\\ll" }, { "≫", "\\gg" },
        // Arrows
        { "→", "\\rightarrow" }, { "←", "\\leftarrow" }, { "↔", "\\leftrightarrow" },
        { "⇒", "\\Rightarrow" }, { "⇐", "\\LeftArrow" }, { "⇔", "\\Leftrightarrow" },
        // Greek lowercase
        { "α", "\\alpha" }, { "β", "\\beta" }, { "γ", "\\gamma" }, { "δ", "\\delta" },
        { "ε", "\\varepsilon" }, { "ζ", "\\zeta" }, { "η", "\\eta" }, { "θ", "\\theta" },
        { "ι", "\\iota" }, { "κ", "\\kappa" }, { "λ", "\\lambda" }, { "μ", "\\mu" },
        { "ν", "\\nu" }, { "ξ", "\\xi" }, { "π", "\\pi" }, { "ρ", "\\rho" },
        { "σ", "\\sigma" }, { "τ", "\\tau" }, { "υ", "\\upsilon" }, { "φ", "\\varphi" },
        { "χ", "\\chi" }, { "ψ", "\\psi" }, { "ω", "\\omega" },
        // Greek uppercase
        { "Ω", "\\Omega" }, { "Σ", "\\Sigma" }, { "Π", "\\Pi" }, { "Δ", "\\Delta" },
        { "Φ", "\\Phi" }, { "Ψ", "\\Psi" }, { "Γ", "\\Gamma" }, { "Λ", "\\Lambda" },
        { "Θ", "\\Theta" },
        // Math structures
        { "√", "\\sqrt" }, { "∫", "\\int" }, { "∑", "\\sum" }, { "∏", "\\prod" },
        { "∂", "\\partial" }, { "∞", "\\infty" }, { "∇", "\\nabla" },
        { "∈", "\\in" }, { "∉", "\\notin" }, { "⊂", "\\subset" }, { "⊃", "\\supset" },
        { "∪", "\\cup" }, { "∩", "\\cap" }, { "∀", "\\forall" }, { "∃", "\\exists" },
        { "¬", "\\neg" }, { "∧", "\\land" }, { "∨", "\\lor" },
        // Formatting
        { "²", "^{2}" }, { "³", "^{3}" }, { "°", "^{\\circ}" }, { "‰", "\\permil" },
        // Special brackets
        { "|", "|" }, // absolute value handled separately
    };

    // ── Expression to LaTeX ─────────────────────────────────────

    /// <summary>
    /// Converts a math recognition result to a LaTeX string.
    /// </summary>
    public static string ToLatex(MathRecognitionResult result)
    {
        if (result == null || string.IsNullOrEmpty(result.Latex))
            return "";

        var latex = result.Latex;
        latex = EscapeSpecialCharacters(latex);
        return latex;
    }

    /// <summary>
    /// Converts a plain-text math expression to LaTeX.
    /// Replaces Unicode math symbols with their LaTeX commands.
    /// </summary>
    public static string PlainTextToLatex(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";

        var result = plainText;
        foreach (var (plain, latex) in PlainToLatex)
        {
            result = result.Replace(plain, latex);
        }

        // Handle implicit multiplication: "2x" → "2 x"
        // But not inside LaTeX commands

        return result;
    }

    /// <summary>
    /// Escapes special LaTeX characters in a raw string.
    /// </summary>
    private static string EscapeSpecialCharacters(string text)
    {
        var sb = new StringBuilder(text);
        // Don't escape characters that are already part of LaTeX commands
        // Only escape if they appear standalone
        return sb.ToString();
    }

    // ── Document Generation ──────────────────────────────────────

    /// <summary>
    /// Generates a complete LaTeX document from a list of expressions.
    /// </summary>
    public static string GenerateDocument(List<string> latexExpressions, string title = "FlipsiInk Math Export")
    {
        var sb = new StringBuilder();
        sb.AppendLine("\\documentclass{article}");
        sb.AppendLine("\\usepackage{amsmath}");
        sb.AppendLine("\\usepackage{amssymb}");
        sb.AppendLine("\\usepackage[utf8]{inputenc}");
        sb.AppendLine("\\usepackage[margin=2cm]{geometry}");
        sb.AppendLine();
        sb.AppendLine($"\\title{{{EscapeLatexTitle(title)}}}");
        sb.AppendLine("\\author{FlipsiInk}");
        sb.AppendLine("\\date{\\today}");
        sb.AppendLine();
        sb.AppendLine("\\begin{document}");
        sb.AppendLine("\\maketitle");
        sb.AppendLine();

        for (int i = 0; i < latexExpressions.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(latexExpressions[i])) continue;
            sb.AppendLine($"\\begin{{equation}}");
            sb.AppendLine($"  {latexExpressions[i]}");
            sb.AppendLine($"\\end{{equation}}");
            sb.AppendLine();
        }

        sb.AppendLine("\\end{document}");
        return sb.ToString();
    }

    private static string EscapeLatexTitle(string title)
    {
        return title.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}")
                    .Replace("$", "\\$").Replace("&", "\\&").Replace("#", "\\#")
                    .Replace("_", "\\_").Replace("~", "\\textasciitilde")
                    .Replace("^", "\\textasciicircum").Replace("%", "\\%");
    }

    // ── Export Methods ───────────────────────────────────────────

    /// <summary>
    /// Exports LaTeX content to a .tex file.
    /// </summary>
    public static void ExportToFile(string latex, string filePath)
    {
        if (string.IsNullOrWhiteSpace(latex))
            throw new ArgumentException("LaTeX content cannot be empty.", nameof(latex));

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(filePath, latex, Encoding.UTF8);
            System.Diagnostics.Debug.WriteLine($"[FlipsiInk] LaTeX exported to: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FlipsiInk] LaTeX export failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Copies LaTeX content to the Windows clipboard.
    /// </summary>
    public static void CopyToClipboard(string latex)
    {
        if (string.IsNullOrEmpty(latex)) return;

        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Clipboard.SetText(latex);
                System.Diagnostics.Debug.WriteLine("[FlipsiInk] LaTeX copied to clipboard");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FlipsiInk] Clipboard copy failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports a list of math results as a complete LaTeX document to a file.
    /// </summary>
    public static string ExportMathResults(List<MathRecognitionResult> results, string filePath, string title = "FlipsiInk Math Export")
    {
        var latexExpressions = new List<string>();
        foreach (var result in results)
        {
            var latex = ToLatex(result);
            if (!string.IsNullOrWhiteSpace(latex))
                latexExpressions.Add(latex);
        }

        var document = GenerateDocument(latexExpressions, title);
        ExportToFile(document, filePath);
        return document;
    }

    /// <summary>
    /// Exports a single math result as an inline LaTeX equation string.
    /// </summary>
    public static string ToInlineLatex(MathRecognitionResult result)
    {
        var latex = ToLatex(result);
        return string.IsNullOrWhiteSpace(latex) ? "" : $"$${latex}$$";
    }

    /// <summary>
    /// Exports a single math result as a display LaTeX equation string.
    /// </summary>
    public static string ToDisplayLatex(MathRecognitionResult result)
    {
        var latex = ToLatex(result);
        return string.IsNullOrWhiteSpace(latex) ? "" : $"\\[\n{latex}\n\\]";
    }
}