namespace Splitracker.Domain;

public record Character(string Id, string Name, Pool Lp, Pool Fo)
{
    public Character(string id, string name, int lpBaseCapacity, int foBaseCapacity) :
        this(id, name,
            new(lpBaseCapacity), new Pool(foBaseCapacity))
    {
    }
}