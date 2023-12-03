using System;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Splitracker.UI.Shared;

public record FlagContext(
    bool Experimental = false,
    bool? DarkMode = null,
    bool StageMode = false
    );

public class FlagContextHolder
{
    FlagContext context = new();
    readonly ILogger<FlagContextHolder> log;

    public FlagContextHolder(ILogger<FlagContextHolder> log)
    {
        this.log = log;
        Source = new(context, false);
    }

    public CascadingValueSource<FlagContext> Source { get; }

    public FlagContext Context
    {
        get => context;
        set
        {
            if (context == value)
            {
                return;
            }

            log.Log(LogLevel.Debug, "Flag context changed (old={Old}, new={New})", context, value);
            context = value;
            Source.NotifyChangedAsync(value);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? Changed;
}