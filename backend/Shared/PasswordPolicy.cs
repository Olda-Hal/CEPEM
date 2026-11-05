namespace Cepem.Shared;

/// <summary>Single password policy used by login-adjacent endpoints.</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public static bool IsValid(string? password) =>
        !string.IsNullOrWhiteSpace(password) &&
        password.Length >= MinimumLength &&
        password.Any(char.IsLetter) &&
        password.Any(char.IsDigit);

    public const string RequirementMessage =
        "Password must be at least 8 characters long and contain at least one letter and one digit.";
}
