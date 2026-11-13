using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// Counts the commands that actually reach the database.
///
/// This is how a list test tells "one extra query for the whole page" apart from "one query
/// per row". Counting the rows returned would not do it, because both shapes return the
/// same rows.
/// </summary>
public sealed class CountingCommandInterceptor : DbCommandInterceptor
{
    #region Fields

    private int executedCommandCount;

    #endregion


    #region State

    /// <summary>Commands executed since the last reset.</summary>
    public int ExecutedCommandCount
    {
        get
        {
            return Volatile.Read(ref executedCommandCount);
        }
    }


    /// <summary>Starts a fresh count, so that warm-up work is not measured.</summary>
    public void Reset()
    {
        Volatile.Write(ref executedCommandCount, 0);
    }

    #endregion


    #region Counting

    /// <inheritdoc />
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Count();
        return base.ReaderExecuting(command, eventData, result);
    }


    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Count();
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }


    /// <inheritdoc />
    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Count();
        return base.ScalarExecuting(command, eventData, result);
    }


    /// <inheritdoc />
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Count();
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }


    /// <inheritdoc />
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Count();
        return base.NonQueryExecuting(command, eventData, result);
    }


    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Count();
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }


    private void Count()
    {
        Interlocked.Increment(ref executedCommandCount);
    }

    #endregion
}


/// <summary>
/// Writes a competing row on a separate connection in the instant between the service
/// deciding to insert and its insert reaching SQLite.
///
/// This is the race the unique indexes exist for, and it is the only way to reach the code
/// that treats a violation of one as success. Two sequential calls never reach it, because
/// the second one finds the row the first one wrote and returns early, so a test built out
/// of two plain calls proves only that the early return works.
/// </summary>
public sealed class RacingInsertInterceptor : DbCommandInterceptor
{
    #region Fields

    private readonly string connectionString;
    private readonly string tableName;
    private readonly string competingCommandText;
    private readonly Dictionary<string, object> competingParameters;

    private bool raced;

    #endregion


    #region Construction

    /// <summary>
    /// Creates the interceptor. It fires once, on the first insert into the named table,
    /// and runs the given statement on a connection of its own before letting the
    /// intercepted insert through.
    /// </summary>
    public RacingInsertInterceptor(
        string connectionString,
        string tableName,
        string competingCommandText,
        Dictionary<string, object> competingParameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(competingCommandText);
        ArgumentNullException.ThrowIfNull(competingParameters);

        this.connectionString = connectionString;
        this.tableName = tableName;
        this.competingCommandText = competingCommandText;
        this.competingParameters = competingParameters;
    }

    #endregion


    #region State

    /// <summary>Whether the competing row was actually written.</summary>
    public bool Raced
    {
        get
        {
            return raced;
        }
    }

    #endregion


    #region Racing

    /// <inheritdoc />
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        RaceIfThisIsTheInsert(command);
        return base.ReaderExecuting(command, eventData, result);
    }


    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        RaceIfThisIsTheInsert(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }


    /// <inheritdoc />
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        RaceIfThisIsTheInsert(command);
        return base.NonQueryExecuting(command, eventData, result);
    }


    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        RaceIfThisIsTheInsert(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }


    private void RaceIfThisIsTheInsert(DbCommand command)
    {
        if (raced)
        {
            return;
        }

        if (!IsInsertIntoWatchedTable(command.CommandText))
        {
            return;
        }

        raced = true;

        RunCompetingStatement();
    }


    private bool IsInsertIntoWatchedTable(string commandText)
    {
        if (commandText.IndexOf("INSERT INTO", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        return commandText.IndexOf(string.Concat("\"", tableName, "\""), StringComparison.Ordinal) >= 0;
    }


    private void RunCompetingStatement()
    {
        using SqliteConnection connection = new SqliteConnection(connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = competingCommandText;

        foreach (KeyValuePair<string, object> parameter in competingParameters)
        {
            command.Parameters.AddWithValue(parameter.Key, parameter.Value);
        }

        command.ExecuteNonQuery();
    }

    #endregion
}
