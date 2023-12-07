namespace Splitracker.Domain.Test;

[TestFixture]
public class NameGenerationServiceTest
{
    NameGenerationService service = null!;

    [SetUp]
    public void SetUp()
    {
        service = new();
    }

    [TestCase(arg: new[] { "a 1" }, ExpectedResult = "\uFFFC 2")]
    [TestCase(arg: new[] { "a 1", "b 2", "c 4" }, ExpectedResult = "\uFFFC 3")]
    [TestCase(arg: new[] { "a 11", "b 12", "c 4" }, ExpectedResult = "\uFFFC 1")]
    [TestCase(arg: new[] { "a 1", "b b", "c C", "d 2", "e 4" }, ExpectedResult = "\uFFFC 3")]
    public string InferNumeric(string[] existingNames) => generateOne(existingNames);

    [TestCase(arg: new[] { "a }", "b .", "c μ" }, ExpectedResult = "\uFFFC 1")]
    [TestCase(arg: new[] { "Flark", "Zorg", "Boop" }, ExpectedResult = "\uFFFC 1")]
    [TestCase(arg: new string[] {}, ExpectedResult = "\uFFFC 1")]
    public string NumericIsTheDefault(string[] existingNames) => generateOne(existingNames);

    [TestCase(arg: new[] { "a a" }, ExpectedResult = "\uFFFC b")]
    [TestCase(arg: new[] { "a a", "b b", "c d" }, ExpectedResult = "\uFFFC c")]
    [TestCase(arg: new[] { "a ba", "b abc", "c d" }, ExpectedResult = "\uFFFC b")]
    [TestCase(arg: new[] { "a a", "b 2", "c C", "d b", "e d" }, ExpectedResult = "\uFFFC c")]
    public string InferLowerAlpha(string[] existingNames) => generateOne(existingNames);
    
    [TestCase(arg: new[] { "a A" }, ExpectedResult = "\uFFFC B")]
    [TestCase(arg: new[] { "a A", "b B", "c D" }, ExpectedResult = "\uFFFC C")]
    [TestCase(arg: new[] { "a BA", "b ABC", "c D" }, ExpectedResult = "\uFFFC B")]
    [TestCase(arg: new[] { "a A", "b 2", "c c", "d B", "e D" }, ExpectedResult = "\uFFFC C")]
    public string InferUpperAlpha(string[] existingNames) => generateOne(existingNames);
    
    [TestCase(arg: "Flark", ExpectedResult = "Flark")]
    [TestCase(arg: " Boop ", ExpectedResult = "Boop")]
    [TestCase(arg: "a 11", ExpectedResult = "a")]
    [TestCase(arg: "a AB", ExpectedResult = "a")]
    [TestCase(arg: "a aa", ExpectedResult = "a")]
    public string InferTemplateBaseName(string combinedName) => service.InferTemplateName(combinedName);

    string generateOne(string[] existingNames)
    {
        var scheme = service.InferNamingScheme(existingNames);
        return scheme.GenerateNext();
    }
}