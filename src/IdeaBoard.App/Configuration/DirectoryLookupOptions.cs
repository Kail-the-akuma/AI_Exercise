namespace IdeaBoard.App.Configuration;

/// <summary>
/// How the application is allowed to talk to the directory when it needs somebody's name.
/// </summary>
public class DirectoryLookupOptions
{
    #region Constants

    /// <summary>
    /// How long a single lookup may take before it is abandoned.
    ///
    /// The number is a judgement and the measurement behind it is a lower bound, not a
    /// guarantee. On a machine that belongs to no domain the call fails in roughly one
    /// second, because there is nothing to contact and no name to resolve. A real domain
    /// controller under load, or one being failed over, can take considerably longer, and
    /// that can only be measured where such a controller exists.
    ///
    /// Five seconds is chosen from both ends. Long enough that a slow but working
    /// controller still answers, so the manual path is not taken by people who did not
    /// need it. Short enough that nobody sits in front of a blank browser wondering
    /// whether the site is broken. The cost is paid once per person, on their first
    /// request ever, and never again.
    /// </summary>
    public const int DefaultTimeoutSeconds = 5;

    /// <summary>Highest timeout the configuration is allowed to ask for.</summary>
    public const int MaximumTimeoutSeconds = 60;

    #endregion


    #region State

    /// <summary>
    /// Whether the directory is consulted at all.
    ///
    /// It defaults to consulting, because that is the behaviour of the environment the
    /// application is published to, and a default that is right only on a developer
    /// machine is a default that lies about production. Switching it off forces the manual
    /// path, which is how that path gets exercised somewhere other than by accident.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Timeout for one lookup, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    #endregion


    #region Derived

    /// <summary>
    /// The timeout handed to the directory search itself, which is the one that actually
    /// stops the call.
    /// </summary>
    public TimeSpan Timeout
    {
        get
        {
            return TimeSpan.FromSeconds(TimeoutSeconds);
        }
    }

    /// <summary>
    /// A looser ceiling on the whole attempt, as a backstop.
    ///
    /// The search timeout above covers the search. It does not cover resolving the domain
    /// and binding to it, which happens before any search is sent and can hang on its own.
    /// This is deliberately longer so that the inner timeout, the one that genuinely
    /// cancels rather than merely stops waiting, is the one that normally wins.
    /// </summary>
    public TimeSpan OuterCeiling
    {
        get
        {
            return TimeSpan.FromSeconds(TimeoutSeconds + 2);
        }
    }

    #endregion
}
