using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;
using Riok.Mapperly.Abstractions;
using Splitracker.Domain;

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

[Mapper]
static partial class CharacterModelMapper
{
    public static Character ToDomain(this CharacterModel model)
    {
        return new(model.Id, model.Name, model.Lp.ToDomainLp(), model.Fo.ToDomainFo());
    }

    public static LpPool ToDomainLp(this PoolModel model)
    {
        return new(model.BaseCapacity, model.Points.ToDomain(), model.Channelings.ToImmutableArray());
    }

    public static FoPool ToDomainFo(this PoolModel model)
    {
        return new(model.BaseCapacity, model.Points.ToDomain(), model.Channelings.ToImmutableArray());
    }

    private static partial PointsVec ToDomain(this PointsModel points);
    
    public static partial CharacterModel ToDbModel(this Character character);
    
    [UsedImplicitly]
    static IImmutableList<int> toImmutableList(List<int> array)
    {
        return array.ToImmutableList();
    }
}

[UsedImplicitly]
class PoolModel
{
    public int BaseCapacity { get; set; } = 1;
    public PointsModel Points { get; set; } = new(0, 0, 0);
    public List<int> Channelings { get; set; } = new();
}

record PointsModel(int Channeled, int Exhausted, int Consumed)
{
    public int Channeled { get; set; } = Channeled;
    public int Exhausted { get; set; } = Exhausted;
    public int Consumed { get; set; } = Consumed;

    public void Deconstruct(out int channeled, out int exhausted, out int consumed)
    {
        channeled = Channeled;
        exhausted = Exhausted;
        consumed = Consumed;
    }
}