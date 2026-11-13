using IdeaBoard.App.Domain;
using Xunit;

namespace IdeaBoard.Tests.Unidade;

/// <summary>
/// The submission rules of Docs/02, checked in isolation: trimming before validating, the
/// two length limits at their exact boundary, and one message per field.
/// </summary>
public class IdeaSubmissionTests
{
    #region Helpers

    /// <summary>Builds a run of one repeated character, to sit on a length boundary.</summary>
    private static string BuildText(int length)
    {
        return new string('a', length);
    }


    /// <summary>
    /// Reads the message recorded for a field, or an empty string when the field carries
    /// none. Returning instead of throwing keeps a missing error readable in the failure.
    /// </summary>
    private static string ReadError(IdeaSubmissionResult result, string field)
    {
        string? message;

        if (!result.Errors.TryGetValue(field, out message))
        {
            return string.Empty;
        }

        return message;
    }

    #endregion


    #region Accepted submissions

    [Fact]
    public void ValidTitleAndDescriptionAreAccepted()
    {
        IdeaSubmissionResult result = IdeaSubmission.Validate("Uma ideia", "Uma descricao qualquer.");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal("Uma ideia", result.Title);
        Assert.Equal("Uma descricao qualquer.", result.Description);
    }


    [Fact]
    public void TitleOfExactlyTheMaximumLengthIsAccepted()
    {
        string title = BuildText(IdeaLimits.TitleMaximumLength);

        IdeaSubmissionResult result = IdeaSubmission.Validate(title, "Uma descricao.");

        Assert.True(result.IsValid);
        Assert.Equal(120, result.Title.Length);
    }


    [Fact]
    public void DescriptionOfExactlyTheMaximumLengthIsAccepted()
    {
        string description = BuildText(IdeaLimits.DescriptionMaximumLength);

        IdeaSubmissionResult result = IdeaSubmission.Validate("Uma ideia", description);

        Assert.True(result.IsValid);
        Assert.Equal(2000, result.Description.Length);
    }

    #endregion


    #region Missing values

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \t\r\n ")]
    public void TitleThatIsAbsentEmptyOrOnlyWhitespaceIsMissing(string? title)
    {
        IdeaSubmissionResult result = IdeaSubmission.Validate(title, "Uma descricao.");

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(IdeaSubmission.MissingTitleMessage, ReadError(result, IdeaLimits.TitleField));
        Assert.Equal(string.Empty, result.Title);
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \t\r\n ")]
    public void DescriptionThatIsAbsentEmptyOrOnlyWhitespaceIsMissing(string? description)
    {
        IdeaSubmissionResult result = IdeaSubmission.Validate("Uma ideia", description);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(IdeaSubmission.MissingDescriptionMessage, ReadError(result, IdeaLimits.DescriptionField));
        Assert.Equal(string.Empty, result.Description);
    }


    [Fact]
    public void BothFieldsMissingAreBothReported()
    {
        IdeaSubmissionResult result = IdeaSubmission.Validate("   ", null);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(IdeaSubmission.MissingTitleMessage, ReadError(result, IdeaLimits.TitleField));
        Assert.Equal(IdeaSubmission.MissingDescriptionMessage, ReadError(result, IdeaLimits.DescriptionField));
    }

    #endregion


    #region Length limits

    [Fact]
    public void TitleOneCharacterOverTheMaximumIsRejected()
    {
        string title = BuildText(IdeaLimits.TitleMaximumLength + 1);

        IdeaSubmissionResult result = IdeaSubmission.Validate(title, "Uma descricao.");

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(IdeaSubmission.TitleTooLongMessage, ReadError(result, IdeaLimits.TitleField));
    }


    [Fact]
    public void DescriptionOneCharacterOverTheMaximumIsRejected()
    {
        string description = BuildText(IdeaLimits.DescriptionMaximumLength + 1);

        IdeaSubmissionResult result = IdeaSubmission.Validate("Uma ideia", description);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(IdeaSubmission.DescriptionTooLongMessage, ReadError(result, IdeaLimits.DescriptionField));
    }


    [Fact]
    public void BothFieldsOverTheMaximumAreBothReported()
    {
        string title = BuildText(IdeaLimits.TitleMaximumLength + 1);
        string description = BuildText(IdeaLimits.DescriptionMaximumLength + 1);

        IdeaSubmissionResult result = IdeaSubmission.Validate(title, description);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(IdeaSubmission.TitleTooLongMessage, ReadError(result, IdeaLimits.TitleField));
        Assert.Equal(IdeaSubmission.DescriptionTooLongMessage, ReadError(result, IdeaLimits.DescriptionField));
    }


    [Fact]
    public void EmptyTitleIsReportedAsMissingAndNotAsTooLong()
    {
        IdeaSubmissionResult result = IdeaSubmission.Validate("     ", "Uma descricao.");

        Assert.Equal(IdeaSubmission.MissingTitleMessage, ReadError(result, IdeaLimits.TitleField));
    }

    #endregion


    #region Trimming

    [Fact]
    public void SurroundingWhitespaceIsRemovedFromTheReturnedValues()
    {
        IdeaSubmissionResult result = IdeaSubmission.Validate("  Uma ideia  ", "\n  Uma descricao.  \t");

        Assert.True(result.IsValid);
        Assert.Equal("Uma ideia", result.Title);
        Assert.Equal("Uma descricao.", result.Description);
    }


    [Fact]
    public void WhitespaceFollowedByRealTextIsTrimmedAndAccepted()
    {
        IdeaSubmissionResult result = IdeaSubmission.Validate("      Uma ideia", "      Uma descricao.");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal("Uma ideia", result.Title);
        Assert.Equal("Uma descricao.", result.Description);
    }


    [Fact]
    public void InnerWhitespaceIsKept()
    {
        IdeaSubmissionResult result = IdeaSubmission.Validate("  Uma  ideia  longa  ", "  Linha um.\nLinha dois.  ");

        Assert.True(result.IsValid);
        Assert.Equal("Uma  ideia  longa", result.Title);
        Assert.Equal("Linha um.\nLinha dois.", result.Description);
    }


    /// <summary>
    /// The case the limit hangs on: the raw value is over the limit and the trimmed value is
    /// not. Docs/02 says trimming happens before validating, so this has to be accepted, and
    /// the stored title has to be the trimmed one of exactly 120 characters.
    /// </summary>
    [Fact]
    public void TitleOverTheMaximumOnlyBecauseOfSurroundingWhitespaceIsAccepted()
    {
        string title = "    " + BuildText(IdeaLimits.TitleMaximumLength) + "    ";

        IdeaSubmissionResult result = IdeaSubmission.Validate(title, "Uma descricao.");

        Assert.Equal(128, title.Length);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(IdeaLimits.TitleMaximumLength, result.Title.Length);
    }


    [Fact]
    public void DescriptionOverTheMaximumOnlyBecauseOfSurroundingWhitespaceIsAccepted()
    {
        string description = "  \n" + BuildText(IdeaLimits.DescriptionMaximumLength) + "\n  ";

        IdeaSubmissionResult result = IdeaSubmission.Validate("Uma ideia", description);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(IdeaLimits.DescriptionMaximumLength, result.Description.Length);
    }


    /// <summary>
    /// The mirror image: whitespace cannot rescue a value that is still over the limit once
    /// trimmed, because trimming only ever shortens.
    /// </summary>
    [Fact]
    public void TitleStillOverTheMaximumAfterTrimmingIsRejected()
    {
        string title = "    " + BuildText(IdeaLimits.TitleMaximumLength + 1) + "    ";

        IdeaSubmissionResult result = IdeaSubmission.Validate(title, "Uma descricao.");

        Assert.False(result.IsValid);
        Assert.Equal(IdeaSubmission.TitleTooLongMessage, ReadError(result, IdeaLimits.TitleField));
        Assert.Equal(IdeaLimits.TitleMaximumLength + 1, result.Title.Length);
    }

    #endregion


    #region Wording

    /// <summary>
    /// The messages are fixed by the table in Docs/02, so they are checked as literal text
    /// and not only through the constants that carry them.
    /// </summary>
    [Fact]
    public void MessagesMatchTheFunctionalSpecification()
    {
        Assert.Equal("Indique um título.", IdeaSubmission.MissingTitleMessage);
        Assert.Equal("O título não pode ultrapassar 120 caracteres.", IdeaSubmission.TitleTooLongMessage);
        Assert.Equal("Descreva a ideia.", IdeaSubmission.MissingDescriptionMessage);
        Assert.Equal("A descrição não pode ultrapassar 2000 caracteres.", IdeaSubmission.DescriptionTooLongMessage);
    }


    [Fact]
    public void ErrorsAreKeyedByTheFieldNamesTheFormUses()
    {
        IdeaSubmissionResult result = IdeaSubmission.Validate(null, null);

        Assert.Equal("Title", IdeaLimits.TitleField);
        Assert.Equal("Description", IdeaLimits.DescriptionField);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(IdeaSubmission.MissingTitleMessage, ReadError(result, "Title"));
        Assert.Equal(IdeaSubmission.MissingDescriptionMessage, ReadError(result, "Description"));
    }

    #endregion
}
