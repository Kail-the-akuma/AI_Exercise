namespace IdeaBoard.App.Domain;

/// <summary>
/// Where the name shown next to somebody's ideas and votes came from.
///
/// This exists because two correct rules about names contradict each other, and nothing in
/// the name itself can tell them apart.
///
/// A name that the organisation supplied has to keep following the organisation. Somebody
/// who changes their family name expects the change to show, and a stale name appears
/// publicly wrong on every idea they ever submitted and in every list of voters.
///
/// A name that the PERSON typed, because nothing could be looked up when they first
/// arrived, must never be overwritten by a later lookup. Overwriting it is silent data
/// loss: no error, no trace, and the person finds out by seeing a name they did not choose
/// attached to their own ideas.
///
/// Neither rule can be applied to both cases, so the origin is recorded rather than
/// guessed.
///
/// There are two values and no "unknown" third one, because a user row is never written
/// without a name. A person whose name could not be obtained has no row at all until they
/// register one, which keeps "every user row has a name" an invariant nobody has to defend
/// at each place a name is rendered.
/// </summary>
public enum DisplayNameSource
{
    /// <summary>
    /// Supplied by the organisation, either as a claim on the sign-in or by looking the
    /// account up in the directory. Both are the same kind of source: authoritative,
    /// maintained elsewhere, and expected to change without anybody here being told.
    /// </summary>
    Directory = 1,

    /// <summary>
    /// Typed by the person, because no name could be obtained when they first arrived.
    /// Never replaced by a later lookup.
    /// </summary>
    SelfRegistered = 2,
}
