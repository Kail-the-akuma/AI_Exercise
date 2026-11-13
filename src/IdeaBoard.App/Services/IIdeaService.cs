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
    /// </summary>
    Task<IReadOnlyList<IdeaListItem>> GetRankingAsync(CancellationToken cancellationToken);


    /// <summary>The same ideas, ordered by submission date descending.</summary>
    Task<IReadOnlyList<IdeaListItem>> GetRecentAsync(CancellationToken cancellationToken);


    /// <summary>One idea with its voters, or null when no idea has that identifier.</summary>
    Task<IdeaDetail?> GetDetailAsync(int ideaId, CancellationToken cancellationToken);
}
