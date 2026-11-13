using System.Globalization;
using Microsoft.Data.Sqlite;

namespace IdeaBoard.App.Data;

/// <summary>
/// Confirms at startup that the database file is in write-ahead logging mode.
///
/// This verifies and does not impose. The mode is persistent in the file and is set once
/// during provisioning, so a database that is not in WAL means the provisioning step was
/// skipped. Applying the pragma here instead would make the check confirm what this code
/// had just done, which is worth much less as detection.
/// </summary>
public static class SqliteJournalModeVerifier
{
    #region Constants

    private const string RequiredJournalMode = "wal";

    #endregion


    #region Verification

    /// <summary>
    /// Opens the database and refuses startup when the journal mode is anything other than
    /// write-ahead logging, naming the mode that was found.
    /// </summary>
    public static void EnsureWriteAheadLogging(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        using SqliteConnection connection = new SqliteConnection(connectionString);
        connection.Open();

        string journalMode = ReadJournalMode(connection);

        if (string.Equals(journalMode, RequiredJournalMode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(BuildFailureMessage(journalMode, connection.DataSource));
    }


    /// <summary>
    /// Reads the current journal mode without altering it. The assigning form of this
    /// pragma also returns the resulting mode, and that mode is not always the one asked
    /// for, which is why the returned value is what gets checked either way.
    /// </summary>
    private static string ReadJournalMode(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";

        object? result = command.ExecuteScalar();

        if (result is null)
        {
            return string.Empty;
        }

        return Convert.ToString(result, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    #endregion


    #region Reporting

    /// <summary>Builds the startup failure message, naming the mode actually found.</summary>
    private static string BuildFailureMessage(string journalMode, string dataSource)
    {
        string foundMode = journalMode;

        if (string.IsNullOrWhiteSpace(foundMode))
        {
            foundMode = "unknown";
        }

        return
            $"IdeaBoard refused to start because the database '{dataSource}' is in journal mode " +
            $"'{foundMode}' instead of '{RequiredJournalMode}'." +
            Environment.NewLine +
            Environment.NewLine +
            "Write-ahead logging is set once, when the database is provisioned, by running: " +
            "PRAGMA journal_mode = WAL; and confirming the mode it returns." +
            Environment.NewLine +
            Environment.NewLine +
            "Treat this as a trip wire rather than as the fault itself. The journal mode is cheap " +
            "to correct, and a database that reached this point without it is a database whose " +
            "provisioning was skipped, so check what else was skipped with it: the write " +
            "permission on the containing folder, the database living outside the published " +
            "folder, and the Data Protection key folder.";
    }

    #endregion
}
