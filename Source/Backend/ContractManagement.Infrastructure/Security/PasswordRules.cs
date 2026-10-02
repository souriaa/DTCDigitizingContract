namespace ContractManagement.Infrastructure.Security;

public static class PasswordRules
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 100;
    public const string ValidationMessage = "Mật khẩu phải có từ 12 đến 100 ký tự, ít nhất một chữ số (0–9), chữ hoa (A–Z) và chữ thường (a–z).";
    public static bool IsValid(string? password) => !string.IsNullOrWhiteSpace(password)
        && password.Length is >= MinimumLength and <= MaximumLength
        && password.Any(c => c is >= '0' and <= '9')
        && password.Any(c => c is >= 'A' and <= 'Z')
        && password.Any(c => c is >= 'a' and <= 'z');
}
