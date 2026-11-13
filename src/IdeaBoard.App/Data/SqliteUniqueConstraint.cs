using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IdeaBoard.App.Data;

/// <summary>
/// Recognises a unique index violation reported by SQLite, and nothing else.
///
/// There are two places in this product where the database, not the code, enforces a
/// uniqueness rule, and where losing the race is the expected outcome rather than a fault:
/// one vote per person per idea, and one local row per person. Both treat that violation as
/// success, so both need to tell it apart from every other way a write can fail.
///
/// The detection lives here once because the shape of the mistake is the same in both:
/// catching DbUpdateException wholesale satisfies the words of the rule and betrays it, by
/// reporting a disk error, a foreign key failure or a corrupt file to the person as a
/// successful write.
/// </summary>
public static class SqliteUniqueConstraint
{
    #region Constants

    /// <summary>
    /// SQLITE_CONSTRAINT_UNIQUE. The extended result code is what identifies a unique index
    /// violation; the primary code, SQLITE_CONSTRAINT, covers foreign keys and check
    /// constraints too and would be too broad to decide on.
    /// </summary>
    private const int UniqueConstraintExtendedErrorCode = 2067;

    #endregion


    #region Detection

    /// <summary>
    /// Tells whether a failed save was a unique index violation. Everything else, including
    /// any other constraint failure, answers false and is meant to propagate.
    /// </summary>
    public static bool IsViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        return sqliteException.SqliteExtendedErrorCode == UniqueConstraintExtendedErrorCode;
    }

    #endregion
}
