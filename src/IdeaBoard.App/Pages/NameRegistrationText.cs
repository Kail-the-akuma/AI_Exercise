namespace IdeaBoard.App.Pages;

/// <summary>
/// The words on the name registration page.
///
/// They are kept apart from <see cref="UiText"/> deliberately. That class holds the strings
/// the functional specification fixes, and its value is that it can be checked against that
/// document line by line. This page is not in the specification: it exists because identity
/// moved to Windows authentication and a name now has to be asked for. Mixing these in
/// would quietly make the specification look like it says things it does not.
///
/// Nothing here names a product either. That choice is still open and a placeholder closes
/// it by sitting there long enough.
/// </summary>
public static class NameRegistrationText
{
    #region Page

    /// <summary>Heading and document title.</summary>
    public const string PageTitle = "Escreva o seu nome";

    /// <summary>Says why the question is being asked at all.</summary>
    public const string Explanation =
        "Não foi possível obter o seu nome automaticamente. Escreva-o para continuar.";

    /// <summary>Label of the single field.</summary>
    public const string FieldLabel = "Nome";

    /// <summary>Button that stores the name.</summary>
    public const string SubmitButton = "Guardar";

    /// <summary>
    /// Says where the name will be seen, before it is typed rather than after.
    /// </summary>
    public const string VisibilityWarning =
        "Este nome fica visível para os seus colegas, como autor das suas ideias e na lista de quem votou.";

    #endregion
}
