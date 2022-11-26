using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace Splitracker.Persistence;

[UsedImplicitly(ImplicitUseTargetFlags.Itself)]
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