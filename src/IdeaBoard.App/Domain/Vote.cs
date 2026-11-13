namespace IdeaBoard.App.Domain;

/// <summary>
/// One person's support for one idea.
///
/// Withdrawing a vote deletes the row. There is no active flag and no soft delete, because
/// nothing in the product needs the history of withdrawn votes, and keeping it would be
/// collecting data about colleagues with no stated purpose.
/// </summary>
public class Vote
{
    /// <summary>Surrogate key, assigned by the database.</summary>
    public int Id { get; set; }

    /// <summary>The idea being supported.</summary>
    public int IdeaId { get; set; }

    /// <summary>Navigation to the idea.</summary>
    public Idea? Idea { get; set; }

    /// <summary>The person voting.</summary>
    public int UserId { get; set; }

    /// <summary>Navigation to the voter, used to render the public list of names.</summary>
    public User? User { get; set; }

    /// <summary>Instant of the vote, in UTC. It orders the public list of voters.</summary>
    public DateTime CreatedAt { get; set; }
}
