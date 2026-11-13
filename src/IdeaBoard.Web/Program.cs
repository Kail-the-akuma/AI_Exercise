using IdeaBoard.App;
using IdeaBoard.App.Configuration;
using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

// This is the host that gets published. It knows how to talk to Entra ID and nothing else:
// every page, service and entity lives in IdeaBoard.App, which is the only project this one
// references. That single reference is what keeps local identity code out of production.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The Windows Event Log is the only sink on the server. The event source is created once
// by an administrator during provisioning, never by the application, because letting the
// application create it would mean running the pool with permissions it should not hold.
if (!builder.Environment.IsDevelopment() && OperatingSystem.IsWindows())
{
    EventLogSettings eventLogSettings = new EventLogSettings
    {
        SourceName = "IdeaBoard",
    };

    builder.Logging.AddEventLog(eventLogSettings);
}

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration, ConfigurationKeys.AzureAdSection);

// Inbound claim mapping is set here on purpose rather than left at its default. With it
// on, the short OpenID Connect claim names are rewritten into long schemas.microsoft.com
// URIs, and that default has changed between versions. The whole local-and-production
// arrangement rests on both sides seeing the same claim types, so this is not a setting to
// inherit: a silent rename of the object identifier claim would show up as every sign-in
// creating a brand new person.
builder.Services.Configure<OpenIdConnectOptions>(
    OpenIdConnectDefaults.AuthenticationScheme,
    options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = IdeaBoardClaimTypes.DisplayName;
    });

builder.Services.AddIdeaBoard(builder.Configuration, builder.Environment);

builder.Services
    .AddRazorPages()
    .AddMicrosoftIdentityUI();

WebApplication app = builder.Build();

app.UseIdeaBoard();

app.MapRazorPages();
app.MapControllers();

app.Run();

// Needed so a test can start this host in process. Nothing in this solution's end to end
// suite starts IdeaBoard.Web, so without that test the composition root that actually gets
// published would be executed by nothing until the day it is published.
// This is not test authentication code: a public entry point asserts no identity and
// bypasses nothing.
public partial class Program
{
}
