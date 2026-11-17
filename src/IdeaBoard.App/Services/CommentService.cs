using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using Microsoft.EntityFrameworkCore;

namespace IdeaBoard.App.Services;

/// <summary>
/// What happened to a request to change or remove a comment.
///
/// It is an answer and not an exception, because none of these is a fault in the program.
/// A comment that is gone and a comment that belongs to somebody else are both ordinary
/// things for a request to be about, and the page turns each into the status code that
/// says so.
/// </summary>
public enum CommentChangeOutcome
{
    /// <summary>The comment was changed or removed.</summary>
    Done,

    /// <summary>No comment has that identifier, on that idea.</summary>
    NotFound,

    /// <summary>
    /// The comment exists and belongs to somebody else, so nothing was done to it.
    ///
    /// REACHING THIS IS NOT AN ERROR AND NOT AN ATTACK, NECESSARILY. A page left open after
    /// somebody else edited, a stale tab, a bookmarked address: all of them land here. What
    /// matters is that they land here instead of succeeding.
    /// </summary>
    NotYours,
}


/// <summary>Writing, changing and removing comments on an idea.</summary>
public interface ICommentService
{
    /// <summary>
    /// Adds a comment by the current person. The text must already be trimmed and valid,
    /// which the caller establishes with <see cref="CommentSubmission.Validate"/>.
    /// </summary>
    Task<Comment> AddAsync(int ideaId, string text, CancellationToken cancellationToken);


    /// <summary>
    /// Replaces the text of a comment, if it exists and the current person wrote it.
    ///
    /// THE OWNERSHIP CHECK IS HERE AND NOT IN THE PAGE, and not in whether a button was
    /// drawn. Not showing somebody a control is a convenience for them; refusing the
    /// request is the thing that actually stops anybody. The two are easy to confuse
    /// because on screen they look identical.
    /// </summary>
    Task<CommentChangeOutcome> EditAsync(int commentId, string text, CancellationToken cancellationToken);


    /// <summary>
    /// Removes a comment, if it exists and the current person wrote it.
    ///
    /// Removal leaves nothing behind. There is no tombstone and no "comment removed" line,
    /// because a visible gap is a statement about a person made in front of everybody else,
    /// and this product has no moderation and no triage to make such a statement inside.
    /// </summary>
    Task<CommentChangeOutcome> DeleteAsync(int commentId, CancellationToken cancellationToken);
}


/// <summary>Comments over the single database context.</summary>
public class CommentService : ICommentService
{
    #region Fields

    private readonly IdeaBoardDbContext dbContext;
    private readonly ICurrentUser currentUser;
    private readonly TimeProvider timeProvider;

    #endregion


    #region Construction

    /// <summary>Creates the service over the request-scoped context.</summary>
    public CommentService(
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


    #region Writing

    /// <inheritdoc />
    public async Task<Comment> AddAsync(int ideaId, string text, CancellationToken cancellationToken)
    {
        CommentSubmissionResult validation = CommentSubmission.Validate(text);

        if (!validation.IsValid)
        {
            throw new ArgumentException(
                "The comment was rejected by domain validation. Validate before calling this method.",
                nameof(text));
        }

        User author = await currentUser.GetAsync(cancellationToken);

        Comment comment = new Comment
        {
            IdeaId = ideaId,
            AuthorUserId = author.Id,
            Text = validation.Text,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return comment;
    }

    #endregion


    #region Changing

    /// <inheritdoc />
    public async Task<CommentChangeOutcome> EditAsync(
        int commentId,
        string text,
        CancellationToken cancellationToken)
    {
        CommentSubmissionResult validation = CommentSubmission.Validate(text);

        if (!validation.IsValid)
        {
            throw new ArgumentException(
                "The comment was rejected by domain validation. Validate before calling this method.",
                nameof(text));
        }

        Comment? comment = await FindMineAsync(commentId, cancellationToken);

        if (comment is null)
        {
            return await ClassifyRefusalAsync(commentId, cancellationToken);
        }

        comment.Text = validation.Text;
        comment.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await dbContext.SaveChangesAsync(cancellationToken);

        return CommentChangeOutcome.Done;
    }


    /// <inheritdoc />
    public async Task<CommentChangeOutcome> DeleteAsync(int commentId, CancellationToken cancellationToken)
    {
        Comment? comment = await FindMineAsync(commentId, cancellationToken);

        if (comment is null)
        {
            return await ClassifyRefusalAsync(commentId, cancellationToken);
        }

        dbContext.Comments.Remove(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CommentChangeOutcome.Done;
    }


    /// <summary>
    /// The comment with that identifier, but only if the current person wrote it.
    ///
    /// The ownership is part of the query and not a check made afterwards on a row that was
    /// already loaded. A row fetched first and judged second is one edit away from being
    /// fetched, judged, and used anyway, and the shape that cannot make that mistake is the
    /// one where the wrong row never arrives.
    /// </summary>
    private async Task<Comment?> FindMineAsync(int commentId, CancellationToken cancellationToken)
    {
        User person = await currentUser.GetAsync(cancellationToken);

        return await dbContext.Comments
            .FirstOrDefaultAsync(
                candidate => candidate.Id == commentId && candidate.AuthorUserId == person.Id,
                cancellationToken);
    }


    /// <summary>
    /// Why the comment could not be changed: it is not there at all, or it is somebody
    /// else's.
    ///
    /// This runs only on the path that is already refusing, so the extra query costs
    /// nothing on the ordinary one. Telling the two apart is worth a query because the two
    /// answers are different things to say to the person and different things to find in a
    /// log afterwards.
    /// </summary>
    private async Task<CommentChangeOutcome> ClassifyRefusalAsync(
        int commentId,
        CancellationToken cancellationToken)
    {
        bool existsAtAll = await dbContext.Comments
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == commentId, cancellationToken);

        if (existsAtAll)
        {
            return CommentChangeOutcome.NotYours;
        }

        return CommentChangeOutcome.NotFound;
    }

    #endregion
}
