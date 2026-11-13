namespace IdeaBoard.App.Domain;

/// <summary>
/// A person who has signed in at least once AND whose name is known. The row is never
/// deleted, because ideas and votes reference it and the names shown across the
/// application are read from here rather than from the current identity.
///
/// The row is not written until there is a name to write. Somebody whose name could not be
/// obtained is sent to register one, and the row appears when they do. That is what makes
/// "every user row has a name" true everywhere, instead of a condition each page checks.
/// </summary>
public class User
{
    /// <summary>Surrogate key, assigned by the database.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The identifier the person is matched by, stable across renames. Under Windows
    /// authentication it is the account's security identifier.
    ///
    /// Named for what it is and not for where it comes from. The provider behind it has
    /// already changed once, and a column named after the provider of the day guarantees a
    /// second rename at the next change.
    /// </summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>
    /// Name shown as idea author and in the public list of voters.
    ///
    /// It is never empty. A person whose name could not be obtained, because the directory
    /// was unreachable and they have not registered one yet, has no row here at all. That
    /// keeps every page that renders an author or a voter free of a null check describing a
    /// state that lasts seconds, and it rules out the real failure, which is putting
    /// DOMAIN\someone or a GUID in front of colleagues as the author of an idea.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Where <see cref="DisplayName"/> came from. It governs whether a later lookup is
    /// allowed to change it, which is the one thing the name itself cannot tell anybody.
    /// </summary>
    public DisplayNameSource DisplayNameSource { get; set; } = DisplayNameSource.Directory;

    /// <summary>
    /// The account the person signs in under, in its DOMAIN\someone form. Kept so that two
    /// people with the same display name can be told apart by somebody reading the table
    /// by hand, and never rendered in the interface.
    ///
    /// It arrives in every request and costs no lookup, which is what makes it usable on
    /// the path where the directory is unreachable. That path is exactly where two
    /// identical names are hardest to tell apart, and it is why this replaced an email
    /// address, which could only ever have come from the same failed lookup.
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Instant of the first sign-in, in UTC.</summary>
    public DateTime FirstSeenAt { get; set; }
}
