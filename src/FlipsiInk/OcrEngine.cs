// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FlipsiInk;

/// <summary>
/// OCR Engine using ONNX Runtime for handwriting recognition.
/// Supports TrOCR and compatible vision models.
/// </summary>
public class OcrEngine : IDisposable
{
    private InferenceSession? _session;
    private string _modelPath = "";
    public string ModelName { get; private set; } = "none";

    /// <summary>
    /// Load the ONNX model from the Models directory.
    /// Tries: model.onnx, trocr_large.onnx, or any .onnx file found.
    /// </summary>
    public void LoadModel()
    {
        // Priority: explicit config path > ModelManager default dir > app dir Models
        var modelDirs = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlipsiInk", "Models"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models")
        };

        // Also check config path
        if (!string.IsNullOrEmpty(App.Config.ModelPath) && File.Exists(App.Config.ModelPath))
        {
            LoadModelFile(App.Config.ModelPath);
            return;
        }

        // Search all model directories
        string? foundModel = null;
        foreach (var modelDir in modelDirs)
        {
            Directory.CreateDirectory(modelDir);
            if (File.Exists(Path.Combine(modelDir, "model.onnx")))
                foundModel = Path.Combine(modelDir, "model.onnx");
            else if (File.Exists(Path.Combine(modelDir, "qwen2.5-vl-3b-q4.onnx")))
                foundModel = Path.Combine(modelDir, "qwen2.5-vl-3b-q4.onnx");
            else if (File.Exists(Path.Combine(modelDir, "trocr-large.onnx")))
                foundModel = Path.Combine(modelDir, "trocr-large.onnx");
            else
            {
                var onnxFiles = Directory.GetFiles(modelDir, "*.onnx");
                if (onnxFiles.Length > 0)
                    foundModel = onnxFiles[0];
            }
            if (foundModel != null) break;
        }

        if (foundModel == null)
        {
            ModelName = "kein Modell gefunden";
            throw new FileNotFoundException(
                "Kein ONNX-Modell gefunden. Bitte ein Modell über 📦 KI-Modelle herunterladen.");
        }

        LoadModelFile(foundModel);
    }

    private void LoadModelFile(string path)
    {
        _modelPath = path;
        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        sessionOptions.AppendExecutionProvider_CPU(0);

        _session = new InferenceSession(path, sessionOptions);
        ModelName = Path.GetFileName(path);
    }

    /// <summary>
    /// Recognize text from a bitmap image.
    /// </summary>
    public string Recognize(Bitmap bitmap)
    {
        if (_session == null)
            throw new InvalidOperationException("Modell nicht geladen");

        var inputTensor = PreprocessImage(bitmap);

        var inputs = new List<NamedOnnxValue>();
        var inputName = _session.InputNames.First();
        inputs.Add(NamedOnnxValue.CreateFromTensor(inputName, inputTensor));

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        return DecodeOutput(output);
    }

    /// <summary>
    /// Preprocess bitmap: resize to 384x384, normalize with ImageNet stats.
    /// </summary>
    private DenseTensor<float> PreprocessImage(Bitmap bitmap)
    {
        const int targetSize = 384;

        using var resized = new Bitmap(targetSize, targetSize);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            g.DrawImage(bitmap, 0, 0, targetSize, targetSize);
        }

        var tensor = new DenseTensor<float>(new[] { 1, 3, targetSize, targetSize });

        for (int y = 0; y < targetSize; y++)
        {
            for (int x = 0; x < targetSize; x++)
            {
                var pixel = resized.GetPixel(x, y);
                tensor[0, 0, y, x] = (pixel.R / 255f - 0.485f) / 0.229f;
                tensor[0, 1, y, x] = (pixel.G / 255f - 0.456f) / 0.224f;
                tensor[0, 2, y, x] = (pixel.B / 255f - 0.406f) / 0.225f;
            }
        }

        return tensor;
    }

    /// <summary>
    /// Decode model output: greedy decoding (argmax per timestep).
    /// Full implementation needs BPE tokenizer – placeholder for now.
    /// </summary>
    private string DecodeOutput(Tensor<float> output)
    {
        var dims = output.Dimensions;
        if (dims.Length < 2) return "";

        int seqLen = dims[1];
        int vocabSize = dims.Length > 2 ? dims[2] : dims[1];

        return $"[Modell-Ausgabe: {seqLen} Schritte, {vocabSize} Vokabular – Tokenizer noch nötig]";
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}