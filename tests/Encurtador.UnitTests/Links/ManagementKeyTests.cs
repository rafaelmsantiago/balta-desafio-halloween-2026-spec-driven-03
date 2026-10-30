using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Encurtador.Application.Links;

namespace Encurtador.UnitTests.Links;

public class ManagementKeyTests
{
    [Fact]
    public void Generated_key_is_43_base64url_characters()
    {
        var key = ManagementKey.Generate();

        Assert.Matches(new Regex("^[A-Za-z0-9_-]{43}$"), key.PlainText);
    }

    [Fact]
    public void Hash_is_sha256_of_the_key_bytes()
    {
        var key = ManagementKey.Generate();
        var bytes = Base64Url.DecodeFromChars(key.PlainText);

        Assert.Equal(32, key.Hash.Length);
        Assert.Equal(SHA256.HashData(bytes), key.Hash);
        Assert.NotEqual(bytes, key.Hash);
    }

    [Fact]
    public void Two_generated_keys_are_different()
    {
        Assert.NotEqual(ManagementKey.Generate().PlainText, ManagementKey.Generate().PlainText);
    }

    [Fact]
    public void ComputeHash_is_deterministic()
    {
        var key = ManagementKey.Generate();

        Assert.Equal(key.Hash, ManagementKey.ComputeHash(key.PlainText));
        Assert.Equal(ManagementKey.ComputeHash(key.PlainText), ManagementKey.ComputeHash(key.PlainText));
    }

    [Fact]
    public void Verify_accepts_the_generated_pair()
    {
        var key = ManagementKey.Generate();

        Assert.True(ManagementKey.Verify(key.PlainText, key.Hash));
    }

    [Fact]
    public void Verify_rejects_another_valid_key()
    {
        var key = ManagementKey.Generate();

        Assert.False(ManagementKey.Verify(ManagementKey.Generate().PlainText, key.Hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("@@@")]
    [InlineData("YWJj")]
    [InlineData("abcdefg")]
    public void Verify_rejects_missing_or_malformed_keys_without_throwing(string? candidate)
    {
        var key = ManagementKey.Generate();

        Assert.False(ManagementKey.Verify(candidate, key.Hash));
    }
}
