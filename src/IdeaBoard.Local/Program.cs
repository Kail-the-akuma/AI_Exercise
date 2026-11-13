using IdeaBoard.App;
using IdeaBoard.Local;
using IdeaBoard.LocalIdentity;

// This host exists so the application can be run and looked at on a developer machine. It
// is never published: IsPublishable is false, and the published host does not reference it.
// It composes the same pipeline as production and differs in one place only, which is the
// authentication scheme.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LocalPeopleOptions>(
    builder.Configuration.GetSection(LocalPeopleOptions.SectionName));

builder.Services.AddLocalIdentity<SelectedPersonIdentitySource>();

builder.Services.AddIdeaBoard(builder.Configuration, builder.Environment);

builder.Services.AddRazorPages();

WebApplication app = builder.Build();

// Nobody chosen yet means the handler reports no identity, which the fallback policy turns
// into a refusal. Sending the person to the picker is this host's job, because the shared
// handler takes no decisions of its own.
app.Use(async (context, next) =>
{
    bool isPickerRequest = context.Request.Path.StartsWithSegments("/pessoa");

    // Static assets are not gated by the picker. They sit under the library's content
    // prefix, they carry nothing personal, and redirecting them would mean a page served
    // right after a person is chosen could still be missing its stylesheet.
    bool isStaticAsset = context.Request.Path.StartsWithSegments("/_content");

    bool hasChosen = context.Request.Cookies.ContainsKey(SelectedPersonIdentitySource.CookieName);

    if (!isPickerRequest && !isStaticAsset && !hasChosen)
    {
        context.Response.Redirect("/pessoa");
        return;
    }

    await next();
});

app.UseIdeaBoard();

app.MapRazorPages();

app.Run();
