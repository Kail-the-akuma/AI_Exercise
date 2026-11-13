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
