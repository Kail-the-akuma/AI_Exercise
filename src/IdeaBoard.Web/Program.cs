using IdeaBoard.App;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.Logging.EventLog;

// This is the host that gets published. It knows how identity arrives and nothing else:
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

// Identity comes from the web server, which negotiates it and attaches it to every
// request. There is no handler to register and no authority to contact: the scheme named
// here is the one the server provides, and the application only consumes it.
//
// The constant comes from the IIS integration namespace rather than from the in-process
// server one. The two carry the SAME scheme name, so this is not a hosting model decision
// and does not have to change if the hosting model does. This one is the constant the
// Windows authentication guidance names, and the other did not resolve without pulling in
// a package this host has no other reason to carry.
//
// That is the whole of the change away from a federated sign-in, and it has a consequence
// worth stating because it is invisible in this file. The published host no longer has any
// network dependency at startup or on the first request. Previously a wrong or unreachable
// authority turned the first request into a server error with nothing to explain it, and
// there is now nothing left that can do that.
//
// What replaced the configuration check this host used to have is in the site's web.config,
// which is also where it can go wrong, out of reach of anything here. Anonymous
// authentication being left on is the failure that matters: the server would then let
// unidentified requests through and nothing in this process would notice.
builder.Services.AddAuthentication(IISDefaults.AuthenticationScheme);

builder.Services.AddIdeaBoard(builder.Configuration, builder.Environment);

builder.Services.AddRazorPages();

WebApplication app = builder.Build();

app.UseIdeaBoard();

app.MapRazorPages();

// There is deliberately no MapControllers. It was here only to serve the sign-in and
// sign-out endpoints of the federated sign-in UI, and this solution has no controller of
// its own: verified by searching, not assumed.

app.Run();

// Needed so a test can start this host in process. Nothing in this solution's end to end
// suite starts IdeaBoard.Web, so without that test the composition root that actually gets
// published would be executed by nothing until the day it is published.
// This is not test authentication code: a public entry point asserts no identity and
// bypasses nothing.
public partial class Program
{
}
