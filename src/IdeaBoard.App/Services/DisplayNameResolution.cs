namespace IdeaBoard.App.Services;

/// <summary>
/// The outcome of looking somebody's display name up in a directory.
///
/// Two outcomes and no third one. Either a name was found, or it was not, and the reason
/// it was not is deliberately absent from this type: the caller behaves identically
/// whether the directory was unreachable, the account was missing or the name came back
/// blank. A reason here would invite a caller to branch on it, and every one of those
/// branches ends in the same place.
///
/// The reason is not lost. It is written to the log by whoever did the lookup, where it
/// belongs, because it describes the state of the infrastructure and not the state of the
/// person.
/// </summary>
public sealed class DisplayNameResolution
{
    #region Fields

    private static readonly DisplayNameResolution notResolved = new DisplayNameResolution(false, string.Empty);

    #endregion


    #region Construction

    /// <summary>Private so that the only two shapes are the two factories below.</summary>
    private DisplayNameResolution(bool isResolved, string displayName)
    {
        IsResolved = isResolved;
        DisplayName = displayName;
    }


    /// <summary>
    /// A name was found. A blank name is refused here rather than stored, because a blank
    /// name shown as an idea's author is the exact outcome this whole mechanism exists to
    /// prevent, and the caller has no way to tell it apart once it is inside a "resolved"
    /// answer.
    /// </summary>
    public static DisplayNameResolution Resolved(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new DisplayNameResolution(true, displayName.Trim());
    }


    /// <summary>No name is available, for whatever reason.</summary>
    public static DisplayNameResolution NotResolved()
    {
        return notResolved;
    }

    #endregion


    #region State

    /// <summary>Whether a usable name was found.</summary>
    public bool IsResolved { get; }

    /// <summary>The name found, trimmed. Empty when nothing was found.</summary>
    public string DisplayName { get; }

    #endregion
}
