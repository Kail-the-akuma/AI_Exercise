namespace IdeaBoard.App.Services;

/// <summary>
/// One row of either list. The description is deliberately absent: with dozens of ideas the
/// list has to be scannable at a glance.
/// </summary>
public class IdeaListItem
{
    /// <summary>Identifier, which the title links to.</summary>
    public int Id { get; set; }

    /// <summary>Title as submitted, already trimmed.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Name of whoever submitted it, read from the local user table.</summary>
    public string AuthorDisplayName { get; set; } = string.Empty;

    /// <summary>Instant of submission, in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Votes received, aggregated at read time and never stored.</summary>
    public int VoteCount { get; set; }

    /// <summary>Whether the person looking at the list has voted on this idea.</summary>
    public bool HasVoted { get; set; }
}


/// <summary>One idea in full, with the public list of who voted for it.</summary>
public class IdeaDetail
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>Title as submitted.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Description as submitted, rendered as plain text with line breaks kept.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Name of whoever submitted it.</summary>
    public string AuthorDisplayName { get; set; } = string.Empty;

    /// <summary>Instant of submission, in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Votes received.</summary>
    public int VoteCount { get; set; }

    /// <summary>Whether the person looking at the page has voted on this idea.</summary>
    public bool HasVoted { get; set; }

    /// <summary>Everyone who voted, oldest vote first. Transparency is deliberate here.</summary>
    public IReadOnlyList<IdeaVoter> Voters { get; set; } = new List<IdeaVoter>();
}


/// <summary>One name in the public list of voters.</summary>
public class IdeaVoter
{
    /// <summary>Name of the person who voted.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Instant of the vote, in UTC, which orders the list.</summary>
    public DateTime CreatedAt { get; set; }
}


/// <summary>
/// The state of one idea's vote after an operation. This is what the server returns and
/// what the interface renders, so that a count the server did not produce cannot appear
/// on screen.
/// </summary>
public class VoteState
{
    /// <summary>The idea the state belongs to.</summary>
    public int IdeaId { get; set; }

    /// <summary>Whether the person has a vote on it right now.</summary>
    public bool HasVoted { get; set; }

    /// <summary>Votes the idea has right now.</summary>
    public int VoteCount { get; set; }
}
