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
}
