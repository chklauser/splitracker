using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;
using Riok.Mapperly.Abstractions;
using Splitracker.Domain;

namespace Splitracker.Persistence.Model;

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