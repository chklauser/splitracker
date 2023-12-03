namespace Splitracker.UI.Shared.Sections;

using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;

public class SectionOutlet : IComponent, IDisposable
{
    static readonly RenderFragment EmptyRenderFragment = _ => { };
    string subscribedName = null!;
    SectionRegistry registry = null!;
    Action<RenderFragment?> onChangeCallback = null!;

    public void Attach(RenderHandle renderHandle)
    {
        onChangeCallback = content => renderHandle.Render(content ?? EmptyRenderFragment);
        registry = SectionRegistry.GetRegistry(renderHandle);
    }

    public Task SetParametersAsync(ParameterView parameters)
    {
        var suppliedName = parameters.GetValueOrDefault<string>("Name")
            ?? throw new ArgumentException("Parameter Name is required.");

        if (suppliedName != subscribedName)
        {
            registry.Unsubscribe(subscribedName, onChangeCallback);
            registry.Subscribe(suppliedName, onChangeCallback);
            subscribedName = suppliedName;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        registry?.Unsubscribe(subscribedName, onChangeCallback);
    }
}