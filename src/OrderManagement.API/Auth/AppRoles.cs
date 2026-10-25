
namespace OrderManagement.API.Auth;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static bool IsKnownRole(string? role) =>
        string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, User, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string role)
    {
        if (string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase))
            return Admin;

        if (string.Equals(role, User, StringComparison.OrdinalIgnoreCase))
            return User;

        throw new ArgumentException("Invalid role.", nameof(role));
    }
}
