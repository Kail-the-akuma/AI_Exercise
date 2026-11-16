namespace IdeaBoard.App.Pages;

/// <summary>
/// What the server answers a vote with, and everything the interface is allowed to show
/// afterwards.
///
/// It carries the rendered strings and not only the numbers, on purpose. If the script had
/// to turn a count into "1 voto" or "Sem votos" itself, it would hold a second copy of the
/// interface wording, and it would be one step away from working out the number too. With
/// the text arriving ready, the script has nothing left to compute: it writes what the
/// server produced, and a count the server did not produce cannot reach the screen.
///
/// The same rule covers the two states the stylesheet needs, which arrive as answers rather
/// than as the values they were decided from. What the script puts on screen is decided
/// here, wording and state alike, and the script chooses nothing.
/// </summary>
public class VoteResponse
{
    /// <summary>The idea this state belongs to.</summary>
    public int IdeaId { get; set; }

    /// <summary>Whether the person has a vote on it right now.</summary>
    public bool HasVoted { get; set; }

    /// <summary>Votes the idea has right now, as the database reported them.</summary>
    public int VoteCount { get; set; }

    /// <summary>
    /// Whether the idea is to be shown as having no support at all.
    ///
    /// It travels as an answer and not as the count it is read from, for the same reason
    /// the wording does. The threshold belongs to one place, and that place is the server:
    /// a script that worked it out from the count would be a second opinion on what counts
    /// as none, free to disagree with the partial the next time either one is touched.
    /// </summary>
    public bool HasNoVotes { get; set; }

    /// <summary>The count already worded, for example "Sem votos" or "3 votos".</summary>
    public string VoteCountText { get; set; } = string.Empty;

    /// <summary>The label the button should now carry, "Votar" or "Votado".</summary>
    public string ButtonText { get; set; } = string.Empty;

    /// <summary>
    /// The handler the form should post to next. The button carries an explicit intent
    /// rather than toggling, so that a repeated submission asks for the same end state.
    /// </summary>
    public string NextHandler { get; set; } = string.Empty;
}
