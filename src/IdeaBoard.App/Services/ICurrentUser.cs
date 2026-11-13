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
    /// <summary>Resolves the local user row for the current request.</summary>
    Task<User> GetAsync(CancellationToken cancellationToken);
}
