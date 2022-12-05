using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Splitracker.Web.Shared.Timelines;

public partial class TimelineCreateEffectCard : IDisposable
{
    EditContext? editContext;
    readonly Model model = new();
    
    [Parameter]
    [EditorRequired]
    public required int SelectedTick { get; set; }

    [Inject]
    public required IServiceProvider ServiceProvider { get; set; }
    
    [Parameter]
    public EventCallback OnEffectCreated { get; set; }

    ValidationMessageStore? customValidationMessages;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        editContext = new(model);
        editContext.EnableDataAnnotationsValidation(ServiceProvider);
        editContext.OnValidationRequested += EditContextOnOnValidationRequested;
        editContext.OnFieldChanged += EditContextOnOnFieldChanged;
        customValidationMessages = new(editContext);
    }
    public void Dispose()
    {
        if (editContext != null)
        {
            editContext.OnValidationRequested -= EditContextOnOnValidationRequested;
            editContext.OnFieldChanged -= EditContextOnOnFieldChanged;
        }
    }

    bool hasValidationMessages => editContext?.GetValidationMessages().Any() ?? false;

    void EditContextOnOnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        if (customValidationMessages is not {} store)
        {
            return;
        }
        store.Clear();
        var validationStateHasChanged = false;

        if (model.Interval >= model.Duration)
        {
            store.Add(() => model.Interval,
                "Intervall darf nicht länger sein als die Dauer des Effekts.");
            validationStateHasChanged = true;
        }

        if (model.At + model.Duration > 999)
        {
            store.Add(() => model.Duration, "Die Tickleiste endet bei 999 😅. Bitte kürze die Dauer des Effekts.");
            validationStateHasChanged = true;
        }

        if (validationStateHasChanged)
        {
            editContext?.NotifyValidationStateChanged();
        }
    }

    void EditContextOnOnFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        customValidationMessages?.Clear(e.FieldIdentifier);
        editContext?.NotifyValidationStateChanged();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (model.At < 0)
        {
            model.At = SelectedTick;
        }
    }

    async Task createEffectButtonClicked()
    {
        if (editContext == null || !editContext.Validate())
        {
            return;
        }
        
        // TODO: actually create the effect
        
        await OnEffectCreated.InvokeAsync();
    }

    class Model
    {
        [Required(ErrorMessage = "Effekt braucht eine Beschreibung.")]
        [MinLength(1, ErrorMessage = "Effekt braucht eine Beschreibung.")]
        [MaxLength(300, ErrorMessage = "Könntest du dich bitte etwas kürzer fassen? 😅")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Effekt braucht einen Start-Zeitpunkt.")]
        public int At { get; set; } = -1;

        [Required(ErrorMessage = "Effekt braucht eine Dauer.")]
        public int Duration { get; set; } = 1;
        
        public int? Interval { get; set; }
    }
}