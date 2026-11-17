using IdeaBoard.App.Domain;

namespace IdeaBoard.App.Services;

/// <summary>Submission and listing of ideas.</summary>
public interface IIdeaService
{
    /// <summary>
    /// Stores a new idea authored by the current user. The values must already be trimmed
    /// and valid, which the caller establishes with <see cref="IdeaSubmission.Validate"/>.
    /// </summary>
    Task<Idea> SubmitAsync(string title, string description, CancellationToken cancellationToken);


    /// <summary>
    /// The ranking: most votes first, then most recent, then highest identifier. The tie
    /// break on the more recent idea is deliberate and gives a small, permanent advantage
    /// to whoever arrives later.
    ///
    /// A search term narrows the list to the ideas whose title or author matches it, and
    /// nothing else about the page changes: the order is the same order and the bars still
    /// measure against the board's leader rather than against the leader of the result.
    /// Null or blank is the unsearched page.
    /// </summary>
    Task<IReadOnlyList<IdeaListItem>> GetRankingAsync(string? searchTerm, CancellationToken cancellationToken);


    /// <summary>The same ideas and the same search, ordered by submission date descending.</summary>
    Task<IReadOnlyList<IdeaListItem>> GetRecentAsync(string? searchTerm, CancellationToken cancellationToken);


    /// <summary>
    /// The ideas the current person submitted, most recent first.
    ///
    /// This and the list below are kept apart on purpose, and not merged into one. An idea
    /// can be in both, because a person may vote on their own idea, and the two are also
    /// the only way to tell apart the two ways this tab can be empty: somebody who has not
    /// proposed anything, and somebody who has not voted on anything. On the first day
    /// everybody is both at once, so a single empty list would leave the page unable to say
    /// which of the two it is looking at.
    /// </summary>
    Task<IReadOnlyList<IdeaListItem>> GetMineAsync(CancellationToken cancellationToken);


    /// <summary>
    /// The ideas the current person voted on, most recent vote first. Withdrawing a vote
    /// deletes the row, so an idea leaves this list the moment the vote is withdrawn, and
    /// comes back at the top if the person votes again.
    /// </summary>
    Task<IReadOnlyList<IdeaListItem>> GetVotedByMeAsync(CancellationToken cancellationToken);


    /// <summary>One idea with its voters, or null when no idea has that identifier.</summary>
    Task<IdeaDetail?> GetDetailAsync(int ideaId, CancellationToken cancellationToken);
}
