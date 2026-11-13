using IdeaBoard.App.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdeaBoard.E2E.Fixtures;

/// <summary>
/// Owns the application under test for the whole run: a throwaway SQLite file, the host
/// running as its own process, and the state reset that runs before each scenario.
/// </summary>
public class AppHostFixture : IAsyncLifetime
{
    #region Fields

    private string temporaryFolder = string.Empty;

    private string connectionString = string.Empty;

    private ApplicationProcess? application;

    #endregion


    #region Properties

    /// <summary>Address the browser navigates to.</summary>
    public string BaseAddress
    {
        get
        {
            if (application is null)
            {
                throw new InvalidOperationException("The fixture has not been initialised.");
            }

            return application.BaseAddress;
        }
    }

    #endregion


    #region Lifetime

    /// <summary>
    /// Provisions the database and starts the host, in that order.
    ///
    /// The order is not arbitrary. The application verifies at startup that the file is in
    /// write-ahead logging mode and refuses to run otherwise, so the database has to exist
    /// and be provisioned before the host is started.
    ///
    /// No pragma is applied here. EF Core creates its databases in WAL already, and this
    /// suite depends on that rather than assuming it: a host that started at all is a host
    /// whose WAL check passed, so a run that gets anywhere has already proved it. Applying
    /// the pragma anyway would imply a provisioning step this path does not have, and would
    /// make the application's check confirm work done moments earlier by us.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        temporaryFolder = Path.Combine(
            Path.GetTempPath(),
            "ideaboard-e2e-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(temporaryFolder);

        string databasePath = Path.Combine(temporaryFolder, "ideaboard.db");
        connectionString = "Data Source=" + databasePath;

        await MigrateAsync(connectionString);

        string keysFolder = Path.Combine(temporaryFolder, "keys");
        Directory.CreateDirectory(keysFolder);

        application = await ApplicationProcess.StartAsync(connectionString, keysFolder);
    }


    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (application is not null)
        {
            await application.DisposeAsync();
        }

        DeleteTemporaryFolder();
    }

    #endregion


    #region State

    /// <summary>
    /// Empties the three tables and resets the identifier sequences, so that no scenario
    /// can see what another one left behind.
    ///
    /// This runs before every scenario rather than once per run. Ordering scenarios so that
    /// the empty-list one happens to go first would work until somebody adds a scenario,
    /// and would then fail in a way that looks like a product defect rather than a harness
    /// one.
    ///
    /// Resetting the sequences matters beyond tidiness: the identifier is the third
    /// ordering criterion of the ranking, so letting it drift would make one scenario's
    /// expectations depend on how many ran before it.
    /// </summary>
    public async Task ResetStateAsync()
    {
        using IdeaBoardDbContext context = CreateContext();

        // Deleted in dependency order, because the foreign keys are restrictive.
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Vote;");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Idea;");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM User;");
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM sqlite_sequence WHERE name IN ('Vote', 'Idea', 'User');");
    }


    /// <summary>
    /// Opens the same database the running host is using, for the few things a scenario
    /// has to arrange or confirm away from the screen.
    /// </summary>
    public IdeaBoardDbContext CreateContext()
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("The fixture has not been initialised.");
        }

        DbContextOptionsBuilder<IdeaBoardDbContext> optionsBuilder =
            new DbContextOptionsBuilder<IdeaBoardDbContext>();

        optionsBuilder.UseSqlite(connectionString);

        return new IdeaBoardDbContext(optionsBuilder.Options);
    }

    #endregion


    #region Provisioning

    /// <summary>Creates the schema from the migrations the application ships.</summary>
    private static async Task MigrateAsync(string connectionString)
    {
        DbContextOptionsBuilder<IdeaBoardDbContext> optionsBuilder =
            new DbContextOptionsBuilder<IdeaBoardDbContext>();

        optionsBuilder.UseSqlite(connectionString);

        using IdeaBoardDbContext context = new IdeaBoardDbContext(optionsBuilder.Options);

        await context.Database.MigrateAsync();
    }


    /// <summary>Removes the run's folder, tolerating a file the host still holds.</summary>
    private void DeleteTemporaryFolder()
    {
        if (string.IsNullOrEmpty(temporaryFolder))
        {
            return;
        }

        try
        {
            Directory.Delete(temporaryFolder, true);
        }
        catch (IOException)
        {
            // A leftover file in the machine's temporary folder is not worth failing a run
            // over, and the next run uses a different folder anyway.
        }
    }

    #endregion
}
