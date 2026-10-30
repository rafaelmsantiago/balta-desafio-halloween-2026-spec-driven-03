using Encurtador.Application.Links;

namespace Encurtador.UnitTests.Fakes;

/// <summary>Gerador que devolve códigos pré-definidos, para testar colisões.</summary>
internal sealed class SequenceCodeGenerator(params string[] codes) : ShortCodeGenerator
{
    private readonly Queue<string> codes = new(codes);

    public int Calls { get; private set; }

    public override string Generate()
    {
        Calls++;
        return codes.Count > 0 ? codes.Dequeue() : base.Generate();
    }
}
