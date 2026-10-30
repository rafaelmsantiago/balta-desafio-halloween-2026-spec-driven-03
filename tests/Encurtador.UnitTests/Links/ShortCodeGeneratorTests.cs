using Encurtador.Application.Links;

namespace Encurtador.UnitTests.Links;

public class ShortCodeGeneratorTests
{
    private const string ExpectedAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";

    private readonly ShortCodeGenerator generator = new();

    [Fact]
    public void Generates_exactly_seven_characters()
    {
        Assert.Equal(7, generator.Generate().Length);
    }

    [Fact]
    public void Alphabet_has_56_symbols_without_ambiguous_characters()
    {
        Assert.Equal(ExpectedAlphabet, ShortCodeGenerator.Alphabet);
        Assert.Equal(56, ShortCodeGenerator.Alphabet.Length);
        Assert.DoesNotContain(ShortCodeGenerator.Alphabet, c => "0Oo1Il".Contains(c));
    }

    [Fact]
    public void Generated_codes_only_use_the_unambiguous_alphabet()
    {
        for (var i = 0; i < 10_000; i++)
        {
            var code = generator.Generate();

            Assert.All(code, c => Assert.Contains(c, ExpectedAlphabet));
        }
    }

    [Fact]
    public void Successive_codes_are_not_all_equal()
    {
        var codes = Enumerable.Range(0, 20).Select(_ => generator.Generate()).ToHashSet();

        Assert.True(codes.Count > 1);
    }
}
