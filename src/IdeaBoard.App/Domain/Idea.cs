namespace IdeaBoard.App.Domain;

/// <summary>
/// An idea somebody put in front of the team. Submission is final: there is no edit, no
/// removal and no state, by decision of the briefing, so nothing here records a lifecycle.
/// </summary>
public class Idea
{
    /// <summary>Surrogate key, assigned by the database.</summary>
    public int Id { get; set; }

    /// <summary>Single line, at most <see cref="IdeaLimits.TitleMaximumLength"/> characters.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Plain text, at most <see cref="IdeaLimits.DescriptionMaximumLength"/> characters.
    /// Line breaks are preserved when rendered, and no markup is ever interpreted.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The person who submitted it.</summary>
    public int AuthorUserId { get; set; }

    /// <summary>Navigation to the author, used to show the name without a second query.</summary>
    public User? Author { get; set; }

    /// <summary>Instant of submission, in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Votes this idea has received.</summary>
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
