namespace LDHub.Helpers;

/// <summary>One password rule for registration, Resource Person accounts and the admin account page.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>Returns an error message, or null when the password is acceptable.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return $"Password must be at least {MinLength} characters.";

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Password must include at least one letter and one number.";

        return null;
    }
}
