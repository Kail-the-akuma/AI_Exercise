using System.Security.Claims;
using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IdeaBoard.App.Services;

/// <summary>
/// How many votes the current person's ideas have received since they last looked.
/// </summary>
public class NewVoteNotice
{
    /// <summary>Votes received since the marker, across all of this person's ideas.</summary>
    public int VoteCount { get; set; }

    /// <summary>How many of their ideas those votes are spread across.</summary>
    public int IdeaCount { get; set; }

    /// <summary>Votes received since the marker, by idea. Ideas with none are absent.</summary>
    public IReadOnlyDictionary<int, int> VoteCountByIdea { get; set; } =
        new Dictionary<int, int>();

    /// <summary>Whether there is anything at all to show.</summary>
    public bool HasAnything
    {
        get
        {
            return VoteCount > 0;
        }
    }
}


/// <summary>
/// The one notice this product has: somebody voted on an idea you proposed.
///
/// It is built from rows that already exist, a vote with an author and a date, plus one
/// marker per person saying how far they have read. Nothing new is stored about the
/// relationship between a person and an idea beyond the vote itself, which is the line the
/// product does not cross.
/// </summary>
public interface INewVoteNotices
{
    /// <summary>What the current person has not seen yet.</summary>
    Task<NewVoteNotice> ReadAsync(CancellationToken cancellationToken);


    /// <summary>
    /// Moves this person's marker up to now, and reports whether it actually wrote.
    ///
    /// The answer matters to the caller, because this is called from a GET and the caller
    /// has to be able to say "nothing was written" for the overwhelming majority of visits.
    /// </summary>
    Task<bool> MarkSeenAsync(CancellationToken cancellationToken);
}


/// <summary>Reads and clears the notice over the request's database context.</summary>
public class NewVoteNotices : INewVoteNotices
{
    #region Fields

    private readonly IdeaBoardDbContext dbContext;
    private readonly ICurrentUser currentUser;
    private readonly TimeProvider timeProvider;
    private readonly IHttpContextAccessor httpContextAccessor;

    #endregion


    #region Construction

    /// <summary>Creates the service over the request-scoped context.</summary>
    public NewVoteNotices(
        IdeaBoardDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        this.dbContext = dbContext;
        this.currentUser = currentUser;
        this.timeProvider = timeProvider;
        this.httpContextAccessor = httpContextAccessor;
    }

    #endregion


    #region Reading

    /// <inheritdoc />
    public async Task<NewVoteNotice> ReadAsync(CancellationToken cancellationToken)
    {
        // Nothing to say about nobody, and this is not defensive habit. The notice is drawn
        // in the layout, which means it is also drawn on the page that asks somebody to
        // register a name and on the error page, and the error page is where a refused
        // request lands. The same mistake made here once already turned a working refusal
        // into a 500, in this very branch, caught by an end to end scenario.
        User? person = await TryGetPersonAsync(cancellationToken);

        if (person is null)
        {
            return new NewVoteNotice();
        }

        List<NewVoteRow> rows = await QueryNewVotes(person)
            .Select(vote => new NewVoteRow { IdeaId = vote.IdeaId })
            .ToListAsync(cancellationToken);

        Dictionary<int, int> countByIdea = new Dictionary<int, int>();

        foreach (NewVoteRow row in rows)
        {
            countByIdea.TryGetValue(row.IdeaId, out int soFar);
            countByIdea[row.IdeaId] = soFar + 1;
        }

        return new NewVoteNotice
        {
            VoteCount = rows.Count,
            IdeaCount = countByIdea.Count,
            VoteCountByIdea = countByIdea,
        };
    }


    /// <summary>
    /// The votes this person has not seen: cast on an idea they wrote, after their marker,
    /// and not cast by them.
    ///
    /// Their own vote is left out deliberately. Telling somebody that their idea received a
    /// vote they themselves just cast is a notice about an action they took a second ago,
    /// and a notice that reports the reader back to themselves is noise the first time and
    /// a reason to stop reading notices the second.
    ///
    /// A marker of null means they have never looked, so everything counts. That is not a
    /// missing value treated as zero: it is the true answer for somebody who has not been
    /// here.
    /// </summary>
    private IQueryable<Vote> QueryNewVotes(User person)
    {
        IQueryable<Vote> votes = dbContext.Votes
            .AsNoTracking()
            .Where(vote => vote.Idea!.AuthorUserId == person.Id)
            .Where(vote => vote.UserId != person.Id);

        if (person.LastNotificationsSeenAt is null)
        {
            return votes;
        }

        DateTime seenAt = person.LastNotificationsSeenAt.Value;

        return votes.Where(vote => vote.CreatedAt > seenAt);
    }


    /// <summary>
    /// The current person, or null when there is nobody: an unauthenticated request, or
    /// somebody authenticated who has no row yet.
    /// </summary>
    private async Task<User?> TryGetPersonAsync(CancellationToken cancellationToken)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return null;
        }

        ClaimsPrincipal principal = httpContext.User;

        if (principal.Identity is null || !principal.Identity.IsAuthenticated)
        {
            return null;
        }

        return await currentUser.TryGetAsync(cancellationToken);
    }


    /// <summary>One unseen vote, reduced to the idea it was cast on.</summary>
    private sealed class NewVoteRow
    {
        /// <summary>The idea the vote belongs to.</summary>
        public int IdeaId { get; set; }
    }

    #endregion


    #region Clearing

    /// <inheritdoc />
    public async Task<bool> MarkSeenAsync(CancellationToken cancellationToken)
    {
        User person = await currentUser.GetAsync(cancellationToken);

        // The write is conditional, and this is the condition. A page that wrote on every
        // visit would write once per person per visit forever, to record a fact that has
        // not changed since the last time, and it would do it inside a GET. Asking first
        // costs one query that this page was already going to pay for the notice itself.
        bool anythingToClear = await QueryNewVotes(person).AnyAsync(cancellationToken);

        if (!anythingToClear)
        {
            return false;
        }

        // The row has to be tracked to be updated, and the one the current user handed over
        // may have come from a no tracking read.
        User tracked = await dbContext.Users
            .FirstAsync(candidate => candidate.Id == person.Id, cancellationToken);

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        tracked.LastNotificationsSeenAt = now;
        person.LastNotificationsSeenAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion
}
