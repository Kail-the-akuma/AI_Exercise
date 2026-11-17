namespace IdeaBoard.App.Domain;

/// <summary>
/// Something somebody said about an idea.
///
/// Unlike an idea, a comment can be changed and can be removed, and only by whoever wrote
/// it. That is a deliberate difference and not an inconsistency: an idea is a proposal the
/// board has voted on, so changing it afterwards would change what the votes were for,
/// while a comment carries no votes and belongs to one person.
///
/// A removed comment is removed. Nothing is left behind saying that something was here,
/// because a visible gap is a decision about a person in front of everybody else, and this
/// product has no moderation and no triage to make such a decision inside.
/// </summary>
public class Comment
{
    /// <summary>Surrogate key, assigned by the database.</summary>
    public int Id { get; set; }

    /// <summary>The idea this was said about.</summary>
    public int IdeaId { get; set; }

    /// <summary>Navigation to the idea, used by the authorisation checks.</summary>
    public Idea? Idea { get; set; }

    /// <summary>
    /// The person who wrote it, and the only person allowed to change or remove it. The
    /// check lives on the server and never in whether a button was drawn.
    /// </summary>
    public int AuthorUserId { get; set; }

    /// <summary>Navigation to the author, used to show the name without a second query.</summary>
    public User? Author { get; set; }

    /// <summary>
    /// Plain text, at most <see cref="CommentLimits.TextMaximumLength"/> characters. Line
    /// breaks are preserved when rendered, and no markup is ever interpreted.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Instant it was written, in UTC. The list is ordered by this.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Instant it was last changed, in UTC, or null if it never was.
    ///
    /// Null is the honest state for a comment nobody edited, and it is what lets the page
    /// say a comment was changed without having to compare two dates and decide how close
    /// is close enough to count as the same instant.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
