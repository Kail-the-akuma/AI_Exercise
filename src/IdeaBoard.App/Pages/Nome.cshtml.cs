using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdeaBoard.App.Pages;

/// <summary>
/// Asks somebody for their name, when no name could be obtained for them.
///
/// This is production behaviour and not a development convenience, which is why it lives
/// here and not in the host that is never published. A directory can be unreachable on the
/// server exactly as it can on a machine outside the domain, and a published application
/// without this page would simply refuse entry to anybody who arrived while it was down.
///
/// Nothing on this page escapes or sanitises anything. Razor encodes output by default and
/// the one field here is rendered through it like any other. Adding encoding by hand to a
/// free text field shown publicly is how a defect gets introduced while fixing one that did
/// not exist.
/// </summary>
public class NomeModel : PageModel
{
    #region Fields

    private readonly IUserProvisioningService userProvisioningService;

    #endregion


    #region Construction

    /// <summary>Creates the page over the provisioning service.</summary>
    public NomeModel(IUserProvisioningService userProvisioningService)
    {
        ArgumentNullException.ThrowIfNull(userProvisioningService);

        this.userProvisioningService = userProvisioningService;
    }

    #endregion


    #region State

    /// <summary>
    /// The name as typed. The property name is what ties the field to the key the domain
    /// reports its problem under, so the message lands next to the input.
    /// </summary>
    [BindProperty]
    public string? DisplayName { get; set; }

    #endregion


    #region Handlers

    /// <summary>
    /// Shows the form, unless the person already has a name, in which case there is
    /// nothing to ask and they are sent on.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (await HasRowAsync(cancellationToken))
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }


    /// <summary>
    /// Validates on the server, which is the validation that counts, and provisions the
    /// person with the name they gave.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        DisplayNameValidationResult validation = DisplayNameRules.Validate(DisplayName);

        if (!validation.IsValid)
        {
            ModelState.AddModelError(DisplayNameRules.FieldName, validation.Error!);

            return Page();
        }

        // Deliberately not guarded by a prior check for an existing row. Two submissions,
        // two tabs, or a directory that started answering since the form was shown, all end
        // here, and the service treats a second insert of the same person as the success it
        // already is, through the same unique index the vote path relies on.
        await userProvisioningService.RegisterAsync(User, validation.DisplayName, cancellationToken);

        return RedirectToPage("/Index");
    }

    #endregion


    #region Lookup

    /// <summary>
    /// Says whether this person already has a row.
    ///
    /// It reads by subject identifier rather than going through the current user, on
    /// purpose. The current user would attempt provisioning, and provisioning is what
    /// consults the directory: simply opening this page would then pay the directory
    /// timeout again, every time, for the person least able to afford it.
    /// </summary>
    private async Task<bool> HasRowAsync(CancellationToken cancellationToken)
    {
        string subjectId = userProvisioningService.ReadSubjectId(User);

        User? existing = await userProvisioningService.FindBySubjectIdAsync(subjectId, cancellationToken);

        return existing is not null;
    }

    #endregion
}
