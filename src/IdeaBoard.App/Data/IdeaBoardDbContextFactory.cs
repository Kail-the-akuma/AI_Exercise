using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IdeaBoard.App.Data;

/// <summary>
/// Builds a context for the EF Core command line tools only, never at run time.
///
/// Without it, adding a migration would mean starting one of the hosts, which validates
/// configuration and refuses to start against a database that is not provisioned. Writing
/// a migration would then depend on having a provisioned database, which is backwards: the
/// migration is what provisions it.
/// </summary>
public class IdeaBoardDbContextFactory : IDesignTimeDbContextFactory<IdeaBoardDbContext>
{
    #region Design time

    /// <summary>
    /// Creates a context pointed at a throwaway file. The tools only need the model, and
    /// no command used in this repository connects to it.
    /// </summary>
    public IdeaBoardDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<IdeaBoardDbContext> optionsBuilder =
            new DbContextOptionsBuilder<IdeaBoardDbContext>();

        optionsBuilder.UseSqlite("Data Source=ideaboard-design-time.db");

        return new IdeaBoardDbContext(optionsBuilder.Options);
    }

    #endregion
}
