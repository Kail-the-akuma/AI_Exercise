namespace IdeaBoard.E2E.Fixtures;

/// <summary>
/// The people the local host is configured with, named here so a scenario can say who it
/// is acting as without repeating an identifier.
///
/// These values come from the local host's own appsettings, which is deliberate: the suite
/// signs in the way a person does, through the picker's cookie, rather than through a
/// mechanism built for testing. There is no test authentication handler anywhere in this
/// solution and there is not meant to be one.
/// </summary>
public static class People
{
    #region Identifiers

    /// <summary>First configured person.</summary>
    public const string AnaObjectId = "11111111-1111-1111-1111-111111111111";

    /// <summary>Name the first person is shown as.</summary>
    public const string AnaDisplayName = "Ana Ribeiro";

    /// <summary>Second configured person.</summary>
    public const string BrunoObjectId = "22222222-2222-2222-2222-222222222222";

    /// <summary>Name the second person is shown as.</summary>
    public const string BrunoDisplayName = "Bruno Carvalho";

    /// <summary>An identifier no configured person has.</summary>
    public const string UnknownObjectId = "99999999-9999-9999-9999-999999999999";

    #endregion
}
