using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Splitracker.Persistence;

[UsedImplicitly(ImplicitUseTargetFlags.Itself)]
[SuppressMessage("Design", "MA0016:Prefer returning collection abstraction instead of implementation")]
public class RavenOptions
{
    [Required]
    public required string Database
    {
        get;
        [UsedImplicitly]
        set;
    }

    [Required]
    [MinLength(1)]
    [UsedImplicitly]
    public List<string> Urls
    {
        get;
        set;
    } = new();
    
    [Required]
    public required string CertificatePath
    {
        get;
        [UsedImplicitly]
        set;
    }
    
    public string? CertificatePassword
    {
        get;
        [UsedImplicitly]
        set;
    }
    
    public string? CertificatePasswordFile
    {
        get;
        [UsedImplicitly]
        set;
    }
}