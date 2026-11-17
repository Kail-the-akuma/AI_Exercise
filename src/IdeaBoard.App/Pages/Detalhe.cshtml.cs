using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdeaBoard.App.Pages;

/// <summary>
/// One idea in full, with the public list of everybody who voted for it and what people
/// said about it. The names are there on purpose: transparency is a decision of the
/// briefing, not an oversight.
/// </summary>
public class DetalheModel : VotingPageModel
{
    #region Constants

    /// <summary>Name of the query string value that carries the comment being edited.</summary>
    public const string EditingQueryParameter = "editar";

    /// <summary>Name of the query string value that carries the comment being removed.</summary>
    public const string DeletingQueryParameter = "apagar";

    /// <summary>Where a reload lands after something happened to the comments.</summary>
    private const string CommentsAnchor = "comentarios";

    #endregion


    #region Fields

    private readonly IIdeaService ideaService;
    private readonly ICommentService commentService;

    #endregion


    #region Construction

    /// <summary>Creates the page over the idea, vote and comment services.</summary>
    public DetalheModel(
        IIdeaService ideaService,
        IVoteService voteService,
        ICommentService commentService)
        : base(voteService)
    {
        ArgumentNullException.ThrowIfNull(ideaService);
        ArgumentNullException.ThrowIfNull(commentService);

        this.ideaService = ideaService;
        this.commentService = commentService;
    }

    #endregion


    #region State

    /// <summary>
    /// The idea being shown. It is only ever read by the view after the handler has found
    /// it, because an identifier that matches nothing answers with a not found instead of
    /// rendering the page.
    /// </summary>
    public IdeaDetail Idea { get; private set; } = null!;

    /// <summary>What is in the comment box, bound from the form.</summary>
    [BindProperty]
    public string? Text { get; set; }

    /// <summary>
    /// Which comment is open for editing, carried in the address.
    ///
    /// It is in the address and not in a hidden field because editing has to work without
    /// JavaScript, which means the way into the edit form is an ordinary link. Putting it
    /// there also means the open form survives a reload and is left by pressing back, which
    /// is what a person expects of a link.
    ///
    /// It says nothing about what MAY be edited. Opening the form over somebody else's
    /// comment is possible and useless, because the submission behind it is refused.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = EditingQueryParameter)]
    public int? EditingCommentId { get; set; }

    /// <summary>
    /// Which comment is waiting to be confirmed for removal, carried in the address.
    ///
    /// Removal has a step in front of it because it cannot be undone and there is no
    /// scripting here to put a dialogue in the way. The step is a link and then a form, so
    /// it works with scripting off like everything else, and nothing is removed by a
    /// request the browser made on its own: the link only changes what is drawn.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = DeletingQueryParameter)]
    public int? DeletingCommentId { get; set; }

    #endregion


    #region Handlers

    /// <summary>Reads one idea, or answers 404 when no idea has that identifier.</summary>
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        return await ShowAsync(id, cancellationToken);
    }


    /// <summary>Adds a comment to this idea.</summary>
    public async Task<IActionResult> OnPostCommentAsync(int id, CancellationToken cancellationToken)
    {
        CommentSubmissionResult submission = CommentSubmission.Validate(Text);

        if (!submission.IsValid)
        {
            AddErrors(submission);

            return await ShowAsync(id, cancellationToken);
        }

        await commentService.AddAsync(id, submission.Text, cancellationToken);

        return RedirectToPage("/Detalhe", null, new { id }, CommentsAnchor);
    }


    /// <summary>
    /// Replaces the text of a comment this person wrote.
    ///
    /// The identifier arrives from the form and is not trusted for a moment. What decides
    /// the answer is the service, which looks the comment up by identifier AND by author in
    /// one query, so a request naming somebody else's comment finds nothing to change
    /// rather than finding something and then being told not to.
    /// </summary>
    public async Task<IActionResult> OnPostEditCommentAsync(
        int id,
        int commentId,
        CancellationToken cancellationToken)
    {
        CommentSubmissionResult submission = CommentSubmission.Validate(Text);

        if (!submission.IsValid)
        {
            AddErrors(submission);

            // The form stays open over the comment that was being edited, so the message
            // appears beside the box it is about rather than at the foot of the page.
            EditingCommentId = commentId;

            return await ShowAsync(id, cancellationToken);
        }

        CommentChangeOutcome outcome = await commentService.EditAsync(
            commentId,
            submission.Text,
            cancellationToken);

        return AnswerFor(outcome, id);
    }


    /// <summary>Removes a comment this person wrote, leaving nothing in its place.</summary>
    public async Task<IActionResult> OnPostDeleteCommentAsync(
        int id,
        int commentId,
        CancellationToken cancellationToken)
    {
        CommentChangeOutcome outcome = await commentService.DeleteAsync(commentId, cancellationToken);

        return AnswerFor(outcome, id);
    }

    #endregion


    #region Responses

    /// <summary>Loads the idea and renders the page, or answers 404.</summary>
    private async Task<IActionResult> ShowAsync(int id, CancellationToken cancellationToken)
    {
        IdeaDetail? detail = await ideaService.GetDetailAsync(id, cancellationToken);

        if (detail is null)
        {
            return NotFound();
        }

        Idea = detail;

        return Page();
    }


    /// <summary>
    /// Turns the service's answer into a response.
    ///
    /// A refusal is a status code and not a message on a rendered page, because nothing a
    /// person can do on this page produces one. Reaching here means the request did not
    /// come from the page as it was drawn, and the honest answer to that is the code that
    /// says so rather than a sentence pretending it was an ordinary mistake.
    /// </summary>
    private IActionResult AnswerFor(CommentChangeOutcome outcome, int ideaId)
    {
        switch (outcome)
        {
            case CommentChangeOutcome.Done:
            {
                return RedirectToPage("/Detalhe", null, new { id = ideaId }, CommentsAnchor);
            }

            case CommentChangeOutcome.NotYours:
            {
                return Forbid();
            }

            case CommentChangeOutcome.NotFound:
            {
                return NotFound();
            }

            default:
            {
                throw new InvalidOperationException(
                    $"The comment service answered with {outcome}, which this page does not handle.");
            }
        }
    }


    /// <summary>Copies the validation problems onto the model state, keyed by field.</summary>
    private void AddErrors(CommentSubmissionResult submission)
    {
        foreach (KeyValuePair<string, string> error in submission.Errors)
        {
            ModelState.AddModelError(error.Key, error.Value);
        }
    }


    /// <inheritdoc />
    protected override IActionResult RedirectAfterVote(int ideaId)
    {
        return RedirectToPage("/Detalhe", null, new { id = ideaId }, BuildIdeaAnchor(ideaId));
    }

    #endregion
}
