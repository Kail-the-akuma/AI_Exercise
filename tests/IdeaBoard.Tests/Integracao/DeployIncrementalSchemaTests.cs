using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// Holds each incremental deploy script against the migration it stands for.
///
/// THE FULL SCRIPT AND THE INCREMENTALS ARE TWO DIFFERENT PATHS AND ONLY ONE WAS TIED DOWN.
/// deploy/migracao.sql builds a database from nothing and is compared elsewhere. A server
/// that is already installed never runs it again: from the second migration onwards it runs
/// migracao-002, migracao-003 and whatever follows, each written by hand on top of what is
/// already there. That is the path the running server actually takes, and nothing compared
/// it with anything.
///
/// The failure it allows is quiet. Somebody adds a migration, generates the full script
/// again so its comparison stays green, and writes the incremental by hand with a column
/// missing. A new installation is correct, the installed server is broken, and every test
/// passes.
///
/// HOW EACH SCRIPT IS TIED TO ITS MIGRATION. The migrations build a database up to the one
/// before, the script is applied to that, and the result is compared with a database the
/// migrations took one step further. Nothing has to be fetched out of git history, and
/// nothing in here names a migration by hand: the pairing is derived from the order the
/// model declares and from the number in each file name, so a script added later is
/// included without anybody remembering to add it.
/// </summary>
public class DeployIncrementalSchemaTests
{
    #region Tests

    [Fact]
    public void EveryMigrationAfterTheFirstHasAnIncrementalScript()
    {
        IReadOnlyList<string> migrations = TemporarySqliteDatabase.DeclaredMigrations();
        IReadOnlyList<int> numbers = IncrementalNumbers();

        // The first migration has no incremental of its own, because a database that does
        // not exist yet is the full script's job. Every one after it must have one, and the
        // numbers must run without a gap, or a script is missing and the theory below would
        // simply have one case fewer and say nothing.
        List<int> expected = new List<int>();

        for (int index = 2; index <= migrations.Count; index++)
        {
            expected.Add(index);
        }

        Assert.Equal(expected, numbers);
    }


    [Theory]
    [MemberData(nameof(IncrementalScripts))]
    public void TheIncrementalScriptTakesTheSchemaWhereItsMigrationDoes(string fileName)
    {
        int number = NumberOf(fileName);

        IReadOnlyList<string> migrations = TemporarySqliteDatabase.DeclaredMigrations();

        Assert.True(
            number >= 2 && number <= migrations.Count,
            string.Concat(
                "The script ", fileName, " is numbered ",
                number.ToString(CultureInfo.InvariantCulture),
                " and the model declares ",
                migrations.Count.ToString(CultureInfo.InvariantCulture),
                " migrations, so there is no migration for it to stand for."));

        string previous = migrations[number - 2];
        string target = migrations[number - 1];

        using TemporarySqliteDatabase byScript = TemporarySqliteDatabase.MigratedTo(previous);

        byScript.ExecuteNonQuery(DeployFiles.Read(fileName));

        using TemporarySqliteDatabase byMigrations = TemporarySqliteDatabase.MigratedTo(target);

        Assert.Equal(
            SqliteSchema.Describe(byMigrations.ConnectionString),
            SqliteSchema.Describe(byScript.ConnectionString));

        // Separately from the schema, because a script that makes every change and stamps
        // the wrong migration leaves a database that looks right and makes the next
        // generated script come out against the wrong baseline.
        Assert.Equal(
            SqliteSchema.ReadAppliedMigrations(byMigrations.ConnectionString),
            SqliteSchema.ReadAppliedMigrations(byScript.ConnectionString));
    }

    #endregion


    #region Discovery

    /// <summary>Every incremental script in the deploy folder, in the order they apply.</summary>
    public static TheoryData<string> IncrementalScripts()
    {
        TheoryData<string> scripts = new TheoryData<string>();

        foreach (string fileName in IncrementalFileNames())
        {
            scripts.Add(fileName);
        }

        return scripts;
    }


    private static IReadOnlyList<string> IncrementalFileNames()
    {
        List<string> found = new List<string>();

        foreach (string path in Directory.GetFiles(DeployFiles.Folder(), "migracao-*.sql"))
        {
            string fileName = Path.GetFileName(path);

            if (Pattern().IsMatch(fileName))
            {
                found.Add(fileName);
            }
        }

        found.Sort(StringComparer.Ordinal);

        return found;
    }


    private static IReadOnlyList<int> IncrementalNumbers()
    {
        List<int> numbers = new List<int>();

        foreach (string fileName in IncrementalFileNames())
        {
            numbers.Add(NumberOf(fileName));
        }

        return numbers;
    }


    private static int NumberOf(string fileName)
    {
        Match match = Pattern().Match(fileName);

        if (!match.Success)
        {
            throw new InvalidOperationException(
                "The script " + fileName + " is not named the way an incremental is named.");
        }

        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }


    private static Regex Pattern()
    {
        return new Regex(@"^migracao-(\d+)-.+\.sql$");
    }

    #endregion
}
