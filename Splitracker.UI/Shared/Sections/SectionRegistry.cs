namespace Splitracker.UI.Shared.Sections;

using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

internal class SectionRegistry
{
    static readonly ConditionalWeakTable<Dispatcher, SectionRegistry> Registries = new();

    readonly Dictionary<string, List<Action<RenderFragment?>>> subscriptions = new();

    public static SectionRegistry GetRegistry(RenderHandle renderHandle)
    {
        return Registries.GetOrCreateValue(renderHandle.Dispatcher);
    }

    public void Subscribe(string name, Action<RenderFragment?> callback)
    {
        if (!subscriptions.TryGetValue(name, out var existingList))
        {
            existingList = new();
            subscriptions.Add(name, existingList);
        }

        existingList.Add(callback);
    }

    public void Unsubscribe(string? name, Action<RenderFragment?> callback)
    {
        if (name != null && subscriptions.TryGetValue(name, out var existingList))
        {
            existingList.Remove(callback);
        }
    }

    public void SetContent(string name, RenderFragment? content)
    {
        if (subscriptions.TryGetValue(name, out var existingList))
        {
            foreach (var callback in existingList)
            {
                callback(content);
            }
        }
    }
}