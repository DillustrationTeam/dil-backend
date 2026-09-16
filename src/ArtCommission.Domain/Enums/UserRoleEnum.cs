namespace ArtCommission.Domain.Enums;

public enum UserRole
{
    Administrator,
    Moderator,
    Creator,
    Client
}

public static class UserRoleNames
{
    public const string Administrator = nameof(UserRole.Administrator);
    public const string Moderator = nameof(UserRole.Moderator);
    public const string Creator = nameof(UserRole.Creator);
    public const string Client = nameof(UserRole.Client);

    public static readonly string[] All = [Administrator, Moderator, Creator, Client];
}
