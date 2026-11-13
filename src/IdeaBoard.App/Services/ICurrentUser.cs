using IdeaBoard.App.Domain;

namespace IdeaBoard.App.Services;

/// <summary>
/// The person the current request belongs to, as a local <see cref="User"/> row.
///
/// This is a convenience over <c>HttpContext.User</c> and never an alternative to it.
/// Authorisation, the fallback policy and the sign-in UI all work off the principal the
/// authentication pipeline produced, and a second, parallel notion of identity would be a
/// way for those two to disagree.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Resolves the local user row for the current request, or null when the signed-in
    /// person has no row yet because no name could be obtained for them.
    ///
    /// Null is a normal answer and not a failure. Only two callers ever see it: the gate
    /// that sends such a person to register a name, and the registration page itself.
    /// </summary>
    Task<User?> TryGetAsync(CancellationToken cancellationToken);


    /// <summary>
    /// Resolves the local user row, refusing to continue when there is none.
    ///
    /// This is what every page calls, and it is allowed to be this blunt because those
    /// pages are only reachable past the gate. Having the common case return a row that
    /// cannot be null is the point: the alternative pushes a null check into every page
    /// that renders an author or a voter, permanently, to describe a state that lasts from
    /// somebody's first request until they type their name.
    /// </summary>
    Task<User> GetAsync(CancellationToken cancellationToken);
}
