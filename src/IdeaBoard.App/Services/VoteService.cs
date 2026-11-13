using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IdeaBoard.App.Services;

/// <summary>
/// Voting and withdrawing a vote, with the one vote per person rule enforced by the unique
/// index on Vote(ideaId, userId) rather than by the check in this code.
///
/// The check exists because it is the normal path. Correctness does not depend on it: two
/// nearly simultaneous requests from the same person both read "not voted" and both
/// insert, and it is the index that stops the second one. That violation is treated as
/// success, because the end state is the one the person asked for.
/// </summary>
public class VoteService : IVoteService
{
    #region Constants

    /// <summary>
    /// SQLITE_CONSTRAINT_UNIQUE. The extended result code is matched rather than the
    /// exception type alone, so that a disk error or a foreign key failure is never
    /// mistaken for a duplicate vote and reported to the person as success.
    /// </summary>
    private const int SqliteUniqueConstraintExtendedErrorCode = 2067;

    #endregion


    #region Fields

    private readonly IdeaBoardDbContext dbContext;
    private readonly ICurrentUser currentUser;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<VoteService> logger;

    #endregion


    #region Construction

    /// <summary>Creates the service over the request-scoped context.</summary>
    public VoteService(
        IdeaBoardDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        ILogger<VoteService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this.dbContext = dbContext;
        this.currentUser = currentUser;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    #endregion


    #region Voting

    /// <inheritdoc />
    public async Task<VoteState?> AddAsync(int ideaId, CancellationToken cancellationToken)
    {
        bool ideaExists = await IdeaExistsAsync(ideaId, cancellationToken);

        if (!ideaExists)
        {
            return null;
        }

        User signedInUser = await currentUser.GetAsync(cancellationToken);

        Vote? existing = await FindVoteAsync(ideaId, signedInUser.Id, cancellationToken);

        if (existing is not null)
        {
            return await BuildStateAsync(ideaId, signedInUser.Id, cancellationToken);
        }

        await InsertVoteAsync(ideaId, signedInUser.Id, cancellationToken);

        return await BuildStateAsync(ideaId, signedInUser.Id, cancellationToken);
    }


    /// <inheritdoc />
    public async Task<VoteState?> RemoveAsync(int ideaId, CancellationToken cancellationToken)
    {
        bool ideaExists = await IdeaExistsAsync(ideaId, cancellationToken);

        if (!ideaExists)
        {
            return null;
        }

        User signedInUser = await currentUser.GetAsync(cancellationToken);

        Vote? existing = await FindVoteAsync(ideaId, signedInUser.Id, cancellationToken);

        if (existing is null)
        {
            return await BuildStateAsync(ideaId, signedInUser.Id, cancellationToken);
        }

        await DeleteVoteAsync(existing, cancellationToken);

        return await BuildStateAsync(ideaId, signedInUser.Id, cancellationToken);
    }


    /// <inheritdoc />
    public async Task<VoteState?> GetStateAsync(int ideaId, CancellationToken cancellationToken)
    {
        bool ideaExists = await IdeaExistsAsync(ideaId, cancellationToken);

        if (!ideaExists)
        {
            return null;
        }

        User signedInUser = await currentUser.GetAsync(cancellationToken);

        return await BuildStateAsync(ideaId, signedInUser.Id, cancellationToken);
    }

    #endregion


    #region Persistence

    /// <summary>
    /// Inserts the vote, treating a violation of the unique index as success. Any other
    /// write failure propagates, because it is a real fault and hiding it here would
    /// disguise a broken database as a duplicate click.
    /// </summary>
    private async Task InsertVoteAsync(int ideaId, int userId, CancellationToken cancellationToken)
    {
        Vote vote = new Vote
        {
            IdeaId = ideaId,
            UserId = userId,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        dbContext.Votes.Add(vote);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsVoteUniqueIndexViolation(exception))
        {
            dbContext.Entry(vote).State = EntityState.Detached;

            logger.LogInformation(
                "A duplicate vote was stopped by the unique index and treated as success.");
        }
    }


    /// <summary>
    /// Deletes the vote, treating "the row was already gone" as success. Any other write
    /// failure propagates, for the same reason as on the insert path.
    /// </summary>
    private async Task DeleteVoteAsync(Vote vote, CancellationToken cancellationToken)
    {
        dbContext.Votes.Remove(vote);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Entry(vote).State = EntityState.Detached;

            logger.LogInformation(
                "A vote was already withdrawn by an earlier request, and the repeat was treated " +
                "as success.");
        }
    }


    /// <summary>
    /// Recognises a violation of the vote uniqueness rule, and nothing else. It matches the
    /// SQLite extended result code rather than the exception type, because catching every
    /// write failure would report real database faults to the person as a successful vote.
    /// </summary>
    private static bool IsVoteUniqueIndexViolation(DbUpdateException exception)
    {
        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        return sqliteException.SqliteExtendedErrorCode == SqliteUniqueConstraintExtendedErrorCode;
    }

    #endregion


    #region Reading

    /// <summary>Tells whether an idea with that identifier exists.</summary>
    private async Task<bool> IdeaExistsAsync(int ideaId, CancellationToken cancellationToken)
    {
        return await dbContext.Ideas
            .AsNoTracking()
            .AnyAsync(idea => idea.Id == ideaId, cancellationToken);
    }


    /// <summary>Finds the person's vote on an idea, tracked so that it can be removed.</summary>
    private async Task<Vote?> FindVoteAsync(int ideaId, int userId, CancellationToken cancellationToken)
    {
        return await dbContext.Votes
            .FirstOrDefaultAsync(vote => vote.IdeaId == ideaId && vote.UserId == userId, cancellationToken);
    }


    /// <summary>
    /// Reads the state the interface will render. It is read back from the database rather
    /// than inferred from what this method just did, so that the count on screen is always
    /// one the database produced.
    /// </summary>
    private async Task<VoteState> BuildStateAsync(int ideaId, int userId, CancellationToken cancellationToken)
    {
        int voteCount = await dbContext.Votes
            .AsNoTracking()
            .CountAsync(vote => vote.IdeaId == ideaId, cancellationToken);

        bool hasVoted = await dbContext.Votes
            .AsNoTracking()
            .AnyAsync(vote => vote.IdeaId == ideaId && vote.UserId == userId, cancellationToken);

        return new VoteState
        {
            IdeaId = ideaId,
            HasVoted = hasVoted,
            VoteCount = voteCount,
        };
    }

    #endregion
}
