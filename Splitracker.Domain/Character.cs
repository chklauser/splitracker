namespace Splitracker.Domain;

public record Character(string Id, string Name, LpPool Lp, FoPool Fo)
{
    public Character(string id, string name, int lpBaseCapacity, int foBaseCapacity) :
        this(id, name, new(lpBaseCapacity), new FoPool(foBaseCapacity))
    {
    }
}