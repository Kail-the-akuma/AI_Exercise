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


    #region Support bar

    /// <summary>
    /// Said once above the list, because the bar is drawn against the idea leading the
    /// same list and nothing else on screen says so. On the ranking the leader is the
    /// first row and its full bar is its own explanation; on the recent tab the leader is
    /// somewhere down the page and it is not.
    /// </summary>
    public const string SupportBarLegend =
        "A barra mostra o apoio de cada ideia face à mais votada.";

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


    #region Search

    /// <summary>
    /// Names the search box. The search is a plain form submitted with GET, so this label
    /// is what a screen reader reads out for the field, and the address afterwards is the
    /// whole state of the search.
    /// </summary>
    public const string SearchLabel = "Procurar por título ou por quem propôs";

    /// <summary>The button that runs the search.</summary>
    public const string SearchButton = "Procurar";

    /// <summary>The button that drops the term and goes back to the whole board.</summary>
    public const string ClearSearchButton = "Limpar";

    /// <summary>
    /// Shown instead of the list when a search matched nothing.
    ///
    /// It is a different sentence from the empty board on purpose. Telling somebody whose
    /// search found nothing that there are no ideas yet, and inviting them to be the first
    /// to propose one, is false on a board with two hundred ideas in it.
    /// </summary>
    public const string NoSearchResults =
        "Nenhuma ideia corresponde a esta procura.";

    #endregion


    #region Personal area

    /// <summary>Names the third tab, and the entry to it in the layout.</summary>
    public const string PersonalTab = "As minhas ideias e votos";

    /// <summary>Heading of the list of ideas the person proposed.</summary>
    public const string MyIdeasHeading = "Ideias que propus";

    /// <summary>Heading of the list of ideas the person voted on.</summary>
    public const string SupportedIdeasHeading = "Ideias que apoiei";

    /// <summary>
    /// Shown when the person has proposed nothing. It is kept apart from the sentence
    /// below because on the first day everybody is in both states at once, and one
    /// sentence could not say which.
    /// </summary>
    public const string NoIdeasSubmitted = "Ainda não propôs nenhuma ideia.";

    /// <summary>Shown when the person has voted on nothing.</summary>
    public const string NoIdeasSupported = "Ainda não apoiou nenhuma ideia.";

    /// <summary>Names the handful of latest voters shown on a row.</summary>
    public const string RecentVotesLabel = "Últimos votos";


    /// <summary>
    /// How many votes one idea received since this person last looked. Shown on their own
    /// ideas and nowhere else.
    /// </summary>
    public static string FormatNewVotes(int voteCount)
    {
        if (voteCount == 1)
        {
            return "1 voto novo";
        }

        return string.Format(CultureInfo.CurrentCulture, "{0} votos novos", voteCount);
    }


    /// <summary>
    /// How many of this person's ideas received votes they have not seen, which is what the
    /// entry in the layout carries.
    /// </summary>
    public static string FormatIdeasWithNewVotes(int ideaCount)
    {
        if (ideaCount == 1)
        {
            return "1 ideia sua recebeu votos novos";
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            "{0} ideias suas receberam votos novos",
            ideaCount);
    }

    #endregion


    #region Comments

    /// <summary>Heads the conversation under an idea.</summary>
    public const string CommentsHeading = "Comentários";

    /// <summary>Shown when nobody has said anything about this idea yet.</summary>
    public const string NoComments = "Ainda ninguém comentou esta ideia.";

    /// <summary>Names the box a comment is written in.</summary>
    public const string CommentLabel = "Comentário";

    /// <summary>The button that adds a comment.</summary>
    public const string AddCommentButton = "Comentar";

    /// <summary>The link that opens one of your own comments for changing.</summary>
    public const string EditCommentLink = "Editar";

    /// <summary>The button that stores a changed comment.</summary>
    public const string SaveCommentButton = "Guardar";

    /// <summary>The link that leaves a comment as it was.</summary>
    public const string CancelEditLink = "Cancelar";

    /// <summary>The link that asks whether to remove one of your own comments.</summary>
    public const string DeleteCommentLink = "Apagar";

    /// <summary>
    /// Asked before a comment is removed.
    ///
    /// There is a step here, rather than a button that removes on the first press, because
    /// removal cannot be undone and this application has no scripting to put a dialogue in
    /// the way. The same reasoning already warns somebody before they submit an idea.
    /// </summary>
    public const string ConfirmDeleteComment = "Apagar este comentário? Não é possível recuperá-lo.";

    /// <summary>The button that goes through with the removal.</summary>
    public const string ConfirmDeleteCommentButton = "Apagar";

    /// <summary>Marks a comment that was changed after it was written.</summary>
    public const string EditedMarker = "editado";

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


    /// <summary>
    /// How many people have arrived at the board, said in a way that cannot be read as the
    /// size of the team.
    ///
    /// The number counts people who have opened the application at least once, so a
    /// caption reading "of 8 people" beside a count of votes would quietly claim that eight
    /// is everybody. The wording says what the eight are.
    /// </summary>
    public static string FormatArrivedPeople(int peopleCount)
    {
        if (peopleCount == 1)
        {
            return "de 1 pessoa no quadro";
        }

        return string.Format(CultureInfo.CurrentCulture, "de {0} pessoas no quadro", peopleCount);
    }

    #endregion
}
