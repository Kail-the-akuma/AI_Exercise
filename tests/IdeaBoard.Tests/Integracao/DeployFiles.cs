namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The deploy folder, as it sits in the repository.
///
/// The files are read where they live rather than copied into the output folder beside the
/// tests. A copy is refreshed by the build, which is almost always, and the one time it is
/// not the test passes against a script nobody is going to run.
/// </summary>
public static class DeployFiles
{
    #region Access

    /// <summary>The deploy folder itself, found by walking up from the test assembly.</summary>
    public static string Folder()
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

        string deploy = Path.Combine(folder.FullName, "deploy");

        if (!Directory.Exists(deploy))
        {
            throw new InvalidOperationException("The deploy folder was not found at " + deploy + ".");
        }

        return deploy;
    }


    /// <summary>Reads one file from the deploy folder.</summary>
    public static string Read(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string path = Path.Combine(Folder(), fileName);

        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The deploy script was not found at " + path + ".");
        }

        return File.ReadAllText(path);
    }

    #endregion
}
