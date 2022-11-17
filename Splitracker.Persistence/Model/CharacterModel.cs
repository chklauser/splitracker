using System.Collections.Immutable;
using Riok.Mapperly.Abstractions;
using Splitracker.Domain;

namespace Splitracker.Persistence.Model;

record CharacterModel(string Id, string Name, PoolModel Lp, PoolModel Fo);

[Mapper]
static partial class CharacterModelMapper
{
    public static partial Character ToDomain(this CharacterModel model);

    public static partial CharacterModel FromDomain(this Character character);
}

class PoolModel
{
    public int BaseCapacity { get; init; }
    public PointsModel Points { get; init; }
    public IImmutableList<int> Channelings { get; init; } = ImmutableArray<int>.Empty;
}

readonly record struct PointsModel(int Channeled, int Exhausted, int Consumed);