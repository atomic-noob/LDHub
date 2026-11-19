namespace LDHub.Helpers;

public static class SessionExtensions
{
    /// <summary>
    /// The logged-in user's id. Login stores it with SetString("UserId", ...), so it must be read
    /// with GetString and parsed (GetInt32 would not work with that value).
    /// </summary>
    public static int? GetUserId(this ISession session) =>
        int.TryParse(session.GetString("UserId"), out var id) ? id : (int?)null;
}
