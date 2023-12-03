using Microsoft.AspNetCore.Components;

namespace Splitracker.UI.Shared.Sections;

using System;
using System.Threading.Tasks;

public class SectionContent : IComponent, IDisposable
{
    SectionRegistry registry = null!;

    [Parameter]
    [EditorRequired]
    public required string Name { get; set; }

    [Parameter]
    [EditorRequired]
    public required RenderFragment ChildContent { get; set; }

    public void Attach(RenderHandle renderHandle)
    {
        registry = SectionRegistry.GetRegistry(renderHandle);
    }

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        registry.SetContent(Name, ChildContent);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!string.IsNullOrEmpty(Name))
        {
            // This relies on the assumption that the old SectionContent gets disposed before the
            // new one is added to the output. This won't be the case in all possible scenarios.
            // We should have the registry keep track of which SectionContent is the most recent
            // one to supply new content, and disregard updates from ones that were superseded.
            registry.SetContent(Name, null);
        }
    }
}