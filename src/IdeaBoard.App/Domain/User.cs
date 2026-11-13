namespace IdeaBoard.App.Domain;

/// <summary>
/// A person who has signed in at least once. The row is created on the first sign-in and
/// is never deleted, because ideas and votes reference it and the names shown across the
/// application are read from here rather than from the current token.
/// </summary>
public class User
{
    /// <summary>Surrogate key, assigned by the database.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Entra ID object identifier. This is the matching key, never the email: an email
    /// changes when someone changes name, an object identifier does not.
    /// </summary>
    public string EntraObjectId { get; set; } = string.Empty;

    /// <summary>Name shown as idea author and in the public list of voters.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Kept to tell people apart, never rendered in the interface.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Instant of the first sign-in, in UTC.</summary>
    public DateTime FirstSeenAt { get; set; }
}
