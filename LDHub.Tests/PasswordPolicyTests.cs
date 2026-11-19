using LDHub.Helpers;

namespace LDHub.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("abcdefg1")]
    [InlineData("Passw0rdOK")]
    [InlineData("12345678a")]
    public void Accepts_passwords_with_8_characters_a_letter_and_a_number(string password)
    {
        Assert.Null(PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData("a1b2c3")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_passwords_that_are_too_short_or_missing(string? password)
    {
        var error = PasswordPolicy.Validate(password);

        Assert.NotNull(error);
        Assert.Contains("at least 8", error);
    }

    [Fact]
    public void Rejects_a_password_with_no_number()
    {
        var error = PasswordPolicy.Validate("abcdefghij");

        Assert.NotNull(error);
        Assert.Contains("letter and one number", error);
    }

    [Fact]
    public void Rejects_a_password_with_no_letter()
    {
        var error = PasswordPolicy.Validate("1234567890");

        Assert.NotNull(error);
        Assert.Contains("letter and one number", error);
    }
}
