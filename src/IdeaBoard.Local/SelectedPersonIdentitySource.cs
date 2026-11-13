using IdeaBoard.LocalIdentity;
using Microsoft.Extensions.Options;

namespace IdeaBoard.Local;

/// <summary>
/// Names the person a request is signed in as, from a cookie the picker sets.
///
/// This is the part that decides, which is why it lives in this host and not in the shared
/// handler. The end to end suite plugs a different source into the same handler, so that
/// nothing it verifies depends on a choice this picker made.
/// </summary>
public class SelectedPersonIdentitySource : ILocalIdentitySource
{
    #region Constants

    /// <summary>Cookie the picker writes the chosen object identifier to.</summary>
    public const string CookieName = "ideaboard-local-person";

    #endregion


    #region Fields

    private readonly LocalPeopleOptions options;

    #endregion


    #region Construction

    /// <summary>Creates the source over the configured list of people.</summary>
    public SelectedPersonIdentitySource(IOptions<LocalPeopleOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options.Value;
    }

    #endregion


    #region Resolution

    /// <inheritdoc />
    public LocalUserIdentity? Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string? objectId = httpContext.Request.Cookies[CookieName];

        if (string.IsNullOrWhiteSpace(objectId))
        {
            return null;
        }

        LocalPerson? person = FindPerson(objectId);

        if (person is null)
        {
            return null;
        }

        return new LocalUserIdentity
        {
            ObjectId = person.ObjectId,
            DisplayName = person.DisplayName,
            Email = person.Email,
        };
    }


    /// <summary>Finds a configured person by object identifier.</summary>
    private LocalPerson? FindPerson(string objectId)
    {
        foreach (LocalPerson candidate in options.People)
        {
            if (string.Equals(candidate.ObjectId, objectId, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    #endregion
}
