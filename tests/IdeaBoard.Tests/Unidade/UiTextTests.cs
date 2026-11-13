using System.Net;
using System.Text.Encodings.Web;
using IdeaBoard.App.Pages;
using Xunit;

namespace IdeaBoard.Tests.Unidade;

/// <summary>
/// The three forms of the vote count fixed by the wording table in Docs/02. The server is
/// the only place allowed to word a count, so this is where the wording is pinned down.
/// </summary>
public class UiTextTests
{
    #region Counts

    [Fact]
    public void NoVotesIsWordedWithoutANumber()
    {
        Assert.Equal("Sem votos", UiText.FormatVoteCount(0));
    }


    [Fact]
    public void OneVoteUsesTheSingular()
    {
        Assert.Equal("1 voto", UiText.FormatVoteCount(1));
    }


    [Theory]
    [InlineData(2, "2 votos")]
    [InlineData(3, "3 votos")]
    [InlineData(11, "11 votos")]
    [InlineData(142, "142 votos")]
    [InlineData(1000, "1000 votos")]
    public void MoreThanOneVoteUsesThePlural(int voteCount, string expected)
    {
        Assert.Equal(expected, UiText.FormatVoteCount(voteCount));
    }


    /// <summary>
    /// A count below zero cannot come out of the database, where the count is an aggregate
    /// of rows. It is treated as no votes rather than rendered as "-1 votos", which is the
    /// only one of the two that could reach a screen without looking like a defect.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(-7)]
    [InlineData(int.MinValue)]
    public void ANegativeCountIsWordedAsNoVotes(int voteCount)
    {
        Assert.Equal("Sem votos", UiText.FormatVoteCount(voteCount));
    }

    #endregion


    #region Wording

    [Fact]
    public void ButtonLabelsMatchTheFunctionalSpecification()
    {
        Assert.Equal("Votar", UiText.VoteButton);
        Assert.Equal("Votado", UiText.VotedButton);
    }

    #endregion


    #region How these strings travel

    /// <summary>
    /// An accented string does NOT travel to the browser as written, and a test that
    /// compares one against a raw response body is comparing against something that was
    /// never there.
    ///
    /// This is measured here rather than taken on trust, because the failure mode is
    /// asymmetric and nasty. A POSITIVE assertion fails loudly and sends somebody hunting
    /// for a defect in server-side validation that does not exist. A NEGATIVE assertion
    /// does not fail at all: it passes while checking nothing, and goes on passing even if
    /// the content it was meant to exclude were served in full.
    ///
    /// The encoder used here is the one Razor applies to an expression written with @.
    /// </summary>
    [Fact]
    public void AnAccentedStringIsEncodedAsEntitiesOnTheWayOut()
    {
        string encoded = HtmlEncoder.Default.Encode(UiText.EmptyList);

        Assert.NotEqual(UiText.EmptyList, encoded);
        Assert.DoesNotContain(UiText.EmptyList, encoded, StringComparison.Ordinal);
        Assert.Contains("&#x", encoded, StringComparison.Ordinal);

        // And decoding recovers it, which is what makes checking the decoded form a
        // valid way to assert against a response body.
        Assert.Equal(UiText.EmptyList, WebUtility.HtmlDecode(encoded));
    }


    /// <summary>
    /// The strings that happen to be pure ASCII travel unchanged, which is exactly why a
    /// group of assertions mixing the two kinds can look healthy while half of it is
    /// doing nothing.
    /// </summary>
    [Theory]
    [InlineData("Mais votadas")]
    [InlineData("Recentes")]
    [InlineData("Submeter ideia")]
    [InlineData("Votar")]
    public void APlainStringTravelsUnchanged(string text)
    {
        Assert.Equal(text, HtmlEncoder.Default.Encode(text));
    }

    #endregion
}
