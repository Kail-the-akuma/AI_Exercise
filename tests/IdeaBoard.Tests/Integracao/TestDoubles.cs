using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The signed-in person, fixed by the test instead of derived from a request.
///
/// It is a plain stand-in and never touches the database, so a query counted during a test
/// is a query the service under test asked for.
/// </summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    #region Fields

    private User signedIn;

    #endregion


    #region Construction

    /// <summary>Creates the stand-in for a given person.</summary>
    public FakeCurrentUser(User signedIn)
    {
        ArgumentNullException.ThrowIfNull(signedIn);

        this.signedIn = signedIn;
    }

    #endregion


    #region Resolution

    /// <summary>Changes who the following calls are made as.</summary>
    public void SignInAs(User other)
    {
        ArgumentNullException.ThrowIfNull(other);

        signedIn = other;
    }


    /// <inheritdoc />
    public Task<User> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(signedIn);
    }


    /// <inheritdoc />
    public Task<User?> TryGetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<User?>(signedIn);
    }

    #endregion
}


/// <summary>
/// A clock the test moves by hand, so that rows can be given instants that disagree with
/// the order they were inserted in. Without that, an ordering test checks the identifier
/// and calls it a date.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    #region Fields

    private DateTimeOffset utcNow;

    #endregion


    #region Construction

    /// <summary>Creates the clock stopped at a given instant.</summary>
    public FakeTimeProvider(DateTimeOffset start)
    {
        utcNow = start;
    }

    #endregion


    #region Clock

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        return utcNow;
    }


    /// <summary>Moves the clock to an exact instant.</summary>
    public void SetUtcNow(DateTimeOffset value)
    {
        utcNow = value;
    }

    #endregion
}


/// <summary>
/// A directory that answers whatever the test tells it to, and counts how often it was
/// asked.
///
/// The count is not decoration. The rule that a lookup is paid once, on a person's first
/// request, and never again, is invisible in the result and only observable here.
/// </summary>
public sealed class FakeDisplayNameResolver : IDisplayNameResolver
{
    #region Fields

    private string? answer;

    #endregion


    #region Construction

    /// <summary>Creates a directory that resolves nothing.</summary>
    public FakeDisplayNameResolver()
    {
        answer = null;
    }


    /// <summary>Creates a directory that answers with a given name.</summary>
    public FakeDisplayNameResolver(string answer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(answer);

        this.answer = answer;
    }

    #endregion


    #region State

    /// <summary>How many lookups were asked for.</summary>
    public int Calls { get; private set; }

    /// <summary>The identifier the last lookup was made with.</summary>
    public string? LastIdentifier { get; private set; }


    /// <summary>Changes what the directory answers from now on.</summary>
    public void AnswerWith(string? displayName)
    {
        answer = displayName;
    }

    #endregion


    #region Resolution

    /// <inheritdoc />
    public Task<DisplayNameResolution> ResolveAsync(string accountIdentifier, CancellationToken cancellationToken)
    {
        Calls++;
        LastIdentifier = accountIdentifier;

        if (string.IsNullOrWhiteSpace(answer))
        {
            return Task.FromResult(DisplayNameResolution.NotResolved());
        }

        return Task.FromResult(DisplayNameResolution.Resolved(answer));
    }

    #endregion
}
