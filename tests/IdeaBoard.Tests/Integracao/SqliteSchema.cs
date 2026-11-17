using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// Reads the structure of a SQLite database into text that can be compared line by line.
///
/// Two descriptions are produced side by side on purpose, because neither alone is enough.
///
/// The pragmas answer for columns, keys and indexes in a form that does not care how the
/// statement was typed, and they are the only way to see an index that no statement
/// created, which is what a UNIQUE constraint or a primary key leaves behind.
///
/// The stored statement answers for everything the pragmas do not report, and AUTOINCREMENT
/// is the one that matters here: PRAGMA table_info does not mention it, so a script that
/// dropped it would compare equal while handing out identifiers that get reused after a
/// delete. It is compared with its whitespace collapsed, so that a line break or an indent
/// never fails a test that is asking about structure.
/// </summary>
public static class SqliteSchema
{
    #region Description

    /// <summary>Describes every table and index in the database.</summary>
    public static string Describe(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        using SqliteConnection connection = new SqliteConnection(connectionString);
        connection.Open();

        StringBuilder description = new StringBuilder();

        foreach (string table in ReadNames(connection, "table"))
        {
            description.AppendLine("table " + table);
            description.AppendLine("  statement " + ReadStatement(connection, table));

            DescribeColumns(connection, table, description);
            DescribeForeignKeys(connection, table, description);
            DescribeIndexes(connection, table, description);
        }

        return description.ToString();
    }


    /// <summary>Reads the migrations the database records as applied.</summary>
    public static IReadOnlyList<string> ReadAppliedMigrations(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        using SqliteConnection connection = new SqliteConnection(connectionString);
        connection.Open();

        return ReadStrings(
            connection,
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";");
    }

    #endregion


    #region Tables

    private static void DescribeColumns(
        SqliteConnection connection,
        string table,
        StringBuilder description)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(" + Quote(table) + ");";

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            // The declared order is kept rather than sorted, because the position of a
            // column is part of the structure: anything that inserts by position, a script
            // included, behaves differently when two columns swap places.
            description.AppendLine(
                "  column " + Text(reader, "name") +
                " type=" + Text(reader, "type") +
                " notnull=" + Text(reader, "notnull") +
                " default=" + Text(reader, "dflt_value") +
                " pk=" + Text(reader, "pk"));
        }
    }


    private static void DescribeForeignKeys(
        SqliteConnection connection,
        string table,
        StringBuilder description)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_list(" + Quote(table) + ");";

        List<string> lines = new List<string>();

        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                lines.Add(
                    "  foreign key " + Text(reader, "from") +
                    " -> " + Text(reader, "table") + "." + Text(reader, "to") +
                    " onDelete=" + Text(reader, "on_delete") +
                    " onUpdate=" + Text(reader, "on_update"));
            }
        }

        lines.Sort(StringComparer.Ordinal);

        foreach (string line in lines)
        {
            description.AppendLine(line);
        }
    }

    #endregion


    #region Indexes

    private static void DescribeIndexes(
        SqliteConnection connection,
        string table,
        StringBuilder description)
    {
        List<string> names = new List<string>();
        Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.Ordinal);

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA index_list(" + Quote(table) + ");";

            using SqliteDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                string name = Text(reader, "name");

                names.Add(name);

                headers[name] =
                    "  index " + name +
                    " unique=" + Text(reader, "unique") +
                    " origin=" + Text(reader, "origin") +
                    " partial=" + Text(reader, "partial");
            }
        }

        names.Sort(StringComparer.Ordinal);

        foreach (string name in names)
        {
            description.AppendLine(headers[name] + " columns=" + ReadIndexColumns(connection, name));

            string statement = ReadStatement(connection, name);

            if (statement.Length > 0)
            {
                description.AppendLine("    statement " + statement);
            }
        }
    }


    private static string ReadIndexColumns(SqliteConnection connection, string index)
    {
        using SqliteCommand command = connection.CreateCommand();

        // index_xinfo rather than index_info, because the descending flag lives only in the
        // extended form and one of this schema's indexes is declared descending.
        command.CommandText = "PRAGMA index_xinfo(" + Quote(index) + ");";

        List<string> parts = new List<string>();

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            if (Text(reader, "key") != "1")
            {
                continue;
            }

            string direction = " asc";

            if (Text(reader, "desc") == "1")
            {
                direction = " desc";
            }

            parts.Add(Text(reader, "name") + direction);
        }

        return string.Join(", ", parts);
    }

    #endregion


    #region Reading

    private static IReadOnlyList<string> ReadNames(SqliteConnection connection, string type)
    {
        using SqliteCommand command = connection.CreateCommand();

        // Objects SQLite made for itself are left out. sqlite_sequence is the one that
        // shows up here, and its contents are row counters rather than structure.
        //
        // __EFMigrationsLock is left out for a different reason, and it is the only name
        // excluded by hand. The migrator creates it at run time to keep two processes from
        // migrating at once, and no generated script contains it, which was confirmed
        // against all three scripts in deploy and against the migrations. Comparing it
        // would be comparing how the schema was built rather than what it is. Nothing else
        // is excluded on that argument: if a later version of Entity Framework leaves
        // another table behind, this fails and somebody decides, rather than the comparison
        // quietly growing a blind spot.
        command.CommandText =
            "SELECT name FROM sqlite_master" +
            " WHERE type = $type AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsLock'" +
            " ORDER BY name;";

        command.Parameters.AddWithValue("$type", type);

        List<string> names = new List<string>();

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }


    private static string ReadStatement(SqliteConnection connection, string name)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name);

        object? result = command.ExecuteScalar();

        if (result is null || result is DBNull)
        {
            // An index a constraint created has no statement of its own, which is a fact
            // about it and not a failure to read it.
            return string.Empty;
        }

        return Collapse(Convert.ToString(result, CultureInfo.InvariantCulture) ?? string.Empty);
    }


    private static IReadOnlyList<string> ReadStrings(SqliteConnection connection, string commandText)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;

        List<string> values = new List<string>();

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }


    private static string Text(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);

        if (reader.IsDBNull(ordinal))
        {
            return "null";
        }

        return Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "null";
    }

    #endregion


    #region Text

    private static string Collapse(string statement)
    {
        StringBuilder collapsed = new StringBuilder(statement.Length);

        bool inWhitespace = false;

        foreach (char character in statement)
        {
            if (char.IsWhiteSpace(character))
            {
                inWhitespace = true;
                continue;
            }

            if (inWhitespace && collapsed.Length > 0)
            {
                collapsed.Append(' ');
            }

            inWhitespace = false;

            collapsed.Append(character);
        }

        return collapsed.ToString();
    }


    private static string Quote(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    #endregion
}
