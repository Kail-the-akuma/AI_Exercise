using System.Globalization;
using IdeaBoard.App.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// A real SQLite database in a temporary file, created by applying the migrations and
/// removed when the test that owns it finishes.
///
/// The schema is built by the migrations and never by EnsureCreated, so what the tests run
/// against is the same schema the provisioning procedure produces. Applying them here is
/// allowed precisely because the application itself never migrates at startup.
///
/// Connection pooling is turned off so that the file can be deleted as soon as the last
/// context is disposed, which is what keeps a failing test from leaving a file behind.
/// </summary>
public sealed class TemporarySqliteDatabase : IDisposable
{
    #region Fields

    private readonly string databasePath;
    private readonly string connectionString;

    private bool disposed;

    #endregion


    #region Construction

    /// <summary>Creates the file and applies every migration to it.</summary>
    public TemporarySqliteDatabase()
    {
        databasePath = Path.Combine(
            Path.GetTempPath(),
            string.Concat("ideaboard-test-", Guid.NewGuid().ToString("N"), ".db"));

        SqliteConnectionStringBuilder connectionBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
        };

        connectionString = connectionBuilder.ToString();

        using IdeaBoardDbContext context = CreateContext();
        context.Database.Migrate();
    }

    #endregion


    #region State

    /// <summary>Connection string pointing at the temporary file.</summary>
    public string ConnectionString
    {
        get
        {
            return connectionString;
        }
    }

    /// <summary>Full path of the temporary file.</summary>
    public string DatabasePath
    {
        get
        {
            return databasePath;
        }
    }

    #endregion


    #region Contexts

    /// <summary>Opens a new context over the same file.</summary>
    public IdeaBoardDbContext CreateContext()
    {
        return CreateContext(null);
    }


    /// <summary>Opens a new context over the same file, optionally intercepted.</summary>
    public IdeaBoardDbContext CreateContext(IInterceptor? interceptor)
    {
        DbContextOptionsBuilder<IdeaBoardDbContext> optionsBuilder =
            new DbContextOptionsBuilder<IdeaBoardDbContext>();

        optionsBuilder.UseSqlite(connectionString);

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new IdeaBoardDbContext(optionsBuilder.Options);
    }

    #endregion


    #region Raw access

    /// <summary>
    /// Runs a statement straight against the file, outside Entity Framework. Tests use it
    /// to set up conditions the model cannot express, such as a trigger that makes a write
    /// fail for a reason other than a unique index.
    /// </summary>
    public void ExecuteNonQuery(string commandText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);

        using SqliteConnection connection = new SqliteConnection(connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }


    /// <summary>Reads a single number straight from the file, outside Entity Framework.</summary>
    public long ExecuteScalarCount(string commandText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);

        using SqliteConnection connection = new SqliteConnection(connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;

        object? result = command.ExecuteScalar();

        if (result is null)
        {
            return 0;
        }

        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    #endregion


    #region Journal mode

    /// <summary>
    /// Puts the file into write-ahead logging mode and returns the mode SQLite reported
    /// back. The assigning form of the pragma does not always grant the mode it was asked
    /// for, so the caller checks the returned value rather than assuming.
    /// </summary>
    public string EnableWriteAheadLogging()
    {
        return ExecuteJournalModePragma("PRAGMA journal_mode = WAL;");
    }


    /// <summary>
    /// Takes the file to another journal mode and returns the mode SQLite granted.
    ///
    /// A test that needs an unprovisioned database has to do this explicitly, because since
    /// Entity Framework Core 7 a database that Entity Framework itself creates is put into
    /// write-ahead logging mode on creation. Every temporary database here is created by
    /// the migrations, so every one of them starts out already in the mode the guard
    /// demands.
    /// </summary>
    public string SetJournalMode(string journalMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalMode);

        return ExecuteJournalModePragma(string.Concat("PRAGMA journal_mode = ", journalMode, ";"));
    }


    /// <summary>Reads the current journal mode without changing it.</summary>
    public string ReadJournalMode()
    {
        return ExecuteJournalModePragma("PRAGMA journal_mode;");
    }


    private string ExecuteJournalModePragma(string commandText)
    {
        using SqliteConnection connection = new SqliteConnection(connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;

        object? result = command.ExecuteScalar();

        if (result is null)
        {
            return string.Empty;
        }

        return Convert.ToString(result, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    #endregion


    #region Clean up

    /// <summary>Removes the database file and the two files write-ahead logging adds to it.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        SqliteConnection.ClearAllPools();

        DeleteIfPresent(databasePath);
        DeleteIfPresent(string.Concat(databasePath, "-wal"));
        DeleteIfPresent(string.Concat(databasePath, "-shm"));
    }


    private static void DeleteIfPresent(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        // A handle can still be closing when the last context is disposed, so the delete is
        // retried briefly rather than failing the test over a file lock.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (TryDelete(path))
            {
                return;
            }

            Thread.Sleep(50);
        }
    }


    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    #endregion
}
