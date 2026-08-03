using CafeChain.Application.Constants;
using CafeChain.Application.Services.POS;
using Microsoft.AspNetCore.DataProtection;

namespace CafeChain.Tests.POS;

public sealed class OperationalOtpCodeAndProtectionTests
{
    private readonly OtpCodeGenerator _generator = new();

    [Fact]
    public void Generator_produces_six_cryptographically_random_allowed_characters()
    {
        var generated = Enumerable.Range(0, 100)
            .Select(_ => _generator.Generate())
            .ToArray();

        Assert.All(generated, code =>
        {
            Assert.Equal(OtpConstants.CodeLength, code.Length);
            Assert.All(code, character => Assert.Contains(character, OtpConstants.Alphabet));
            Assert.DoesNotContain(code, character => "O0I1".Contains(character));
        });
        Assert.True(generated.Distinct(StringComparer.Ordinal).Count() > 90);
    }

    [Theory]
    [InlineData("A@2B3C")]
    [InlineData("A 2B3C")]
    [InlineData("A\n2B3C")]
    [InlineData("Á2B3C4")]
    [InlineData("A2B3C😀")]
    [InlineData("A2B3C")]
    [InlineData("A2B3C45")]
    [InlineData("O2B3C4")]
    [InlineData("A0B3C4")]
    [InlineData("A2I3C4")]
    [InlineData("A21BC4")]
    public void Normalizer_rejects_special_ambiguous_or_wrong_length_codes(string value)
    {
        Assert.Null(_generator.NormalizeAndValidate(value));
    }

    [Fact]
    public void Normalizer_trims_ends_and_normalizes_ascii_lowercase()
    {
        Assert.Equal("A2B3C4", _generator.NormalizeAndValidate("  a2b3c4  "));
    }

    [Fact]
    public void Protected_payload_is_bound_to_challenge_approver_and_expiry()
    {
        const string otp = "A2B3C4";
        var challengeId = Guid.NewGuid();
        var expiresAtUtc = new DateTime(2026, 8, 3, 6, 0, 0, DateTimeKind.Utc);
        var service = new OtpProtectedPayloadService(
            new EphemeralDataProtectionProvider(),
            _generator);

        var ciphertext = service.Protect(challengeId, 15, otp, expiresAtUtc);

        Assert.DoesNotContain(otp, ciphertext, StringComparison.Ordinal);
        Assert.True(service.TryUnprotect(
            ciphertext,
            challengeId,
            15,
            expiresAtUtc,
            expiresAtUtc.AddMinutes(-1),
            out var recovered));
        Assert.Equal(otp, recovered);

        Assert.False(service.TryUnprotect(
            ciphertext,
            challengeId,
            16,
            expiresAtUtc,
            expiresAtUtc.AddMinutes(-1),
            out _));
        Assert.False(service.TryUnprotect(
            ciphertext,
            Guid.NewGuid(),
            15,
            expiresAtUtc,
            expiresAtUtc.AddMinutes(-1),
            out _));
        Assert.False(service.TryUnprotect(
            ciphertext,
            challengeId,
            15,
            expiresAtUtc,
            expiresAtUtc,
            out _));
    }
}
