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

        AssignSupportPercent(items, LeadingVoteCountOf(items));

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

        AssignSupportPercent(items, LeadingVoteCountOf(items));

        return items;
    }


    /// <inheritdoc />
    public async Task<IReadOnlyList<IdeaListItem>> GetMineAsync(CancellationToken cancellationToken)
    {
        User signedInUser = await currentUser.GetAsync(cancellationToken);

        List<IdeaListItem> items = await ProjectIdeas(
                dbContext.Ideas.Where(idea => idea.AuthorUserId == signedInUser.Id))
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        await MarkVotedAsync(items, cancellationToken);

        AssignSupportPercent(items, await ReadLeadingVoteCountAsync(cancellationToken));

        return items;
    }


    /// <inheritdoc />
    public async Task<IReadOnlyList<IdeaListItem>> GetVotedByMeAsync(CancellationToken cancellationToken)
    {
        User signedInUser = await currentUser.GetAsync(cancellationToken);

        // The votes are read first, and in their own order, because the order of this list
        // is the order of the VOTES and not of the ideas. It cannot be recovered after the
        // ideas are projected, since nothing in an idea records when somebody voted on it.
        List<int> votedIdeaIds = await dbContext.Votes
            .AsNoTracking()
            .Where(vote => vote.UserId == signedInUser.Id)
            .OrderByDescending(vote => vote.CreatedAt)
            .ThenByDescending(vote => vote.Id)
            .Select(vote => vote.IdeaId)
            .ToListAsync(cancellationToken);

        if (votedIdeaIds.Count == 0)
        {
            return [];
        }

        List<IdeaListItem> items = await ProjectIdeas(
                dbContext.Ideas.Where(idea => votedIdeaIds.Contains(idea.Id)))
            .ToListAsync(cancellationToken);

        items = ArrangeByVoteOrder(items, votedIdeaIds);

        // Nothing is asked about the vote state here, and that is not an oversight. Every
        // idea on this list is on it BECAUSE the current person voted on it, so the answer
        // is known before the question, and asking would be a query to confirm the filter
        // that produced the list.
        foreach (IdeaListItem item in items)
        {
            item.HasVoted = true;
        }

        AssignSupportPercent(items, await ReadLeadingVoteCountAsync(cancellationToken));

        return items;
    }


    /// <summary>
    /// Puts the ideas back in the order the votes came in, which the database returned them
    /// out of. The vote order is walked once and each idea picked out of a map, so the cost
    /// stays linear rather than searching the list per vote.
    /// </summary>
    private static List<IdeaListItem> ArrangeByVoteOrder(List<IdeaListItem> items, List<int> votedIdeaIds)
    {
        Dictionary<int, IdeaListItem> itemById = new Dictionary<int, IdeaListItem>(items.Count);

        foreach (IdeaListItem item in items)
        {
            itemById[item.Id] = item;
        }

        List<IdeaListItem> arranged = new List<IdeaListItem>(items.Count);

        foreach (int ideaId in votedIdeaIds)
        {
            if (!itemById.TryGetValue(ideaId, out IdeaListItem? item))
            {
                continue;
            }

            arranged.Add(item);
        }

        return arranged;
    }


    /// <summary>Projects every idea with its author and its aggregated vote count.</summary>
    private IQueryable<IdeaListItem> ProjectIdeas()
    {
        return ProjectIdeas(dbContext.Ideas);
    }


    /// <summary>The same projection over a narrower set of ideas.</summary>
    private static IQueryable<IdeaListItem> ProjectIdeas(IQueryable<Idea> ideas)
    {
        return ideas
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


    /// <summary>
    /// Works out how long each bar is, given the count the bars are measured against.
    ///
    /// The leading count is a parameter and not something worked out here, because where it
    /// comes from depends on the list. That day was foreseen in a comment and has arrived:
    /// the two main tabs carry every idea, so their own largest count IS the leading count
    /// and costs nothing to find, while the personal tab shows a subset and has to ask the
    /// table, or its bars would measure against whatever that person happens to have.
    /// </summary>
    private static void AssignSupportPercent(List<IdeaListItem> items, int leaderVoteCount)
    {
        foreach (IdeaListItem item in items)
        {
            if (leaderVoteCount <= 0 || item.VoteCount <= 0)
            {
                // Either nothing has been voted on at all, or this idea has not been. With
                // no leader there is nothing to measure against, and an empty bar is the
                // honest picture rather than a full one.
                item.SupportPercent = 0;
                continue;
            }

            item.SupportPercent = item.VoteCount * 100 / leaderVoteCount;
        }
    }


    /// <summary>
    /// The largest count in a list that is known to hold every idea. No query: the counts
    /// have already been read.
    /// </summary>
    private static int LeadingVoteCountOf(List<IdeaListItem> items)
    {
        int leaderVoteCount = 0;

        foreach (IdeaListItem item in items)
        {
            if (item.VoteCount > leaderVoteCount)
            {
                leaderVoteCount = item.VoteCount;
            }
        }

        return leaderVoteCount;
    }


    /// <summary>
    /// The largest count in the WHOLE table, for the lists that show a subset.
    ///
    /// One command, whatever the size of the list, and the count is nullable on purpose:
    /// an empty table has no maximum at all, and asking for one without saying so throws
    /// rather than answering zero.
    /// </summary>
    private async Task<int> ReadLeadingVoteCountAsync(CancellationToken cancellationToken)
    {
        int? leadingVoteCount = await dbContext.Ideas
            .AsNoTracking()
            .Select(idea => (int?)idea.Votes.Count)
            .MaxAsync(cancellationToken);

        if (leadingVoteCount is null)
        {
            return 0;
        }

        return leadingVoteCount.Value;
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
