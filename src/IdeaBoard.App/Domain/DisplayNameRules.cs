namespace IdeaBoard.App.Domain;

/// <summary>
/// What a display name is allowed to be, and the shape checks that keep an account name
/// from ever becoming one.
///
/// Three times over, the same failure has shown up in this application: the TYPE was right
/// and the ORIGIN of the value was wrong. A claim that still exists but now carries
/// something else, an identifier from a different provider, a name field fed from the
/// wrong place. None of those produce an error on their own. They produce a working
/// application showing the wrong thing.
///
/// So the rules here assert the SHAPE of a value rather than its presence, and they live in
/// one place because the same invariant has to hold whether the name came from the
/// directory or from the person who typed it.
/// </summary>
public static class DisplayNameRules
{
    #region Constants

    /// <summary>
    /// Longest name accepted, in characters, after trimming.
    ///
    /// The functional specification fixes a limit for everything a person writes, and sets
    /// none for a name, because when it was written nobody typed one: the name arrived with
    /// the sign-in. Now it is typed, and an unbounded one breaks the line in every list,
    /// shows in the detail page and in the public list of voters, and is PERMANENT, because
    /// nothing in this product edits or removes.
    ///
    /// Eighty is enough for any real name, compound Portuguese surnames included, and short
    /// enough not to wreck a list row. The number is a judgement and can be changed. Having
    /// one is not.
    ///
    /// This one depends on a PRODUCT decision and not on an environment, unlike the shape
    /// rules below it. Nothing about identity or infrastructure can invalidate it; only
    /// somebody deciding that names should be allowed to be longer.
    /// </summary>
    public const int MaximumLength = 80;

    /// <summary>Model state key the registration error is reported under.</summary>
    public const string FieldName = "DisplayName";

    /// <summary>What separates a domain from an account in DOMAIN\someone.</summary>
    public const char AccountSeparator = '\\';

    /// <summary>What separates an account from a domain in someone@domain.</summary>
    public const char UserPrincipalSeparator = '@';

    #endregion


    #region Messages

    /// <summary>Shown when nothing, or only whitespace, was typed.</summary>
    public const string MissingMessage = "Indique o seu nome.";

    /// <summary>Shown when the trimmed name is longer than the limit.</summary>
    public const string TooLongMessage = "O nome não pode ultrapassar 80 caracteres.";

    /// <summary>
    /// Shown when the name has the shape of an account rather than of a person.
    ///
    /// This is a validation message and not a guard against our own mistakes, because this
    /// field is free text typed by a person and shown to the whole team. Without it,
    /// somebody could register a name shaped like an account identifier and appear in the
    /// public list of voters as though they were one.
    /// </summary>
    public const string LooksLikeAccountMessage =
        "O nome não pode conter uma barra invertida. Escreva o seu nome, não o nome de utilizador.";

    #endregion


    #region Validation

    /// <summary>
    /// Trims and checks a name somebody typed, returning the trimmed value and the first
    /// problem found, or no problem at all.
    /// </summary>
    public static DisplayNameValidationResult Validate(string? displayName)
    {
        string trimmed = string.Empty;

        if (displayName is not null)
        {
            trimmed = displayName.Trim();
        }

        if (trimmed.Length == 0)
        {
            return new DisplayNameValidationResult(trimmed, MissingMessage);
        }

        if (trimmed.Length > MaximumLength)
        {
            return new DisplayNameValidationResult(trimmed, TooLongMessage);
        }

        if (LooksLikeAccountName(trimmed))
        {
            return new DisplayNameValidationResult(trimmed, LooksLikeAccountMessage);
        }

        return new DisplayNameValidationResult(trimmed, null);
    }

    #endregion


    #region Shape

    /// <summary>
    /// Tells whether a value has the shape of an account rather than of a person's name.
    ///
    /// A person's name does not contain a backslash. DOMAIN\someone always does. That one
    /// character is the whole test, and it is enough to catch the single failure in this
    /// change that produces no error of any kind: reading the account name into the field
    /// that gets shown to colleagues.
    ///
    /// ENVIRONMENT DEPENDENCY: this rests on people's names not containing a backslash,
    /// which is the closest thing to an invariant in this file and the least likely of
    /// these rules to need revisiting.
    ///
    /// Safe even if the assumption is ever wrong. A legitimate name containing a backslash
    /// would be refused loudly, on the first request, which is where anybody would want to
    /// find out, rather than appearing wrong in a public list of voters.
    /// </summary>
    public static bool LooksLikeAccountName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Contains(AccountSeparator, StringComparison.Ordinal);
    }


    /// <summary>
    /// Tells whether a value has the shape of an ACCOUNT IDENTIFIER rather than of a
    /// person's name: exactly one backslash, or exactly one at sign, with something on
    /// each side and none of the other character.
    ///
    /// THE DISJUNCTION IS LOAD BEARING. DO NOT SIMPLIFY IT TO THE BACKSLASH ALONE.
    ///
    /// The rejected form was "exactly one backslash", which reads more tidily and was
    /// measured to hold: an account really does arrive as DOMAIN\someone. It was measured on
    /// a machine in a workgroup signing in with NTLM. In a domain using Kerberos the same
    /// value can arrive in user principal form, someone@domain, with no backslash at all,
    /// and the tidier rule would then provision NOBODY, on the first day, for everybody.
    ///
    /// That is the identical mistake this application has already made once, when the
    /// identifier was validated by parsing it as a GUID: a correct check resting on a
    /// format belonging to one environment, which became a total block the moment the
    /// environment changed. The difference this time is that it was seen coming.
    ///
    /// Nothing is weakened by accepting both. A person's name carries neither character,
    /// so "Ana Ribeiro" still fails, and catching a presentable name in the account field
    /// is the half that was ever useful. What is given up is telling one legitimate account
    /// format from the other, and those are two spellings of the same thing rather than a
    /// mistake worth catching.
    ///
    /// ENVIRONMENT DEPENDENCY: this rests on the formats the Windows negotiate handler
    /// delivers, and NEITHER of the two has been exercised against a real domain. The
    /// backslash form was measured on a workgroup machine; the user principal form is
    /// anticipated and unverified. A third format would break this.
    ///
    /// It is asked only of a value that arrived under the Windows account claim, never of
    /// whatever a host not using Windows authentication names its accounts: there, an
    /// account field legitimately holds something with no separator at all, and asserting
    /// this against it would be asserting the wrong thing on correct behaviour.
    /// </summary>
    public static bool HasAccountNameShape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (HasExactlyOneSeparator(value, AccountSeparator))
        {
            return true;
        }

        return HasExactlyOneSeparator(value, UserPrincipalSeparator);
    }


    /// <summary>
    /// Tells whether a value is split in two by exactly one occurrence of a separator, with
    /// something on each side and no occurrence of the other separator.
    /// </summary>
    private static bool HasExactlyOneSeparator(string value, char separator)
    {
        char other = AccountSeparator;

        if (separator == AccountSeparator)
        {
            other = UserPrincipalSeparator;
        }

        if (value.Contains(other, StringComparison.Ordinal))
        {
            return false;
        }

        string[] parts = value.Split(separator);

        if (parts.Length != 2)
        {
            return false;
        }

        return parts[0].Length > 0 && parts[1].Length > 0;
    }

    #endregion
}


/// <summary>The outcome of checking a name somebody typed.</summary>
public class DisplayNameValidationResult
{
    #region Construction

    /// <summary>Creates the result with the trimmed value and the problem found, if any.</summary>
    public DisplayNameValidationResult(string displayName, string? error)
    {
        DisplayName = displayName;
        Error = error;
    }

    #endregion


    #region State

    /// <summary>The name with leading and trailing whitespace removed.</summary>
    public string DisplayName { get; }

    /// <summary>The message to show, or null when the name is acceptable.</summary>
    public string? Error { get; }

    /// <summary>Whether the name may be stored.</summary>
    public bool IsValid
    {
        get
        {
            return Error is null;
        }
    }

    #endregion
}
