using CentaurScores.Api.Application;

namespace CentaurScores.Api.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void Password_hash_round_trip_succeeds_and_wrong_password_fails()
    {
        var hash = Passwords.Hash("correct horse");
        Assert.True(Passwords.Verify("correct horse", hash));
        Assert.False(Passwords.Verify("wrong horse", hash));
        Assert.False(Passwords.Verify("correct horse", "not-a-valid-hash"));
    }
}