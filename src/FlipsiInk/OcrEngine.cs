// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FlipsiInk;

/// <summary>
/// OCR Engine using ONNX Runtime for handwriting recognition.
/// Supports Florence-2 vision-language model with proper encoder/decoder architecture.
/// GPU acceleration via CUDA/DirectML when available, CPU fallback otherwise.
/// </summary>
public class OcrEngine : IDisposable
{
    private InferenceSession? _encoderSession;
    private InferenceSession? _decoderSession;
    private InferenceSession? _visionSession;
    private string _modelPath = "";
    public string ModelName { get; private set; } = "none";
    public bool IsGpuEnabled { get; private set; }
    public string ExecutionProvider { get; private set; } = "CPU";

    // Florence-2 BPE tokenizer vocabulary (subset loaded from model dir)
    private Dictionary<string, int>? _tokenizerVocab;
    private Dictionary<int, string>? _decoderVocab;

    /// <summary>
    /// Detect available GPU and execution providers.
    /// Priority: CUDA > DirectML > CPU
    /// </summary>
    public static (bool hasGpu, string provider, string gpuName) DetectGpu()
    {
        // Try CUDA first (NVIDIA GPU)
        try
        {
            var cudaOptions = new SessionOptions();
            cudaOptions.AppendExecutionProvider_CUDA(0);
            // If we get here without exception, CUDA is available
            cudaOptions.Dispose();

            string gpuName = "NVIDIA GPU (CUDA)";
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Name FROM Win32_VideoController WHERE AdapterCompatibility LIKE '%NVIDIA%'");
                foreach (var obj in searcher.Get())
                {
                    gpuName = obj["Name"]?.ToString() ?? gpuName;
                    break;
                }
            }
            catch { /* Management API may not be available */ }

            return (true, "CUDA", gpuName);
        }
        catch { /* CUDA not available */ }

        // Try DirectML (AMD/Intel GPU, or NVIDIA fallback)
        try
        {
            var dmlOptions = new SessionOptions();
            dmlOptions.AppendExecutionProvider_DML(0);
            dmlOptions.Dispose();

            string gpuName = "GPU (DirectML)";
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Name FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(name) && !name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase))
                    {
                        gpuName = name;
                        break;
                    }
                }
            }
            catch { /* Management API may not be available */ }

            return (true, "DML", gpuName);
        }
        catch { /* DirectML not available */ }

        return (false, "CPU", "CPU Only");
    }

    /// <summary>
    /// Load the ONNX model from the Models directory.
    /// Supports Florence-2 directory-based models with encoder/decoder/vision_encoder.
    /// </summary>
    public void LoadModel()
    {
        // Priority: explicit config path > ModelManager default dir > app dir Models
        var modelDirs = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlipsiInk", "Models"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models")
        };

        // Check config path first
        if (!string.IsNullOrEmpty(App.Config.ModelPath))
        {
            if (Directory.Exists(App.Config.ModelPath))
            {
                // Directory-based model (Florence-2)
                try
                {
                    LoadFlorence2Model(App.Config.ModelPath);
                    return;
                }
                catch (Exception ex)
                {
                    ModelName = $"Fehler: {ex.Message}";
                    return;
                }
            }
            else if (File.Exists(App.Config.ModelPath))
            {
                try
                {
                    LoadSingleFileModel(App.Config.ModelPath);
                    return;
                }
                catch (Exception ex)
                {
                    ModelName = $"Fehler: {ex.Message}";
                    return;
                }
            }
        }

        // Search model directories for Florence-2 or legacy models
        foreach (var modelDir in modelDirs)
        {
            try
            {
                if (!Directory.Exists(modelDir)) continue;

                // Look for Florence-2 subdirectories (model_id/encoder_model.onnx etc.)
                foreach (var subDir in Directory.GetDirectories(modelDir))
                {
                    if (File.Exists(Path.Combine(subDir, "encoder_model_int8.onnx")) ||
                        File.Exists(Path.Combine(subDir, "encoder_model.onnx")) ||
                        File.Exists(Path.Combine(subDir, "encoder_model_fp16.onnx")))
                    {
                        try
                        {
                            LoadFlorence2Model(subDir);
                            return;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[FlipsiInk] Failed to load Florence-2 from {subDir}: {ex.Message}");
                            continue;
                        }
                    }
                }

                // Legacy: single-file models
                foreach (var pattern in new[] { "model.onnx", "*.onnx" })
                {
                    var files = Directory.GetFiles(modelDir, pattern);
                    if (files.Length > 0)
                    {
                        try
                        {
                            LoadSingleFileModel(files[0]);
                            return;
                        }
                        catch { continue; }
                    }
                }
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (Exception) { continue; }
        }

        ModelName = "kein Modell gefunden";
    }

    /// <summary>
    /// Load a Florence-2 model (directory with encoder/decoder/vision_encoder).
    /// Automatically selects INT8 > FP16 > FP32 based on what's available.
    /// </summary>
    private void LoadFlorence2Model(string modelDir)
    {
        _modelPath = modelDir;

        // Detect best available quantization
        string quantSuffix = "_int8";
        if (!File.Exists(Path.Combine(modelDir, $"encoder_model{quantSuffix}.onnx")))
        {
            quantSuffix = "_fp16";
            if (!File.Exists(Path.Combine(modelDir, $"encoder_model{quantSuffix}.onnx")))
            {
                quantSuffix = ""; // FP32 fallback
            }
        }

        // Detect GPU
        var (hasGpu, provider, gpuName) = DetectGpu();
        IsGpuEnabled = hasGpu;
        ExecutionProvider = provider;

        // Create session options with GPU if available
        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        if (hasGpu)
        {
            try
            {
                if (provider == "CUDA")
                    sessionOptions.AppendExecutionProvider_CUDA(0);
                else if (provider == "DML")
                    sessionOptions.AppendExecutionProvider_DML(0);
            }
            catch
            {
                // GPU init failed, fallback to CPU
                IsGpuEnabled = false;
                ExecutionProvider = "CPU";
                sessionOptions.Dispose();
                sessionOptions = new SessionOptions();
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            }
        }

        // Always append CPU as fallback provider
        sessionOptions.AppendExecutionProvider_CPU(hasGpu ? 1 : 0);

        // Load the three Florence-2 components
        var encoderFile = Path.Combine(modelDir, $"encoder_model{quantSuffix}.onnx");
        var decoderFile = Path.Combine(modelDir, $"decoder_model_merged{quantSuffix}.onnx");
        var visionFile = Path.Combine(modelDir, $"vision_encoder{quantSuffix}.onnx");

        // Check for embed_tokens (large model variant)
        var embedTokensFile = Path.Combine(modelDir, $"embed_tokens{quantSuffix}.onnx");

        if (!File.Exists(encoderFile))
            throw new FileNotFoundException($"Encoder model not found: {encoderFile}");
        if (!File.Exists(decoderFile))
            throw new FileNotFoundException($"Decoder model not found: {decoderFile}");

        _encoderSession = new InferenceSession(encoderFile, sessionOptions);

        // Decoder may need embed_tokens as external data
        var decoderOpts = new SessionOptions();
        decoderOpts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        if (hasGpu)
        {
            try
            {
                if (provider == "CUDA") decoderOpts.AppendExecutionProvider_CUDA(0);
                else if (provider == "DML") decoderOpts.AppendExecutionProvider_DML(0);
            }
            catch { /* fallback */ }
        }
        decoderOpts.AppendExecutionProvider_CPU(hasGpu ? 1 : 0);

        _decoderSession = new InferenceSession(decoderFile, decoderOpts);

        if (File.Exists(visionFile))
        {
            var visionOpts = new SessionOptions();
            visionOpts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            if (hasGpu)
            {
                try
                {
                    if (provider == "CUDA") visionOpts.AppendExecutionProvider_CUDA(0);
                    else if (provider == "DML") visionOpts.AppendExecutionProvider_DML(0);
                }
                catch { /* fallback */ }
            }
            visionOpts.AppendExecutionProvider_CPU(hasGpu ? 1 : 0);
            _visionSession = new InferenceSession(visionFile, visionOpts);
        }

        // Load tokenizer vocabulary
        LoadTokenizer(modelDir);

        var quantLabel = quantSuffix switch
        {
            "_int8" => "INT8",
            "_fp16" => "FP16",
            "" => "FP32",
            _ => quantSuffix.TrimStart('_').ToUpper()
        };
        ModelName = $"Florence-2 ({quantLabel}, {ExecutionProvider}{(IsGpuEnabled ? " ⚡" : "")})";
    }

    /// <summary>
    /// Load a legacy single-file ONNX model (TrOCR etc.)
    /// </summary>
    private void LoadSingleFileModel(string path)
    {
        _modelPath = path;

        var (hasGpu, provider, _) = DetectGpu();
        IsGpuEnabled = hasGpu;
        ExecutionProvider = provider;

        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        if (hasGpu)
        {
            try
            {
                if (provider == "CUDA") sessionOptions.AppendExecutionProvider_CUDA(0);
                else if (provider == "DML") sessionOptions.AppendExecutionProvider_DML(0);
            }
            catch
            {
                IsGpuEnabled = false;
                ExecutionProvider = "CPU";
            }
        }
        sessionOptions.AppendExecutionProvider_CPU(hasGpu ? 1 : 0);

        _encoderSession = new InferenceSession(path, sessionOptions);
        ModelName = $"{Path.GetFileName(path)} ({ExecutionProvider})";
    }

    /// <summary>
    /// Load BPE tokenizer vocabulary from the model directory.
    /// Tries: vocab.json (HuggingFace format), tokenizer.json, or falls back to ASCII.
    /// </summary>
    private void LoadTokenizer(string modelDir)
    {
        _tokenizerVocab = new Dictionary<string, int>();
        _decoderVocab = new Dictionary<int, string>();

        // Try loading vocab.json (HuggingFace tokenizer format)
        var vocabPath = Path.Combine(modelDir, "vocab.json");
        if (File.Exists(vocabPath))
        {
            try
            {
                var json = File.ReadAllText(vocabPath);
                var vocab = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                if (vocab != null)
                {
                    _tokenizerVocab = vocab;
                    foreach (var (token, id) in vocab)
                        _decoderVocab[id] = token;
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FlipsiInk] Failed to load vocab.json: {ex.Message}");
            }
        }

        // Try tokenizer.json
        var tokenizerPath = Path.Combine(modelDir, "tokenizer.json");
        if (File.Exists(tokenizerPath))
        {
            try
            {
                var json = File.ReadAllText(tokenizerPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var model = doc.RootElement.GetProperty("model");
                if (model.TryGetProperty("vocab", out var vocabEl))
                {
                    foreach (var prop in vocabEl.EnumerateObject())
                    {
                        if (int.TryParse(prop.Value.GetString(), out var id) ||
                            prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number && prop.Value.TryGetInt32(out id))
                        {
                            _tokenizerVocab[prop.Name] = id;
                            _decoderVocab[id] = prop.Name;
                        }
                    }
                    if (_tokenizerVocab.Count > 0) return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FlipsiInk] Failed to load tokenizer.json: {ex.Message}");
            }
        }

        // Fallback: Build minimal ASCII vocabulary for Florence-2
        // Florence-2 uses BPE but the essential tokens for text output are ASCII + special tokens
        BuildFallbackVocab();
    }

    /// <summary>
    /// Build a minimal fallback vocabulary for decoding model output.
    /// Florence-2 uses BPE; this provides basic ASCII coverage + common subword tokens.
    /// </summary>
    private void BuildFallbackVocab()
    {
        _decoderVocab = new Dictionary<int, string>();

        // Special tokens (Florence-2 specific)
        _decoderVocab[0] = "<s>";         // BOS
        _decoderVocab[1] = "</s>";        // EOS
        _decoderVocab[2] = "<unk>";       // UNK
        _decoderVocab[3] = "<pad>";        // PAD

        // ASCII printable characters (covers basic text output)
        for (int i = 32; i < 127; i++)
        {
            var c = (char)i;
            _decoderVocab[i + 1] = c.ToString(); // Offset by 1 after special tokens
        }

        // Common BPE subword tokens for German/English text
        var commonTokens = new[]
        {
            "er", "en", "de", "es", "ei", "ie", "ch", "ck", "ng", "sch",
            "the", "and", "ing", "tion", "ent", "ung", "keit", "lich",
            "à", "ä", "ö", "ü", "ß", "é", "è", "â", "î", "ô", "û",
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
            "+", "-", "×", "÷", "=", "(", ")", "[", "]", "{", "}",
            "√", "∫", "∑", "π", "θ", "α", "β", "γ", "δ", "∞",
            "²", "³", "°", "‰", "€", "§"
        };

        int idx = 128 + 1; // After ASCII range
        foreach (var token in commonTokens)
        {
            _decoderVocab[idx++] = token;
        }

        _tokenizerVocab = _decoderVocab.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
    }

    /// <summary>
    /// Recognize text from a bitmap image.
    /// For Florence-2: encodes image → generates text via decoder.
    /// </summary>
    public string Recognize(DBitmap bitmap)
    {
        if (_visionSession != null && _encoderSession != null && _decoderSession != null)
            return RecognizeFlorence2(bitmap);
        else if (_encoderSession != null)
            return RecognizeSingleModel(bitmap);

        throw new InvalidOperationException("Modell nicht geladen");
    }

    /// <summary>
    /// Florence-2 recognition pipeline: vision_encoder → encoder → autoregressive decoder.
    /// </summary>
    private string RecognizeFlorence2(DBitmap bitmap)
    {
        // Step 1: Preprocess image for vision encoder (Florence-2 uses 768x768 or 448x448)
        const int visionSize = 768; // Florence-2 default
        using var resized = new DBitmap(visionSize, visionSize);
        using (var g = DGraphics.FromImage(resized))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            g.DrawImage(bitmap, 0, 0, visionSize, visionSize);
        }

        // Step 2: Run vision encoder
        var pixelValues = PreprocessImageForVision(resized, visionSize);
        var visionInputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("pixel_values", pixelValues)
        };

        using var visionOutput = _visionSession!.Run(visionInputs);
        var imageFeatures = visionOutput.First().AsTensor<float>();

        // Step 3: Run encoder with image features
        // Florence-2 encoder needs: image_embeds + decoder_input_ids
        var encoderInputs = new List<NamedOnnxValue>();
        // Convert vision output to encoder input format
        var imageEmbedsTensor = new DenseTensor<float>(imageFeatures.ToArray(), imageFeatures.Dimensions.ToArray());
        encoderInputs.Add(NamedOnnxValue.CreateFromTensor("encoder_hidden_states", imageEmbedsTensor));

        // Step 4: Autoregressive decoding
        // Start with BOS token, generate until EOS or max length
        var generatedTokens = new List<int> { 0 }; // <s> BOS token
        const int maxLen = 512;

        for (int step = 0; step < maxLen; step++)
        {
            var decoderInputs = new List<NamedOnnxValue>();
            var inputIds = new DenseTensor<long>(new long[] { 0 }.Concat(generatedTokens.Select(t => (long)t)).ToArray(), new[] { 1, generatedTokens.Count + 1 });
            decoderInputs.Add(NamedOnnxValue.CreateFromTensor("input_ids", inputIds));
            decoderInputs.Add(NamedOnnxValue.CreateFromTensor("encoder_hidden_states", imageEmbedsTensor));

            using var decoderOutput = _decoderSession!.Run(decoderInputs);
            var logits = decoderOutput.First().AsTensor<float>();

            // Get the last token's logits
            int vocabSize = logits.Dimensions[^1];
            int lastIdx = generatedTokens.Count; // Index of the last generated position
            float maxLogit = float.NegativeInfinity;
            int nextToken = 1; // Default: </s>

            for (int v = 0; v < vocabSize; v++)
            {
                var logit = logits[lastIdx * vocabSize + v];
                if (logit > maxLogit)
                {
                    maxLogit = logit;
                    nextToken = v;
                }
            }

            // Stop on EOS token
            if (nextToken == 1 || nextToken == 2) // </s> or <unk> at end
                break;

            generatedTokens.Add(nextToken);
        }

        // Step 5: Decode tokens to text
        return DecodeTokens(generatedTokens);
    }

    /// <summary>
    /// Legacy single-model recognition (TrOCR, etc.)
    /// </summary>
    private string RecognizeSingleModel(DBitmap bitmap)
    {
        var inputTensor = PreprocessImage(bitmap);

        var inputs = new List<NamedOnnxValue>();
        var inputName = _encoderSession!.InputNames.First();
        inputs.Add(NamedOnnxValue.CreateFromTensor(inputName, inputTensor));

        using var results = _encoderSession.Run(inputs);
        var output = results.First().AsTensor<float>();

        return DecodeOutputLegacy(output);
    }

    /// <summary>
    /// Preprocess bitmap for vision encoder: resize + normalize with ImageNet stats.
    /// Florence-2 uses 768x768 with ImageNet normalization.
    /// </summary>
    private DenseTensor<float> PreprocessImageForVision(DBitmap bitmap, int targetSize)
    {
        var tensor = new DenseTensor<float>(new[] { 1, 3, targetSize, targetSize });

        for (int y = 0; y < targetSize; y++)
        {
            for (int x = 0; x < targetSize; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                // ImageNet normalization (same as Florence-2 preprocessing)
                tensor[0, 0, y, x] = (pixel.R / 255f - 0.485f) / 0.229f;
                tensor[0, 1, y, x] = (pixel.G / 255f - 0.456f) / 0.224f;
                tensor[0, 2, y, x] = (pixel.B / 255f - 0.406f) / 0.225f;
            }
        }

        return tensor;
    }

    /// <summary>
    /// Preprocess bitmap: resize to 384x384, normalize with ImageNet stats (legacy).
    /// </summary>
    private DenseTensor<float> PreprocessImage(DBitmap bitmap)
    {
        const int targetSize = 384;

        using var resized = new DBitmap(targetSize, targetSize);
        using (var g = DGraphics.FromImage(resized))
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
    /// Decode token IDs to text using the loaded vocabulary.
    /// Handles BPE subword tokens (▁ prefix = word boundary).
    /// </summary>
    private string DecodeTokens(List<int> tokens)
    {
        if (_decoderVocab == null || _decoderVocab.Count == 0)
            return $"[Erkannt: {tokens.Count} Tokens – Tokenizer fehlt]";

        var parts = new List<string>();
        foreach (var tokenId in tokens.Skip(1)) // Skip BOS
        {
            if (tokenId == 1) break; // EOS

            if (_decoderVocab.TryGetValue(tokenId, out var token))
            {
                // Handle BPE subword tokens
                // ▁ (U+2581) = word boundary in SentencePiece/BPE
                if (token.StartsWith("▁"))
                {
                    parts.Add(" " + token[1..]);
                }
                else if (token.StartsWith("##"))
                {
                    // WordPiece style subword
                    parts.Add(token[2..]);
                }
                else if (token == "<s>" || token == "</s>" || token == "<pad>" || token == "<unk>")
                {
                    // Skip special tokens
                    continue;
                }
                else
                {
                    parts.Add(token);
                }
            }
        }

        var text = string.Join("", parts).Trim();
        return string.IsNullOrEmpty(text) ? "[Kein Text erkannt]" : text;
    }

    /// <summary>
    /// Legacy decode: greedy decoding for single-model output tensors.
    /// </summary>
    private string DecodeOutputLegacy(Tensor<float> output)
    {
        var dims = output.Dimensions;
        if (dims.Length < 2) return "";

        int seqLen = dims[1];
        int vocabSize = dims.Length > 2 ? dims[2] : dims[1];
        var tokens = new List<int>();

        for (int s = 0; s < seqLen; s++)
        {
            float maxVal = float.NegativeInfinity;
            int maxIdx = 0;
            for (int v = 0; v < vocabSize; v++)
            {
                var val = dims.Length > 2 ? output[0, s, v] : output[s, v];
                if (val > maxVal)
                {
                    maxVal = val;
                    maxIdx = v;
                }
            }
            if (maxIdx == 1 || maxIdx == 2) break; // EOS/UNK
            tokens.Add(maxIdx);
        }

        return DecodeTokens(tokens);
    }

    public void Dispose()
    {
        _encoderSession?.Dispose();
        _decoderSession?.Dispose();
        _visionSession?.Dispose();
    }
}