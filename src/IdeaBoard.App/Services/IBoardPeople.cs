namespace IdeaBoard.App.Services;

/// <summary>
/// How many people the board knows about.
///
/// It is deliberately not called a headcount and not called the size of the team. A row
/// only appears in the user table when somebody opens the application for the first time
/// AND a name is known for them, so what this counts is arrivals and never the size of the
/// organisation. A colleague who has never opened the application is not here, and the
/// number goes up as more of them do.
///
/// That distinction has to survive contact with the interface, which is why it is written
/// here as well as next to the number: a caption saying "of 8 people" is read as "everybody"
/// unless the words around it say otherwise, and the words are the only thing standing
/// between this count and a false claim about who has seen an idea.
/// </summary>
public interface IBoardPeople
{
    /// <summary>
    /// How many people have arrived at the board so far, counted at read time and never
    /// stored, so it cannot drift from the rows it counts.
    /// </summary>
    Task<int> CountArrivedAsync(CancellationToken cancellationToken);
}
