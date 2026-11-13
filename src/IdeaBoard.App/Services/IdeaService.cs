using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using Microsoft.EntityFrameworkCore;

namespace IdeaBoard.App.Services;

/// <summary>
/// Submission and listing of ideas, over the single database context.
///
/// The vote count is aggregated in the query and never stored, so it cannot drift from the
/// votes that make it up. Whether the current person has already voted is resolved with one
/// extra query for the whole list, not one query per row, which is the single place in this
/// product where an N plus 1 problem is easy to create.
/// </summary>
public class IdeaService : IIdeaService
{
    #region Fields

    private readonly IdeaBoardDbContext dbContext;
    private readonly ICurrentUser currentUser;
    private readonly TimeProvider timeProvider;

    #endregion


    #region Construction

    /// <summary>Creates the service over the request-scoped context.</summary>
    public IdeaService(
        IdeaBoardDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.dbContext = dbContext;
        this.currentUser = currentUser;
        this.timeProvider = timeProvider;
    }

    #endregion


    #region Submission

    /// <inheritdoc />
    public async Task<Idea> SubmitAsync(string title, string description, CancellationToken cancellationToken)
    {
        IdeaSubmissionResult validation = IdeaSubmission.Validate(title, description);

        if (!validation.IsValid)
        {
            throw new ArgumentException(
                "The idea was rejected by domain validation. Validate before calling this method.",
                nameof(title));
        }

        User author = await currentUser.GetAsync(cancellationToken);

        Idea idea = new Idea
        {
            Title = validation.Title,
            Description = validation.Description,
            AuthorUserId = author.Id,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        dbContext.Ideas.Add(idea);
        await dbContext.SaveChangesAsync(cancellationToken);

        return idea;
    }

    #endregion


    #region Listing

    /// <inheritdoc />
    public async Task<IReadOnlyList<IdeaListItem>> GetRankingAsync(CancellationToken cancellationToken)
    {
        List<IdeaListItem> items = await ProjectIdeas()
            .OrderByDescending(item => item.VoteCount)
            .ThenByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        await MarkVotedAsync(items, cancellationToken);

        return items;
    }


    /// <inheritdoc />
    public async Task<IReadOnlyList<IdeaListItem>> GetRecentAsync(CancellationToken cancellationToken)
    {
        List<IdeaListItem> items = await ProjectIdeas()
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        await MarkVotedAsync(items, cancellationToken);

        return items;
    }


    /// <summary>Projects every idea with its author and its aggregated vote count.</summary>
    private IQueryable<IdeaListItem> ProjectIdeas()
    {
        return dbContext.Ideas
            .AsNoTracking()
            .Select(idea => new IdeaListItem
            {
                Id = idea.Id,
                Title = idea.Title,
                AuthorDisplayName = idea.Author!.DisplayName,
                CreatedAt = idea.CreatedAt,
                VoteCount = idea.Votes.Count,
            });
    }


    /// <summary>
    /// Fills in the vote state for a whole list with a single extra query, rather than
    /// asking the database once per row.
    /// </summary>
    private async Task MarkVotedAsync(List<IdeaListItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        User signedInUser = await currentUser.GetAsync(cancellationToken);

        List<int> votedIdeaIds = await dbContext.Votes
            .AsNoTracking()
            .Where(vote => vote.UserId == signedInUser.Id)
            .Select(vote => vote.IdeaId)
            .ToListAsync(cancellationToken);

        HashSet<int> votedIdeaIdSet = new HashSet<int>(votedIdeaIds);

        foreach (IdeaListItem item in items)
        {
            item.HasVoted = votedIdeaIdSet.Contains(item.Id);
        }
    }

    #endregion


    #region Detail

    /// <inheritdoc />
    public async Task<IdeaDetail?> GetDetailAsync(int ideaId, CancellationToken cancellationToken)
    {
        IdeaDetail? detail = await dbContext.Ideas
            .AsNoTracking()
            .Where(idea => idea.Id == ideaId)
            .Select(idea => new IdeaDetail
            {
                Id = idea.Id,
                Title = idea.Title,
                Description = idea.Description,
                AuthorDisplayName = idea.Author!.DisplayName,
                CreatedAt = idea.CreatedAt,
                VoteCount = idea.Votes.Count,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (detail is null)
        {
            return null;
        }

        detail.Voters = await LoadVotersAsync(ideaId, cancellationToken);

        User signedInUser = await currentUser.GetAsync(cancellationToken);

        detail.HasVoted = await dbContext.Votes
            .AsNoTracking()
            .AnyAsync(vote => vote.IdeaId == ideaId && vote.UserId == signedInUser.Id, cancellationToken);

        return detail;
    }


    /// <summary>Reads the public list of voters, oldest vote first.</summary>
    private async Task<IReadOnlyList<IdeaVoter>> LoadVotersAsync(int ideaId, CancellationToken cancellationToken)
    {
        return await dbContext.Votes
            .AsNoTracking()
            .Where(vote => vote.IdeaId == ideaId)
            .OrderBy(vote => vote.CreatedAt)
            .ThenBy(vote => vote.Id)
            .Select(vote => new IdeaVoter
            {
                DisplayName = vote.User!.DisplayName,
                CreatedAt = vote.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    #endregion
}
