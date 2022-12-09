using System;

namespace Splitracker.Web.Shared;

public record FlagContext(
    bool Experimental = false,
    bool? DarkMode = null,
    bool StageMode = false
    );

public class FlagContextHolder
{
    FlagContext context = new();

    public FlagContext Context
    {
        get => context;
        set
        {
            if (context == value)
            {
                return;
            }

            context = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? Changed;
}