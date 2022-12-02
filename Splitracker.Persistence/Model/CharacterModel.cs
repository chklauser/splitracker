using JetBrains.Annotations;

namespace Splitracker.Persistence.Model;

[UsedImplicitly]
record CharacterModel(string Id, string Name, PoolModel Lp, PoolModel Fo)
{
    public string Id { get; set; } = Id;
    public string Name { get; set; } = Name;
    public PoolModel Lp { get; set; } = Lp;
    public PoolModel Fo { get; set; } = Fo;

    public void Deconstruct(out string id, out string name, out PoolModel lp, out PoolModel fo)
    {
        id = Id;
        name = Name;
        lp = Lp;
        fo = Fo;
    }
}