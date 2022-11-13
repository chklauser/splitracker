using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Splitracker.Web.Shared;

readonly struct CssClass
{
    public string Name { get; }
    public bool Enabled { get; private init; }

    [SetsRequiredMembers]
    public CssClass(string name)
    {
        Name = name;
        Enabled = true;
    }

    public CssClass If(bool enabled)
    {
        return new(Name) { Enabled = enabled };
    }
        
    public static string Rendered(params CssClass[] classes) => 
        string.Join(" ", classes.Where(c => c.Enabled).Select(c => c.Name));

    public static implicit operator CssClass(string cssClass) => new(cssClass);
    public static implicit operator CssClass((string, bool) cssClass) => new CssClass(cssClass.Item1).If(cssClass.Item2);
}
