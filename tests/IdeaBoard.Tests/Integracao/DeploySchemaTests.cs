using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// Holds the script the provisioning procedure runs against the schema the migrations
/// produce.
///
/// Nothing in the application calls Migrate. On a real server the schema is built once, by
/// hand, by running deploy/migracao.sql, and from then on the application assumes the
/// database matches the model it was compiled against. So the script is not a convenience
/// next to the migrations, it is the only thing that ever builds the schema in production,
/// and until now nothing compared the two.
///
/// What that leaves open is a migration added to the model without the script being
/// generated again. Every test in the solution would stay green, because every test builds
/// its database from the migrations, and the mismatch would first be seen by a person on
/// the server, as a missing column at run time.
/// </summary>
public class DeploySchemaTests
{
    #region Tests

    [Fact]
    public void TheDeployScriptBuildsTheSameSchemaAsTheMigrations()
    {
        string script = ReadDeployScript("migracao.sql");

        using TemporarySqliteDatabase byMigrations = new TemporarySqliteDatabase();
        using TemporarySqliteDatabase byScript = TemporarySqliteDatabase.BuiltByScript(script);

        string expected = SqliteSchema.Describe(byMigrations.ConnectionString);
        string actual = SqliteSchema.Describe(byScript.ConnectionString);

        Assert.Equal(expected, actual);
    }


    [Fact]
    public void TheDeployScriptRecordsTheSameMigrationsAsTheMigrationsDo()
    {
        string script = ReadDeployScript("migracao.sql");

        using TemporarySqliteDatabase byMigrations = new TemporarySqliteDatabase();
        using TemporarySqliteDatabase byScript = TemporarySqliteDatabase.BuiltByScript(script);

        // The schema and the record of it are separate failures. A script that builds every
        // table but stamps one migration short leaves a database that looks right and that
        // the next generated script would be written against the wrong baseline for.
        Assert.Equal(
            SqliteSchema.ReadAppliedMigrations(byMigrations.ConnectionString),
            SqliteSchema.ReadAppliedMigrations(byScript.ConnectionString));
    }

    #endregion


    #region Files

    /// <summary>
    /// Reads a file from the deploy folder, found by walking up from the test assembly.
    ///
    /// The file is read where it lives rather than copied into the output folder next to
    /// the tests. A copy is refreshed by the build, which is almost always, and the one
    /// time it is not the test passes against a script nobody is going to run.
    /// </summary>
    private static string ReadDeployScript(string fileName)
    {
        DirectoryInfo? folder = new DirectoryInfo(AppContext.BaseDirectory);

        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "IdeaBoard.sln")))
        {
            folder = folder.Parent;
        }

        if (folder is null)
        {
            throw new InvalidOperationException("The solution root could not be found.");
        }

        string path = Path.Combine(folder.FullName, "deploy", fileName);

        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The deploy script was not found at " + path + ".");
        }

        return File.ReadAllText(path);
    }

    #endregion
}
