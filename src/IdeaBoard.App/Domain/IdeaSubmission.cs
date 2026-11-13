namespace IdeaBoard.App.Domain;

/// <summary>
/// The rules a submitted idea has to satisfy, with no dependency on the web layer or on
/// the database. Server-side validation is the one that counts: the browser check is a
/// convenience, and a submission that bypasses it has to be rejected just the same.
/// </summary>
public static class IdeaSubmission
{
    #region Messages

    /// <summary>Shown when the title is empty or made only of whitespace.</summary>
    public const string MissingTitleMessage = "Indique um título.";

    /// <summary>Shown when the trimmed title is longer than the limit.</summary>
    public const string TitleTooLongMessage = "O título não pode ultrapassar 120 caracteres.";

    /// <summary>Shown when the description is empty or made only of whitespace.</summary>
    public const string MissingDescriptionMessage = "Descreva a ideia.";

    /// <summary>Shown when the trimmed description is longer than the limit.</summary>
    public const string DescriptionTooLongMessage = "A descrição não pode ultrapassar 2000 caracteres.";

    #endregion


    #region Validation

    /// <summary>
    /// Trims leading and trailing whitespace, then validates. Trimming happens before the
    /// check on purpose, which is what makes a title of nothing but spaces an empty title.
    /// </summary>
    public static IdeaSubmissionResult Validate(string? title, string? description)
    {
        string trimmedTitle = Trim(title);
        string trimmedDescription = Trim(description);

        Dictionary<string, string> errors = new Dictionary<string, string>(StringComparer.Ordinal);

        ValidateTitle(trimmedTitle, errors);
        ValidateDescription(trimmedDescription, errors);

        return new IdeaSubmissionResult(trimmedTitle, trimmedDescription, errors);
    }


    /// <summary>Records the first title problem found, so each field shows one message.</summary>
    private static void ValidateTitle(string title, Dictionary<string, string> errors)
    {
        if (title.Length == 0)
        {
            errors.Add(IdeaLimits.TitleField, MissingTitleMessage);
            return;
        }

        if (title.Length > IdeaLimits.TitleMaximumLength)
        {
            errors.Add(IdeaLimits.TitleField, TitleTooLongMessage);
        }
    }


    /// <summary>Records the first description problem found.</summary>
    private static void ValidateDescription(string description, Dictionary<string, string> errors)
    {
        if (description.Length == 0)
        {
            errors.Add(IdeaLimits.DescriptionField, MissingDescriptionMessage);
            return;
        }

        if (description.Length > IdeaLimits.DescriptionMaximumLength)
        {
            errors.Add(IdeaLimits.DescriptionField, DescriptionTooLongMessage);
        }
    }


    /// <summary>Trims a value that may be absent.</summary>
    private static string Trim(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value.Trim();
    }

    #endregion
}
