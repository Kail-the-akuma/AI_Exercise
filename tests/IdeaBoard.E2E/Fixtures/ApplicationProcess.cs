using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace IdeaBoard.E2E.Fixtures;

/// <summary>
/// Runs the local host as its own process and waits until it answers.
///
/// Measured, not assumed, on the .NET 10 SDK: a plain WebApplicationFactory installs
/// Microsoft.AspNetCore.TestHost.TestServer, publishes no server addresses, and hands out a
/// client bound to the synthetic http://localhost/. Nothing a browser can connect to. And
/// substituting Kestrel from CreateHost does not work either, because the factory then
/// casts the server to TestServer and throws InvalidCastException.
///
/// The remaining options were to reassemble the pipeline inside the fixture or to run the
/// real entry point. Reassembling would mean the suite exercises a host built here rather
/// than the one that ships, which is the one thing an end to end suite must not do, so the
/// application is started exactly as a person starts it.
/// </summary>
public class ApplicationProcess : IAsyncDisposable
{
    #region Fields

    private readonly Process process;

    private static readonly List<string> capturedOutput = [];

    #endregion


    #region Construction

    private ApplicationProcess(Process process, string baseAddress)
    {
        this.process = process;

        BaseAddress = baseAddress;
    }

    #endregion


    #region Properties

    /// <summary>Address the browser navigates to.</summary>
    public string BaseAddress { get; }

    #endregion


    #region Startup

    /// <summary>Starts the host against the given database and waits for it to answer.</summary>
    public static async Task<ApplicationProcess> StartAsync(string connectionString, string keysFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(keysFolder);

        int port = ReserveFreePort();
        string baseAddress = "http://127.0.0.1:" + port.ToString();

        string assemblyPath = LocateHostAssembly();

        ProcessStartInfo startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(assemblyPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add(assemblyPath);

        startInfo.Environment["ASPNETCORE_URLS"] = baseAddress;
        startInfo.Environment["ConnectionStrings__IdeaBoard"] = connectionString;

        // Outside Development the application refuses to start without somewhere to persist
        // the key ring, which is correct: on a server, keys that live only in memory end
        // sessions at every recycle. Pointing it at the run's own folder also keeps the
        // suite from writing keys onto the machine it happens to be running on.
        startInfo.Environment["DataProtection__KeysPath"] = keysFolder;

        // Anything other than Development, so the pipeline under test is the one that
        // ships: the generic error page is wired in, and nothing branches to a developer
        // convenience. The cost is that a failure here is as opaque as it is in production.
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "E2E";

        Process? started = Process.Start(startInfo);

        if (started is null)
        {
            throw new InvalidOperationException("The host process did not start.");
        }

        // Drained so that a full pipe cannot block the host, and kept so that a host which
        // refuses to start can say why instead of leaving us to guess.
        capturedOutput.Clear();

        started.OutputDataReceived += OnOutput;
        started.ErrorDataReceived += OnOutput;

        started.BeginOutputReadLine();
        started.BeginErrorReadLine();

        await WaitUntilAnsweringAsync(started, baseAddress);

        return new ApplicationProcess(started, baseAddress);
    }


    /// <summary>
    /// Asks the operating system for a free port and gives it straight back.
    ///
    /// Docs/07 describes asking Kestrel for port zero and reading what it bound. Out of
    /// process that address is only available by parsing a log line, which is a weaker
    /// thing to depend on than this, so the port is chosen here and handed over instead.
    /// </summary>
    private static int ReserveFreePort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        listener.Stop();

        return port;
    }


    /// <summary>Finds the built local host next to the solution file.</summary>
    private static string LocateHostAssembly()
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

        string configuration = Path.GetFileName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)))!;

        string assemblyPath = Path.Combine(
            folder.FullName,
            "src",
            "IdeaBoard.Local",
            "bin",
            configuration,
            "net10.0",
            "IdeaBoard.Local.dll");

        if (!File.Exists(assemblyPath))
        {
            throw new InvalidOperationException(
                "The local host has not been built. Expected it at " + assemblyPath);
        }

        return assemblyPath;
    }


    /// <summary>Polls the host until it answers, or gives up with what it knows.</summary>
    private static async Task WaitUntilAnsweringAsync(Process started, string baseAddress)
    {
        using HttpClient client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(2);

        DateTime deadline = DateTime.UtcNow.AddSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            if (started.HasExited)
            {
                throw new InvalidOperationException(
                    "The host exited during startup with code " + started.ExitCode.ToString() +
                    "." + Environment.NewLine + ReadCapturedOutput());
            }

            try
            {
                using HttpResponseMessage response = await client.GetAsync(baseAddress + "/pessoa");

                return;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(200);
            }
            catch (TaskCanceledException)
            {
                await Task.Delay(200);
            }
        }

        throw new TimeoutException(
            "The host did not answer within sixty seconds." + Environment.NewLine +
            ReadCapturedOutput());
    }

    /// <summary>Keeps a line the host wrote, for a failure message that can be acted on.</summary>
    private static void OnOutput(object sender, DataReceivedEventArgs arguments)
    {
        if (arguments.Data is null)
        {
            return;
        }

        lock (capturedOutput)
        {
            capturedOutput.Add(arguments.Data);
        }
    }


    /// <summary>Returns what the host wrote, so a failure arrives with its reason.</summary>
    private static string ReadCapturedOutput()
    {
        lock (capturedOutput)
        {
            if (capturedOutput.Count == 0)
            {
                return "The host wrote nothing before exiting.";
            }

            return "What the host wrote:" + Environment.NewLine +
                string.Join(Environment.NewLine, capturedOutput);
        }
    }

    #endregion


    #region Shutdown

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (!process.HasExited)
        {
            process.Kill(true);

            await process.WaitForExitAsync();
        }

        process.Dispose();
    }

    #endregion
}
