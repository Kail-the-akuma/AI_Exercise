using IdeaBoard.LocalIdentity;

namespace IdeaBoard.Local;

/// <summary>People this local host can sign in as, read from configuration.</summary>
public class LocalPeopleOptions
{
    /// <summary>Configuration section these options are bound from.</summary>
    public const string SectionName = "LocalPeople";

    /// <summary>The people on offer. An empty list means nobody can sign in.</summary>
    public List<LocalPerson> People { get; set; } = [];
}


/// <summary>One person on offer, with the three claim values the handler will emit.</summary>
public class LocalPerson
{
    /// <summary>Stands in for the Entra object identifier.</summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>Name shown as author and in the list of voters.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Stands in for the preferred username.</summary>
    public string Email { get; set; } = string.Empty;
}
