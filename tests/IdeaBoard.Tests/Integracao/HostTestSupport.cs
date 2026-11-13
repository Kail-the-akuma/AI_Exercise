using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// Groups the tests that start the published host.
///
/// They are serialised because each one sets process wide environment variables to feed the
/// host its configuration, and two of them running at once would read each other's values.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class HostTestCollection
{
    /// <summary>Name the tests in this group refer to.</summary>
    public const string Name = "IdeaBoard published host";
}


/// <summary>
/// Sets environment variables for the duration of a test and puts back whatever was there
/// before, including putting nothing back where there was nothing.
///
/// Environment variables are the only configuration source that reaches the published host
/// in time. Its composition root reads configuration while the service collection is still
/// being filled, which happens before a test host gets a chance to layer anything on top,
/// so a value supplied any other way arrives after the host has already decided.
/// </summary>
public sealed class EnvironmentVariableScope : IDisposable
{
    #region Fields

    private readonly Dictionary<string, string?> previousValues =
        new Dictionary<string, string?>(StringComparer.Ordinal);

    private bool disposed;

    #endregion


    #region Scope

    /// <summary>Sets a variable, or removes it when the value is null.</summary>
    public void Set(string name, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!previousValues.ContainsKey(name))
        {
            previousValues[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable(name, value);
    }


    /// <summary>Puts every touched variable back the way it was.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        foreach (KeyValuePair<string, string?> entry in previousValues)
        {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }

        previousValues.Clear();
    }

    #endregion
}


/// <summary>
/// The published host, started in process.
///
/// The environment is Development, and deliberately so: static web assets of a referenced
/// Razor class library are only served from a build in that environment, and outside it the
/// configuration validator also requires a Data Protection key folder that a test has no
/// business inventing. What this covers is therefore "it serves in development". Whether
/// the same file reaches the published tree is a different mechanism, checked elsewhere.
/// </summary>
public sealed class IdeaBoardWebFactory : WebApplicationFactory<Program>
{
    #region Configuration

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");
    }

    #endregion
}


/// <summary>
/// Flattens an exception chain into one piece of text, so that a test can assert on what a
/// startup failure actually said regardless of how many layers wrapped it.
/// </summary>
public static class ExceptionText
{
    #region Flattening

    /// <summary>Returns the type and message of the exception and everything inside it.</summary>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        StringBuilder builder = new StringBuilder();

        Append(builder, exception, 0);

        return builder.ToString();
    }


    private static void Append(StringBuilder builder, Exception exception, int depth)
    {
        if (depth > 10)
        {
            return;
        }

        builder.AppendLine(exception.GetType().FullName);
        builder.AppendLine(exception.Message);

        if (exception is AggregateException aggregate)
        {
            AppendAll(builder, aggregate, depth);
            return;
        }

        if (exception.InnerException is null)
        {
            return;
        }

        Append(builder, exception.InnerException, depth + 1);
    }


    private static void AppendAll(StringBuilder builder, AggregateException aggregate, int depth)
    {
        foreach (Exception inner in aggregate.InnerExceptions)
        {
            Append(builder, inner, depth + 1);
        }
    }

    #endregion
}
