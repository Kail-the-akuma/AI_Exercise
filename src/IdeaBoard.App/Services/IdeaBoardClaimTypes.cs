namespace IdeaBoard.App.Services;

/// <summary>
/// The claim types identity arrives under, defined once and read by everything that
/// touches a principal.
///
/// Two forms of these claims exist in the wild: the short OpenID Connect names, and the
/// long schemas.microsoft.com URIs that inbound claim mapping rewrites them into. Which
/// one reaches the principal depends on a host setting whose default has changed between
/// versions, so no component is allowed to write a claim type by hand and hope.
///
/// The short form is the one this application standardises on. The published host turns
/// inbound claim mapping off explicitly so the short form is what arrives, and the long
/// form is still read as a fallback, because a silently renamed claim would show up as
/// every sign-in creating a brand new person.
/// </summary>
public static class IdeaBoardClaimTypes
{
    #region Canonical forms

    /// <summary>Entra ID object identifier, the key a person is matched by.</summary>
    public const string ObjectId = "oid";

    /// <summary>Name shown as author and in the list of voters.</summary>
    public const string DisplayName = "name";

    /// <summary>Stands in for the email, and is what Entra ID issues as the username.</summary>
    public const string Email = "preferred_username";

    /// <summary>
    /// User principal name. Read as a fallback for the email because preferred_username
    /// only exists in version 2.0 tokens: against a version 1.0 authority it never
    /// arrives, and this is what does.
    /// </summary>
    public const string Upn = "upn";

    /// <summary>Email address, the other short form a version 1.0 token may carry.</summary>
    public const string EmailAddress = "email";

    /// <summary>
    /// Token version, which the token declares outright. Worth reading rather than
    /// inferring from which other claims happen to be missing.
    /// </summary>
    public const string TokenVersion = "ver";

    /// <summary>
    /// Issuer. Only its shape is ever recorded, never its value, because it carries the
    /// tenant identifier.
    /// </summary>
    public const string Issuer = "iss";

    #endregion


    #region Legacy forms

    /// <summary>Object identifier as inbound claim mapping rewrites it.</summary>
    public const string MappedObjectId = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <summary>Name as inbound claim mapping rewrites it.</summary>
    public const string MappedDisplayName = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

    /// <summary>Email as inbound claim mapping rewrites it.</summary>
    public const string MappedEmail = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

    /// <summary>User principal name, read only as a last resort for the email.</summary>
    public const string MappedUpn = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn";

    #endregion
}
