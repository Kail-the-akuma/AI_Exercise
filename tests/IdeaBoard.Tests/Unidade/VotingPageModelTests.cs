using IdeaBoard.App.Pages;
using IdeaBoard.App.Services;
using Xunit;

namespace IdeaBoard.Tests.Unidade;

/// <summary>
/// The decision of what the vote button shows next, taken on the server. The interface is
/// only allowed to display numbers and words the server produced, so everything the script
/// writes to the screen has to come out of this one method.
/// </summary>
public class VotingPageModelTests
{
    #region Helpers

    /// <summary>Builds the state a vote operation would have returned.</summary>
    private static VoteState BuildState(int ideaId, bool hasVoted, int voteCount)
    {
        return new VoteState
        {
            IdeaId = ideaId,
            HasVoted = hasVoted,
            VoteCount = voteCount,
        };
    }

    #endregion


    #region Button state

    /// <summary>
    /// Docs/02: someone who has not voted yet sees "Votar", and the button records a vote.
    /// </summary>
    [Fact]
    public void SomeoneWhoHasNotVotedIsOfferedToVote()
    {
        VoteResponse response = VotingPageModel.BuildVoteResponse(BuildState(7, false, 3));

        Assert.Equal("Votar", response.ButtonText);
        Assert.Equal("Vote", response.NextHandler);
        Assert.False(response.HasVoted);
    }


    /// <summary>
    /// Docs/02: someone who has voted sees "Votado", and the same button withdraws the vote.
    /// </summary>
    [Fact]
    public void SomeoneWhoHasVotedIsOfferedToWithdraw()
    {
        VoteResponse response = VotingPageModel.BuildVoteResponse(BuildState(7, true, 4));

        Assert.Equal("Votado", response.ButtonText);
        Assert.Equal("Unvote", response.NextHandler);
        Assert.True(response.HasVoted);
    }


    [Fact]
    public void TheHandlerNamesMatchTheHandlersThatExist()
    {
        Assert.Equal("Vote", VotingPageModel.VoteHandlerName);
        Assert.Equal("Unvote", VotingPageModel.UnvoteHandlerName);
    }

    #endregion


    #region Carried state

    [Theory]
    [InlineData(1, true, 0)]
    [InlineData(42, false, 1)]
    [InlineData(99, true, 17)]
    public void TheStateTheDatabaseProducedIsCarriedThroughUnchanged(int ideaId, bool hasVoted, int voteCount)
    {
        VoteResponse response = VotingPageModel.BuildVoteResponse(BuildState(ideaId, hasVoted, voteCount));

        Assert.Equal(ideaId, response.IdeaId);
        Assert.Equal(hasVoted, response.HasVoted);
        Assert.Equal(voteCount, response.VoteCount);
    }

    #endregion


    #region Wording of the count

    /// <summary>
    /// The wording travels with the number. If the script had to word the count itself it
    /// would hold a second copy of the interface text, one step away from computing the
    /// number too.
    /// </summary>
    [Theory]
    [InlineData(0, "Sem votos")]
    [InlineData(1, "1 voto")]
    [InlineData(2, "2 votos")]
    [InlineData(31, "31 votos")]
    public void TheCountArrivesAlreadyWorded(int voteCount, string expected)
    {
        VoteResponse response = VotingPageModel.BuildVoteResponse(BuildState(5, false, voteCount));

        Assert.Equal(expected, response.VoteCountText);
        Assert.Equal(UiText.FormatVoteCount(voteCount), response.VoteCountText);
    }

    #endregion


    #region Arguments

    [Fact]
    public void AnAbsentStateIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => VotingPageModel.BuildVoteResponse(null!));
    }

    #endregion
}
