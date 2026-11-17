namespace IdeaBoard.App.Domain;

/// <summary>
/// Size limit for a comment, and the field name errors are attached to.
///
/// It mirrors <see cref="IdeaLimits"/> rather than inventing a second way of saying the
/// same thing, down to the detail that matters most: SQLite does not enforce column
/// lengths, so the limit that actually applies is this one and not the one declared in the
/// model configuration.
/// </summary>
public static class CommentLimits
{
    #region Lengths

    /// <summary>
    /// Longest accepted comment, after trimming.
    ///
    /// The same as an idea's description on purpose. A comment that can be longer than the
    /// idea it is about inverts the page, and picking a different number would be a second
    /// limit to explain with no reason behind it.
    /// </summary>
    public const int TextMaximumLength = 2000;

    #endregion


    #region Field names

    /// <summary>Key errors about the text are reported under.</summary>
    public const string TextField = "Text";

    #endregion
}
