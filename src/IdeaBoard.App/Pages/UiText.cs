using System.Globalization;

namespace IdeaBoard.App.Pages;

/// <summary>
/// The interface strings fixed by the functional specification, kept in one place so that
/// they can be checked against that document without reading every page.
///
/// European Portuguese, no emoji. These are the only user-facing strings the application
/// is allowed to show for the elements listed in the specification.
/// </summary>
public static class UiText
{
    #region Navigation

    /// <summary>Main tab, the ranking.</summary>
    public const string RankingTab = "Mais votadas";

    /// <summary>Second tab, ordered by submission date.</summary>
    public const string RecentTab = "Recentes";

    /// <summary>Button that opens the submission form.</summary>
    public const string SubmitIdeaButton = "Submeter ideia";

    #endregion


    #region Voting

    /// <summary>Vote button before voting.</summary>
    public const string VoteButton = "Votar";

    /// <summary>Vote button once the person has voted.</summary>
    public const string VotedButton = "Votado";

    /// <summary>Shown next to the vote button, before the person votes.</summary>
    public const string PublicVoteWarning = "O seu voto é público.";

    #endregion


    #region Submission

    /// <summary>Shown next to the submit button, before the person submits.</summary>
    public const string PermanenceWarning =
        "Depois de submeter, a ideia não pode ser editada nem removida.";

    #endregion


    #region Empty state

    /// <summary>Shown instead of an empty list, on the first day and after.</summary>
    public const string EmptyList = "Ainda não há ideias. Seja a primeira pessoa a propor uma.";

    #endregion


    #region Counts

    /// <summary>
    /// Renders a vote count in words, with the three forms the specification fixes: none,
    /// exactly one, and many.
    /// </summary>
    public static string FormatVoteCount(int voteCount)
    {
        if (voteCount <= 0)
        {
            return "Sem votos";
        }

        if (voteCount == 1)
        {
            return "1 voto";
        }

        return string.Format(CultureInfo.CurrentCulture, "{0} votos", voteCount);
    }

    #endregion
}
