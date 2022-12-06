using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Splitracker.Domain;
using Splitracker.Domain.Commands;

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
    
    [CascadingParameter]
    public required ITimelineDispatcher Dispatcher { get; set; }
    
    [Parameter]
    [EditorRequired]
    public required IReadOnlyList<Character> TimelineCharacters { get; set; }

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
    
    Task<IEnumerable<Character>> completeCharacters(string? searchTerm)
    {
        return string.IsNullOrWhiteSpace(searchTerm)
            ? Task.FromResult((IEnumerable<Character>)TimelineCharacters)
            : Task.FromResult(TimelineCharacters
                .Where(c => c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
    }
    
    string renderCharacter(Character? character)
    {
        return character?.Name ?? string.Empty;
    }
    
    void characterSelectionKeyUp(KeyboardEventArgs obj)
    {
        if (obj.Code == "Enter" && model.SelectedCharacter is {} selected)
        {
            model.AffectedCharacters.Add(selected);
            model.SelectedCharacter = null;
        } 
    }

    async Task createEffectButtonClicked()
    {
        if (editContext == null || !editContext.Validate())
        {
            return;
        }

        await Dispatcher.ApplyCommandAsync(new TimelineCommand.AddEffect(
            null!,
            IdGenerator.RandomId(), 
            model.Description,
            model.At,
            model.Duration,
            model.Interval,
            model.AffectedCharacters.Select(c => c.Id).ToImmutableArray()));
        
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
        
        public List<Character> AffectedCharacters { get; set; } = new();
        
        public Character? SelectedCharacter { get; set; }
    }
}