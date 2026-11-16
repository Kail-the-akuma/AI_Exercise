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

    /// <summary>
    /// How long this idea's bar is next to the leader's, from 0 to 100.
    ///
    /// It is a fact about the LIST and not about the idea. The same idea carries a
    /// different value in a list where something else leads, so only the service that
    /// built the list may set it, and it means nothing on an item read outside its list.
    ///
    /// The division is done here rather than in the page because the page would then have
    /// to handle the list where nothing has been voted on yet, and a view is the worst
    /// place to discover that a denominator can be zero.
    ///
    /// Truncated downwards, so no bar ever claims more support than its idea has. The
    /// leader is the only row that reaches 100, and a tie at the top puts every tied row
    /// there.
    /// </summary>
    public int SupportPercent { get; set; }

    /// <summary>
    /// How many of the latest voters a row carries at most. It is small on purpose: this is
    /// a sign of life on a card and not the list of who voted, which lives on the idea's own
    /// page and is complete there.
    /// </summary>
    public const int RecentVoterLimit = 4;

    /// <summary>
    /// The names of the last few people to vote, most recent vote first, at most
    /// <see cref="RecentVoterLimit"/> of them.
    ///
    /// Nothing here is private that was not already public. The warning that a vote is
    /// public is on screen before every button that casts one, and the full list of voters
    /// has always been on the idea's own page. What changes is where the names appear, not
    /// who may see them.
    ///
    /// The list is empty for an idea nobody has voted on, which is not the same shape as a
    /// row that was never asked: both read as empty, and only the service knows which it
    /// built.
    /// </summary>
    public IReadOnlyList<string> RecentVoterNames { get; set; } = [];
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
