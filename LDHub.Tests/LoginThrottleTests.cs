using LDHub.Helpers;

namespace LDHub.Tests;

public class LoginThrottleTests
{
    private static string KeyFor(string email, string ip = "10.0.0.1") =>
        LoginThrottle.Key(TestHttp.Context(ip: ip), email);

    [Fact]
    public void Four_failures_do_not_lock_the_account()
    {
        var throttle = new LoginThrottle();
        var key = KeyFor("user@example.com");

        for (var i = 0; i < 4; i++) throttle.RegisterFailure(key);

        Assert.False(throttle.IsLocked(key, out _));
    }

    [Fact]
    public void The_fifth_failure_locks_for_about_15_minutes()
    {
        var throttle = new LoginThrottle();
        var key = KeyFor("user@example.com");

        for (var i = 0; i < 5; i++) throttle.RegisterFailure(key);

        Assert.True(throttle.IsLocked(key, out var remaining));
        Assert.True(remaining > TimeSpan.FromMinutes(14));
        Assert.True(remaining <= TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void A_successful_login_clears_the_failures()
    {
        var throttle = new LoginThrottle();
        var key = KeyFor("user@example.com");

        for (var i = 0; i < 5; i++) throttle.RegisterFailure(key);
        throttle.Reset(key);

        Assert.False(throttle.IsLocked(key, out _));
    }

    [Fact]
    public void Locking_one_email_does_not_lock_another()
    {
        var throttle = new LoginThrottle();
        var locked = KeyFor("one@example.com");
        var other  = KeyFor("two@example.com");

        for (var i = 0; i < 5; i++) throttle.RegisterFailure(locked);

        Assert.True(throttle.IsLocked(locked, out _));
        Assert.False(throttle.IsLocked(other, out _));
    }

    [Fact]
    public void The_same_email_from_another_address_is_not_locked()
    {
        var throttle = new LoginThrottle();
        var attacker = KeyFor("user@example.com", "10.0.0.1");
        var owner    = KeyFor("user@example.com", "10.0.0.2");

        for (var i = 0; i < 5; i++) throttle.RegisterFailure(attacker);

        Assert.True(throttle.IsLocked(attacker, out _));
        Assert.False(throttle.IsLocked(owner, out _));
    }

    [Fact]
    public void The_key_ignores_capital_letters_and_spaces_in_the_email()
    {
        Assert.Equal(KeyFor("user@example.com"), KeyFor("  User@Example.COM "));
    }
}
