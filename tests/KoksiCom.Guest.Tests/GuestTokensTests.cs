using System.Security.Cryptography;
using KoksiCom.Api.Guest;

namespace KoksiCom.Guest.Tests;

public class GuestTokensTests
{
    [Fact]
    public void Issue_carries_display_name()
    {
        var tokens = new GuestTokens();
        var now = TimeProvider.System.GetUtcNow();

        var jwt = tokens.Issue("Ada2", now);

        Assert.True(tokens.TryReadName(jwt, now, out var name));
        Assert.Equal("Ada2", name);
    }

    [Fact]
    public void TryReadName_fails_after_expiry()
    {
        var tokens = new GuestTokens();
        var now = TimeProvider.System.GetUtcNow();
        var jwt = tokens.Issue("Ada2", now);

        Assert.False(tokens.TryReadName(jwt, now.AddHours(GuestTokens.LifetimeHours).AddSeconds(1), out _));
    }

    [Fact]
    public void TryReadName_fails_with_a_new_signing_key()
    {
        var now = TimeProvider.System.GetUtcNow();
        var first = new GuestTokens(RandomNumberGenerator.GetBytes(32));
        var second = new GuestTokens(RandomNumberGenerator.GetBytes(32));
        var jwt = first.Issue("Ada2", now);

        Assert.False(second.TryReadName(jwt, now, out _));
    }
}
