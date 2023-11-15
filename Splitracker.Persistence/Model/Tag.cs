namespace Splitracker.Persistence.Model;

record Tag(string Id, string Name)
{
    public string Id { get; set; } = Id;
    public string Name { get; set; } = Name;
}

static class TagModelMapper
{
    public static Domain.Tag ToDomain(this Tag model) => new(model.Id, model.Name);
}