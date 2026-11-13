namespace IdeaBoard.App.Services;

/// <summary>
/// Turns the account the request arrived under into a name fit to show other people.
///
/// This exists because Windows authentication delivers an account name such as
/// DOMAIN\someone, which is an identifier and not a name. The briefing requires the
/// person's name to be visible as the author of an idea and in the public list of voters,
/// so something has to bridge the two, and that something can fail.
/// </summary>
public interface IDisplayNameResolver
{
    /// <summary>
    /// Looks a name up, and NEVER throws.
    ///
    /// That is the whole contract and it is not a nicety. This runs on somebody's very
    /// first request, before they have seen a single page. A directory that is unreachable,
    /// an account that cannot be read, a lookup that hangs: every one of those has to come
    /// back as "no name" and let the request continue, because the alternative is an error
    /// page as a person's first and only impression of the application.
    ///
    /// Implementations are therefore expected to swallow their own failures and to log
    /// them. A caller that wraps this in a try block has misread the contract.
    /// </summary>
    Task<DisplayNameResolution> ResolveAsync(string accountIdentifier, CancellationToken cancellationToken);
}
