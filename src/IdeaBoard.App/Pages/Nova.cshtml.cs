using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdeaBoard.App.Pages;

/// <summary>
/// Submission of a new idea.
///
/// This page does not inherit the voting page model, because there is no vote button on
/// it: an idea that does not exist yet cannot be voted on.
/// </summary>
public class NovaModel : PageModel
{
    #region Fields

    private readonly IIdeaService ideaService;

    #endregion


    #region Construction

    /// <summary>Creates the page over the idea service.</summary>
    public NovaModel(IIdeaService ideaService)
    {
        ArgumentNullException.ThrowIfNull(ideaService);

        this.ideaService = ideaService;
    }

    #endregion


    #region State

    /// <summary>
    /// Title as typed. The property name is what ties the field to the error key the
    /// domain reports under, so that the message lands next to its own input.
    /// </summary>
    [BindProperty]
    public string? Title { get; set; }

    /// <summary>Description as typed, several lines of plain text.</summary>
    [BindProperty]
    public string? Description { get; set; }

    #endregion


    #region Handlers

    /// <summary>Shows the empty form.</summary>
    public void OnGet()
    {
    }


    /// <summary>
    /// Validates on the server, which is the validation that counts, and stores the idea.
    /// The values are left exactly as they were typed when validation fails, so that
    /// nothing written is lost on the way back.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        IdeaSubmissionResult submission = IdeaSubmission.Validate(Title, Description);

        if (!submission.IsValid)
        {
            AddErrors(submission);

            return Page();
        }

        Idea idea = await ideaService.SubmitAsync(
            submission.Title,
            submission.Description,
            cancellationToken);

        // Whoever just wrote an idea wants to see it, and a brand new idea has no votes,
        // so it would be nowhere near the top of the ranking.
        return RedirectToPage("/Detalhe", new { id = idea.Id });
    }

    #endregion


    #region Validation

    /// <summary>Copies the domain's problems into the model state, keyed by field.</summary>
    private void AddErrors(IdeaSubmissionResult submission)
    {
        foreach (KeyValuePair<string, string> error in submission.Errors)
        {
            ModelState.AddModelError(error.Key, error.Value);
        }
    }

    #endregion
}
