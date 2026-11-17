using System.Reflection;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// Nothing reachable from IdeaBoard.Web carries a host into the published artefact.
///
/// THIS IS THE INVARIANT ITSELF AND NOT THE GUARD THAT DEFENDS IT. Directory.Build.targets
/// refuses to build a watched project that declares a forbidden ProjectReference, and that
/// guard is worth having, but until now nothing anywhere asserted the outcome it exists to
/// produce. A guard nobody exercises is a guard nobody knows still works.
///
/// It also sees less than the invariant says. It reads the ProjectReference items of two
/// named projects, so a DLL referenced directly, one arriving through a package, one copied
/// into the output folder by a target, or a reference added to a project nobody remembered
/// to add a GuardedProject row for, all pass it without being noticed. The tests here ask
/// the artefact and the compiled metadata instead, which is where the answer actually has
/// to be true.
///
/// Why it matters: IdeaBoard.Local and IdeaBoard.LocalIdentity exist to stand in for the
/// Windows directory and to sign people in without a domain. On a server that is not a
/// development convenience, it is a way into the application as somebody else.
/// </summary>
public class PublishedClosureTests
{
    #region Constants

    /// <summary>
    /// The prefix both local hosts share. Matching on the prefix rather than on two exact
    /// names is deliberate: a third one would be named the same way and would otherwise
    /// arrive unwatched.
    /// </summary>
    private const string LocalPrefix = "IdeaBoard.Local";

    #endregion


    #region Tests

    [Fact]
    public void TheHostBuildOutputCarriesNothingLocal()
    {
        string output = LocateHostOutput();

        List<string> offenders = new List<string>();

        foreach (string file in Directory.GetFiles(output))
        {
            string name = Path.GetFileName(file);

            if (name.StartsWith(LocalPrefix, StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add(name);
            }
        }

        Assert.True(
            offenders.Count == 0,
            string.Concat(
                "The published host's output folder carries ",
                string.Join(", ", offenders),
                ". Anything sitting there is published, whether or not a project reference "
                + "put it there, which is why this asks the folder and not the project file. "
                + "Folder: ",
                output));
    }


    [Fact]
    public void TheHostDependencyManifestNamesNothingLocal()
    {
        string manifest = Path.Combine(LocateHostOutput(), "IdeaBoard.Web.deps.json");

        Assert.True(
            File.Exists(manifest),
            string.Concat("The published host's dependency manifest was not found at ", manifest, "."));

        string contents = File.ReadAllText(manifest);

        // The manifest is what the runtime reads to decide what to load, so a name in here
        // is a thing that ships even if the file beside it was cleaned away by hand.
        Assert.DoesNotContain(LocalPrefix, contents, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void TheHostAssemblyClosureReferencesNothingLocal()
    {
        // Program is the published host's own entry point, so the walk starts where the
        // artefact starts and not at a library that happens to be nearby.
        Assembly host = typeof(Program).Assembly;

        List<string> visited = new List<string>();
        List<string> offenders = new List<string>();

        Walk(host, visited, offenders);

        Assert.True(
            offenders.Count == 0,
            string.Concat(
                "The assembly closure reachable from the published host reaches ",
                string.Join(", ", offenders),
                ". Walked: ",
                string.Join(", ", visited)));
    }

    #endregion


    #region Walking

    /// <summary>
    /// Follows the references of one assembly and of every IdeaBoard assembly it reaches.
    ///
    /// Only this project's own assemblies are followed. Walking into the framework would
    /// load hundreds of assemblies to answer a question none of them can affect.
    /// </summary>
    private static void Walk(Assembly assembly, List<string> visited, List<string> offenders)
    {
        string name = assembly.GetName().Name ?? string.Empty;

        if (visited.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        visited.Add(name);

        if (name.StartsWith(LocalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            offenders.Add(name);

            return;
        }

        foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
        {
            string referenceName = reference.Name ?? string.Empty;

            if (!referenceName.StartsWith("IdeaBoard.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (referenceName.StartsWith(LocalPrefix, StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add(referenceName);

                continue;
            }

            Walk(Assembly.Load(reference), visited, offenders);
        }
    }

    #endregion


    #region Files

    /// <summary>
    /// Finds the published host's build output, in the same configuration and framework
    /// these tests were built in.
    ///
    /// The host's own folder is read rather than the test project's. They both end up
    /// holding IdeaBoard.Web.dll, but the test project's folder is the union of everything
    /// every test reference dragged in, so asking it what the host publishes would be
    /// asking the wrong folder and getting a confident answer.
    /// </summary>
    private static string LocateHostOutput()
    {
        string baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

        string framework = Path.GetFileName(baseDirectory);
        string configuration = Path.GetFileName(Path.GetDirectoryName(baseDirectory) ?? string.Empty);

        DirectoryInfo? folder = new DirectoryInfo(baseDirectory);

        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "IdeaBoard.sln")))
        {
            folder = folder.Parent;
        }

        if (folder is null)
        {
            throw new InvalidOperationException("The solution root could not be found.");
        }

        string output = Path.Combine(
            folder.FullName, "src", "IdeaBoard.Web", "bin", configuration, framework);

        if (!Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "The published host's output folder was not found at " + output + ".");
        }

        return output;
    }

    #endregion
}
