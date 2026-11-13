namespace IdeaBoard.App.Domain;

/// <summary>
/// Size limits for a submitted idea, and the field names errors are attached to.
///
/// SQLite does not enforce column lengths, so these are the limits that actually apply.
/// The model configuration declares them as well, because that is what documents them and
/// what would take effect if the database engine ever changed.
/// </summary>
public static class IdeaLimits
{
    #region Lengths

    /// <summary>Longest accepted title, after trimming.</summary>
    public const int TitleMaximumLength = 120;

    /// <summary>Longest accepted description, after trimming.</summary>
    public const int DescriptionMaximumLength = 2000;

    #endregion


    #region Field names

    /// <summary>Key errors about the title are reported under.</summary>
    public const string TitleField = "Title";

    /// <summary>Key errors about the description are reported under.</summary>
    public const string DescriptionField = "Description";

    #endregion
}
