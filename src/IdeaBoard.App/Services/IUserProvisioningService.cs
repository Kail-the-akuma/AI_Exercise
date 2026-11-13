using System.Security.Claims;
using IdeaBoard.App.Domain;

namespace IdeaBoard.App.Services;

/// <summary>
/// Keeps the local <see cref="User"/> table in step with the people who sign in. It exists
/// because the public list of voters needs the names of third parties, and the claims of
/// the current sign-in only ever describe whoever is signed in right now.
///
/// A row is written only once there is a name to write. That is why the first method here
/// can answer "nobody yet" and the second one exists at all.
/// </summary>
public interface IUserProvisioningService
{
    /// <summary>
    /// Provisions the signed-in person, or reports that they have no name yet.
    ///
    /// Returns null when no name could be obtained, from a claim or from the directory, and
    /// no row was therefore written. The caller sends such a person to register a name.
    /// Null is a normal outcome here and not a failure.
    /// </summary>
    Task<User?> TryEnsureUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);


    /// <summary>
    /// Provisions the signed-in person with a name they supplied themselves, and records
    /// that origin so no later directory lookup overwrites it.
    ///
    /// Idempotent. Submitting the form twice, or in two tabs, returns the row that already
    /// exists rather than failing, in the same way a repeated vote does.
    /// </summary>
    Task<User> RegisterAsync(ClaimsPrincipal principal, string displayName, CancellationToken cancellationToken);


    /// <summary>
    /// Creates the person on first sight, and refreshes them afterwards. Matching is by
    /// subject identifier, never by anything derived from a name.
    /// </summary>
    Task<User> EnsureUserAsync(
        string subjectId,
        string displayName,
        string accountName,
        DisplayNameSource displayNameSource,
        CancellationToken cancellationToken);


    /// <summary>Reads a person by subject identifier, without tracking.</summary>
    Task<User?> FindBySubjectIdAsync(string subjectId, CancellationToken cancellationToken);


    /// <summary>
    /// Reads the subject identifier off a principal, refusing anything that does not have
    /// the shape of an identifier. Exposed so that a caller can look somebody up without
    /// provisioning them, and above all without triggering a directory lookup.
    /// </summary>
    string ReadSubjectId(ClaimsPrincipal principal);
}
