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
    public async Task<IReadOnlyList<IdeaListItem>> GetRankingAsync(
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        List<IdeaListItem> everything = await ProjectIdeas()
            .OrderByDescending(item => item.VoteCount)
            .ThenByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return await NarrowAsync(everything, searchTerm, cancellationToken);
    }


    /// <inheritdoc />
    public async Task<IReadOnlyList<IdeaListItem>> GetRecentAsync(
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        List<IdeaListItem> everything = await ProjectIdeas()
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return await NarrowAsync(everything, searchTerm, cancellationToken);
    }


    /// <summary>
    /// Applies the search term to a list that holds every idea, and fills in the rest.
    ///
    /// The order of the three steps is the whole content of this method. The leading count
    /// is taken from the FULL list, before anything is removed, so that the bars keep
    /// measuring against the board and not against the result of somebody's search. A
    /// search for two ideas with one and two votes would otherwise fill the second bar and
    /// tell that person they are looking at the leader of a board whose leader has seven.
    ///
    /// Marking the vote state comes after the narrowing and not before it, so that a search
    /// matching nothing costs one command rather than two.
    /// </summary>
    private async Task<IReadOnlyList<IdeaListItem>> NarrowAsync(
        List<IdeaListItem> everything,
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        int leaderVoteCount = LeadingVoteCountOf(everything);

        List<IdeaListItem> items = IdeaSearch.Filter(everything, searchTerm);

        await MarkVotedAsync(items, cancellationToken);
        await LoadRecentVotersAsync(items, cancellationToken);

        AssignSupportPercent(items, leaderVoteCount);

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

        await LoadRecentVotersAsync(items, cancellationToken);

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

        await LoadRecentVotersAsync(items, cancellationToken);

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
    /// Reads the latest voters for a whole list with a single extra query, rather than
    /// asking the database once per row.
    ///
    /// The shape of this method is forced by a measured limitation and not chosen. Asking
    /// for the latest few voters INSIDE the projection, which would fetch exactly the rows
    /// needed and cost no extra command at all, makes Entity Framework translate the query
    /// with SQL APPLY, and SQLite does not support it: the query throws at run time with
    /// "Translating this query requires the SQL APPLY operation". Measured against a real
    /// database through this stack, not assumed.
    ///
    /// So the votes of the ideas on screen are read in one ordered pass and cut down here.
    /// The cost grows with the number of VOTES on the page and not with the number of
    /// ideas, and the command count stays flat, which is the property the page can actually
    /// be ruined by losing.
    /// </summary>
    private async Task LoadRecentVotersAsync(List<IdeaListItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        List<int> ideaIds = new List<int>(items.Count);

        foreach (IdeaListItem item in items)
        {
            ideaIds.Add(item.Id);
        }

        List<RecentVoteRow> rows = await dbContext.Votes
            .AsNoTracking()
            .Where(vote => ideaIds.Contains(vote.IdeaId))
            .OrderByDescending(vote => vote.CreatedAt)
            .ThenByDescending(vote => vote.Id)
            .Select(vote => new RecentVoteRow
            {
                IdeaId = vote.IdeaId,
                DisplayName = vote.User!.DisplayName,
            })
            .ToListAsync(cancellationToken);

        Dictionary<int, List<string>> namesByIdea = new Dictionary<int, List<string>>(items.Count);

        foreach (RecentVoteRow row in rows)
        {
            if (!namesByIdea.TryGetValue(row.IdeaId, out List<string>? names))
            {
                names = new List<string>(IdeaListItem.RecentVoterLimit);
                namesByIdea[row.IdeaId] = names;
            }

            if (names.Count >= IdeaListItem.RecentVoterLimit)
            {
                continue;
            }

            names.Add(row.DisplayName);
        }

        foreach (IdeaListItem item in items)
        {
            if (!namesByIdea.TryGetValue(item.Id, out List<string>? names))
            {
                continue;
            }

            item.RecentVoterNames = names;
        }
    }


    /// <summary>One vote, reduced to the idea it belongs to and the name to show for it.</summary>
    private sealed class RecentVoteRow
    {
        /// <summary>The idea the vote was cast on.</summary>
        public int IdeaId { get; set; }

        /// <summary>The name of whoever cast it.</summary>
        public string DisplayName { get; set; } = string.Empty;
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
    ///
    /// THE CALLER HAS TO HAVE READ EVERY IDEA FOR THIS TO BE THE LEADING COUNT, and the two
    /// main tabs do, because neither of them pages. The search narrows the list AFTER this
    /// has been taken, which is why it can still be taken from memory.
    ///
    /// The day somebody adds paging, the observable trigger is a Skip or a Take anywhere on
    /// the way to this call, or a page model that stops saying it carries every idea. On
    /// that day two things break at once and neither of them errors: the bars start
    /// measuring against whatever is on the current page, and the search starts searching
    /// the current page instead of the board, quietly answering "no ideas match" for ideas
    /// that are two pages down. Paging means reading the leading count from the table and
    /// moving the search into the query, in that order.
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
        detail.Comments = await LoadCommentsAsync(ideaId, cancellationToken);

        User signedInUser = await currentUser.GetAsync(cancellationToken);

        detail.HasVoted = await dbContext.Votes
            .AsNoTracking()
            .AnyAsync(vote => vote.IdeaId == ideaId && vote.UserId == signedInUser.Id, cancellationToken);

        return detail;
    }


    /// <summary>
    /// Reads the comments on one idea, oldest first, in a single query.
    ///
    /// Whether each one belongs to the person looking is decided here, from the identifier
    /// already in hand, rather than by asking the database once per comment. It is the same
    /// shape as the vote state on the lists, and the same thing goes wrong if it is written
    /// the obvious way: the page looks identical and only the number of commands differs.
    /// </summary>
    private async Task<IReadOnlyList<IdeaComment>> LoadCommentsAsync(
        int ideaId,
        CancellationToken cancellationToken)
    {
        User signedInUser = await currentUser.GetAsync(cancellationToken);

        return await dbContext.Comments
            .AsNoTracking()
            .Where(comment => comment.IdeaId == ideaId)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .Select(comment => new IdeaComment
            {
                Id = comment.Id,
                AuthorDisplayName = comment.Author!.DisplayName,
                Text = comment.Text,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                IsMine = comment.AuthorUserId == signedInUser.Id,
            })
            .ToListAsync(cancellationToken);
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
