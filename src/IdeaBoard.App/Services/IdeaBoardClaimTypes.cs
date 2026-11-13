namespace IdeaBoard.App.Services;

/// <summary>
/// The claim types identity arrives under, defined once and read by everything that
/// touches a principal.
///
/// Keeping them in one file is not tidiness. Identity moved from Entra ID to Windows
/// authentication, and every constant here changed with it. Because they were all in one
/// place, that review was one file rather than a hunt through the application, and nothing
/// was left pointing at a claim that no longer arrives.
///
/// Windows authentication is what the published host uses. Its claims are the long schemas
/// URIs, which here are not a legacy form and not something a setting rewrites: they are
/// what the handler emits. The short OpenID Connect names are still read, because the
/// hosts that are never published sign in with them, and because a claim silently not
/// found would show up as every sign-in creating a brand new person.
/// </summary>
public static class IdeaBoardClaimTypes
{
    #region Windows forms

    /// <summary>
    /// Security identifier of the signed-in account, and the key a person is matched by.
    ///
    /// It arrives in the request itself and costs no lookup, which is what makes the
    /// directory a dependency for the display name only and never for identity. The
    /// application keeps working whole with the directory unreachable, and that is why
    /// asking somebody for their name is an acceptable answer rather than a pretence.
    /// </summary>
    public const string PrimarySid = "http://schemas.microsoft.com/ws/2008/06/identity/claims/primarysid";

    /// <summary>
    /// The name claim Windows authentication emits. It carries the ACCOUNT name, in its
    /// DOMAIN\someone form, and not a name fit to show anybody. That gap is the whole
    /// reason a display name has to be resolved or asked for.
    /// </summary>
    public const string WindowsAccountName = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

    #endregion


    #region OpenID Connect forms

    /// <summary>
    /// Object identifier, read as a subject identifier after the security identifier. The
    /// hosts that are never published sign in with this, so it stays read.
    /// </summary>
    public const string ObjectId = "oid";

    /// <summary>Name fit to show, when the sign-in already carries one.</summary>
    public const string DisplayName = "name";

    /// <summary>
    /// Object identifier in the form inbound claim mapping produces.
    ///
    /// NOTHING EMITS THIS ANY MORE, and that is said here so that nobody reading the
    /// list it sits in assumes otherwise. It is read as a third fallback for the subject
    /// identifier, behind two forms that are emitted, so nothing can exercise it. It
    /// stays because removing a fallback can only reduce what the application tolerates
    /// and costs nothing, but it is not load bearing and a reader should not treat its
    /// presence as evidence of anything.
    /// </summary>
    public const string MappedObjectId = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <summary>
    /// Username. Nothing in the application reads this any more: the column it used to
    /// fill was replaced by the account name, which arrives without a directory lookup.
    /// It stays defined because the hosts that are never published still emit it, and
    /// because a claim type belongs in this file even when only they use it.
    /// </summary>
    public const string Email = "preferred_username";

    #endregion


    #region Diagnostics

    /// <summary>
    /// Token version, declared by the sign-in itself. Only ever written to the log, and
    /// only so that an identity carrying unexpected claims is diagnosable without a
    /// debugger and without a diagnostic page in the published application.
    /// </summary>
    public const string TokenVersion = "ver";

    /// <summary>
    /// Issuer. Only its shape is ever recorded, never its value, because it carries the
    /// tenant identifier.
    /// </summary>
    public const string Issuer = "iss";

    #endregion
}
