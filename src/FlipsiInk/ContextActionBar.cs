// FlipsiInk - AI-powered Handwriting & Math Notes App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;

namespace FlipsiInk;

/// <summary>
/// Kontext-sensitive Aktionsleiste (Issue #35).
/// Bietet Aktionen basierend auf dem erkannten Kontext (Text, Math, etc.).
/// </summary>
public class ContextActionBar
{
    private readonly List<ContextAction> _actions = new();

    public void AddAction(string context, string label, Action execute)
    {
        _actions.Add(new ContextAction(context, label, () => { execute(); return Task.CompletedTask; }));
    }

    public void AddAction(string context, string label, Func<Task> executeAsync)
    {
        _actions.Add(new ContextAction(context, label, executeAsync));
    }

    public IReadOnlyList<ContextAction> GetActionsForContext(string context)
    {
        return _actions.FindAll(a => a.Context == context).AsReadOnly();
    }
}

public class ContextAction
{
    public string Context { get; }
    public string Label { get; }
    public Func<Task> ExecuteAsync { get; }

    public ContextAction(string context, string label, Func<Task> executeAsync)
    {
        Context = context;
        Label = label;
        ExecuteAsync = executeAsync;
    }

    public void Execute() => ExecuteAsync();
}