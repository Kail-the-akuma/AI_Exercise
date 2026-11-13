using IdeaBoard.App.Domain;
using Xunit;

namespace IdeaBoard.Tests.Unidade;

/// <summary>
/// What a typed name is allowed to be.
///
/// The case that matters most here is not length. It is that somebody can type anything
/// into this field and have it shown to the whole team beside their ideas, permanently,
/// because nothing in this product edits or removes.
/// </summary>
public class DisplayNameRulesTests
{
    #region Presence

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ANameThatIsNothingButSpaceIsMissing(string? typed)
    {
        DisplayNameValidationResult result = DisplayNameRules.Validate(typed);

        Assert.False(result.IsValid);
        Assert.Equal(DisplayNameRules.MissingMessage, result.Error);
    }


    [Fact]
    public void SurroundingSpaceIsTrimmedBeforeAnythingElse()
    {
        DisplayNameValidationResult result = DisplayNameRules.Validate("   Ana Dias   ");

        Assert.True(result.IsValid);
        Assert.Equal("Ana Dias", result.DisplayName);
    }

    #endregion


    #region Length

    [Fact]
    public void ANameAtTheLimitIsAccepted()
    {
        string name = new string('a', DisplayNameRules.MaximumLength);

        DisplayNameValidationResult result = DisplayNameRules.Validate(name);

        Assert.True(result.IsValid);
    }


    [Fact]
    public void ANameOneCharacterPastTheLimitIsRefused()
    {
        string name = new string('a', DisplayNameRules.MaximumLength + 1);

        DisplayNameValidationResult result = DisplayNameRules.Validate(name);

        Assert.False(result.IsValid);
        Assert.Equal(DisplayNameRules.TooLongMessage, result.Error);
    }


    /// <summary>
    /// Trimming happens before the length is counted, so trailing space cannot push an
    /// acceptable name over the limit.
    /// </summary>
    [Fact]
    public void SpaceDoesNotCountTowardsTheLimit()
    {
        string name = new string('a', DisplayNameRules.MaximumLength) + "     ";

        DisplayNameValidationResult result = DisplayNameRules.Validate(name);

        Assert.True(result.IsValid);
    }


    /// <summary>The message names the same number the rule enforces.</summary>
    [Fact]
    public void TheMessageAndTheLimitAgree()
    {
        Assert.Contains(
            DisplayNameRules.MaximumLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DisplayNameRules.TooLongMessage,
            StringComparison.Ordinal);
    }

    #endregion


    #region Impersonation

    /// <summary>
    /// A name shaped like an account identifier is refused with a message, not an
    /// exception. This field is free text shown publicly, so without this somebody could
    /// register a name that reads as an account identifier and appear in the list of voters
    /// as though they were one.
    /// </summary>
    [Theory]
    [InlineData("EXEMPLO\\ana")]
    [InlineData("Ana\\Dias")]
    [InlineData("\\")]
    [InlineData("DOMINIO\\administrador")]
    public void ANameShapedLikeAnAccountIsRefusedWithAMessage(string typed)
    {
        DisplayNameValidationResult result = DisplayNameRules.Validate(typed);

        Assert.False(result.IsValid);
        Assert.Equal(DisplayNameRules.LooksLikeAccountMessage, result.Error);
    }


    /// <summary>
    /// An at sign is NOT refused in a display name. Only the account field cares about it,
    /// and refusing it here would turn a rule about impersonation into a rule about
    /// punctuation.
    /// </summary>
    [Fact]
    public void AnAtSignIsAcceptedInANameEvenThoughAnAccountMayCarryOne()
    {
        DisplayNameValidationResult result = DisplayNameRules.Validate("Ana @ Dias");

        Assert.True(result.IsValid);
    }


    [Theory]
    [InlineData("Ana Dias")]
    [InlineData("Ana Ribeiro de Sousa e Castro")]
    [InlineData("Zoë Lüders")]
    [InlineData("Ana-Maria O'Brien")]
    public void OrdinaryNamesAreAccepted(string typed)
    {
        DisplayNameValidationResult result = DisplayNameRules.Validate(typed);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    #endregion


    #region Account shape

    /// <summary>
    /// Both legitimate account formats are recognised. The user principal form is the one a
    /// rule written only around the backslash would have refused, provisioning nobody.
    /// </summary>
    [Theory]
    [InlineData("EXEMPLO\\ana")]
    [InlineData("DESKTOP-U62SCE9\\Hugo")]
    [InlineData("ana@exemplo.test")]
    [InlineData("ana.dias@exemplo.local")]
    public void BothAccountFormatsAreRecognised(string accountName)
    {
        Assert.True(DisplayNameRules.HasAccountNameShape(accountName));
    }


    [Theory]
    [InlineData("Ana Dias")]
    [InlineData("EXEMPLO\\sub\\ana")]
    [InlineData("ana@@exemplo.test")]
    [InlineData("EXEMPLO\\ana@exemplo.test")]
    [InlineData("EXEMPLO\\")]
    [InlineData("@exemplo.test")]
    [InlineData("")]
    public void AnythingElseIsNotAnAccountName(string value)
    {
        Assert.False(DisplayNameRules.HasAccountNameShape(value));
    }

    #endregion
}
