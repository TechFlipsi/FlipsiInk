// InkNote - AI-powered Handwriting & Math Notes App
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
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace InkNote;

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
        var modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
        Directory.CreateDirectory(modelDir);

        // Look for model files
        string? foundModel = null;
        if (File.Exists(Path.Combine(modelDir, "model.onnx")))
            foundModel = Path.Combine(modelDir, "model.onnx");
        else if (File.Exists(Path.Combine(modelDir, "trocr_large.onnx")))
            foundModel = Path.Combine(modelDir, "trocr_large.onnx");
        else
        {
            var onnxFiles = Directory.GetFiles(modelDir, "*.onnx");
            if (onnxFiles.Length > 0)
                foundModel = onnxFiles[0];
        }

        if (foundModel == null)
        {
            // No model found - create placeholder
            ModelName = "kein Modell gefunden";
            throw new FileNotFoundException(
                "Kein ONNX-Modell im Models-Ordner gefunden. " +
                "Bitte ein Modell (z.B. TrOCR large als .onnx) in den Ordner legen: " + modelDir);
        }

        _modelPath = foundModel;
        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        // Use CPU only (no GPU required)
        sessionOptions.AppendExecutionProvider_CPU(0);

        _session = new InferenceSession(foundModel, sessionOptions);
        ModelName = Path.GetFileName(foundModel);
    }

    /// <summary>
    /// Recognize text from a bitmap image.
    /// Preprocesses image and runs inference.
    /// </summary>
    public string Recognize(Bitmap bitmap)
    {
        if (_session == null)
            throw new InvalidOperationException("Modell nicht geladen");

        // Preprocess: convert to model input format
        var inputTensor = PreprocessImage(bitmap);

        // Run inference
        var inputs = new List<NamedOnnxValue>();
        var inputName = _session.InputNames.First();
        inputs.Add(NamedOnnxValue.CreateFromTensor(inputName, inputTensor));

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        // Decode output to text
        return DecodeOutput(output);
    }

    /// <summary>
    /// Preprocess bitmap for the model.
    /// Resizes to 384x384 (TrOCR standard), normalizes pixel values.
    /// </summary>
    private DenseTensor<float> PreprocessImage(Bitmap bitmap)
    {
        const int targetSize = 384;

        // Resize to target size
        using var resized = new Bitmap(targetSize, targetSize);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            g.DrawImage(bitmap, 0, 0, targetSize, targetSize);
        }

        // Convert to RGB tensor [1, 3, 384, 384] normalized to [0, 1]
        var tensor = new DenseTensor<float>(new[] { 1, 3, targetSize, targetSize });

        for (int y = 0; y < targetSize; y++)
        {
            for (int x = 0; x < targetSize; x++)
            {
                var pixel = resized.GetPixel(x, y);
                // Normalize: ImageNet mean/std
                tensor[0, 0, y, x] = (pixel.R / 255f - 0.485f) / 0.229f; // R
                tensor[0, 1, y, x] = (pixel.G / 255f - 0.456f) / 0.224f; // G
                tensor[0, 2, y, x] = (pixel.B / 255f - 0.406f) / 0.225f; // B
            }
        }

        return tensor;
    }

    /// <summary>
    /// Decode model output tensor to text string.
    /// Uses greedy decoding (argmax per timestep).
    /// </summary>
    private string DecodeOutput(Tensor<float> output)
    {
        // TrOCR output: [1, seq_len, vocab_size]
        var dims = output.Dimensions;
        if (dims.Length < 2) return "";

        int seqLen = dims[1];
        int vocabSize = dims.Length > 2 ? dims[2] : dims[1];

        // For now, return raw output info
        // Full implementation needs tokenizer (BPE) for proper decoding
        // This will be completed when model is integrated
        return $"[Modell-Ausgabe: {seqLen} Schritte, {vocabSize} Vokabular – Tokenizer noch nötig]";
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}