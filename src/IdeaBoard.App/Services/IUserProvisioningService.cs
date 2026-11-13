using System.Security.Claims;
using IdeaBoard.App.Domain;

namespace IdeaBoard.App.Services;

/// <summary>
/// Keeps the local <see cref="User"/> table in step with the people who sign in. It exists
/// because the public list of voters needs the names of third parties, and the claims of
/// the current token only ever describe whoever is signed in right now.
/// </summary>
public interface IUserProvisioningService
{
    /// <summary>Provisions the signed-in person from the claims of a validated token.</summary>
    Task<User> EnsureUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);


    /// <summary>
    /// Creates the person on the first sign-in, and refreshes the name and email on the
    /// following ones. Matching is by Entra object identifier and never by email.
    /// </summary>
    Task<User> EnsureUserAsync(
        string entraObjectId,
        string displayName,
        string email,
        CancellationToken cancellationToken);


    /// <summary>Reads a person by Entra object identifier, without tracking.</summary>
    Task<User?> FindByEntraObjectIdAsync(string entraObjectId, CancellationToken cancellationToken);
}
