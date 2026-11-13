namespace IdeaBoard.App.Services;

/// <summary>
/// Voting and withdrawing a vote.
///
/// Both operations are idempotent, and each carries an explicit intent rather than
/// toggling. A toggle would turn a double click into a vote followed by a withdrawal,
/// which is the opposite of what the person asked for. Asking twice to vote, or twice to
/// withdraw, leaves the database as it is and reports success either way.
/// </summary>
public interface IVoteService
{
    /// <summary>
    /// Records a vote by the current user. Returns null when no idea has that identifier.
    /// </summary>
    Task<VoteState?> AddAsync(int ideaId, CancellationToken cancellationToken);


    /// <summary>
    /// Withdraws the current user's vote. Returns null when no idea has that identifier.
    /// </summary>
    Task<VoteState?> RemoveAsync(int ideaId, CancellationToken cancellationToken);


    /// <summary>Reads the current state without changing anything.</summary>
    Task<VoteState?> GetStateAsync(int ideaId, CancellationToken cancellationToken);
}
