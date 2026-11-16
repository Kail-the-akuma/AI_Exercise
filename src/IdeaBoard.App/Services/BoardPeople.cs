using IdeaBoard.App.Data;
using Microsoft.EntityFrameworkCore;

namespace IdeaBoard.App.Services;

/// <summary>
/// Counts the people the board knows about, over the request's database context.
/// </summary>
public class BoardPeople : IBoardPeople
{
    #region Fields

    private readonly IdeaBoardDbContext dbContext;

    #endregion


    #region Construction

    /// <summary>Creates the service over the request-scoped context.</summary>
    public BoardPeople(IdeaBoardDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        this.dbContext = dbContext;
    }

    #endregion


    #region Counting

    /// <inheritdoc />
    public async Task<int> CountArrivedAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .CountAsync(cancellationToken);
    }

    #endregion
}
