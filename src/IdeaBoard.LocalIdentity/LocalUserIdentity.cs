using Microsoft.AspNetCore.Http;

namespace IdeaBoard.LocalIdentity;

/// <summary>
/// One identity the local authentication handler can present, carrying exactly three short
/// OpenID Connect claims. They are not the shape the published host receives: see the
/// handler for what differs and what that does and does not cover.
/// </summary>
public class LocalUserIdentity
{
    /// <summary>Value of the <c>oid</c> claim.</summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>Value of the <c>name</c> claim.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Value of the <c>preferred_username</c> claim.</summary>
    public string Email { get; set; } = string.Empty;
}


/// <summary>
/// Where the local handler gets the identity for a request.
///
/// The handler takes no decisions of its own, which is what makes it shareable between the
/// local host and the end to end suite. One supplies the identity from configuration and a
/// person picker, the other from a request header, and neither behaviour belongs in the
/// handler itself.
/// </summary>
public interface ILocalIdentitySource
{
    /// <summary>
    /// Returns the identity for this request, or null when there is none. Null is a real
    /// answer and never a reason to invent somebody.
    /// </summary>
    LocalUserIdentity? Resolve(HttpContext httpContext);
}
