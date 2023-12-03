using System.Net;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;
using OpenTelemetry.Metrics;
using Splitracker.Persistence;
using Splitracker.UI;
using Splitracker.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.GetSection("AzureAdB2C").Bind(options);
        options.ResponseType = OpenIdConnectResponseType.IdToken;
    }, options =>
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();
builder.Services.AddHealthChecks();

builder.Services.AddAuthorization(options =>
{
    // By default, all incoming requests will be authorized according to the default policy
    options.FallbackPolicy = options.DefaultPolicy;
});

// Observability
builder.Services.AddOpenTelemetry().WithMetrics(metrics =>
{
    metrics.AddPrometheusExporter();
    metrics.AddMeter(
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel",
        "System.Net.NameResolution",
        "System.Net.Http");
});

builder.Services.AddRazorPages();
builder.Services.AddMudServices();
builder.Services.AddServerSideBlazor()
    .AddMicrosoftIdentityConsentHandler();
#if DEBUG
builder.Services.AddSassCompiler();
#endif

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.All;
    logging.RequestHeaders.Add("X-Forwarded-For");
    logging.RequestHeaders.Add("X-Forwarded-Proto");
    logging.RequestHeaders.Add("X-Forwarded-Host");
    logging.MediaTypeOptions.AddText("application/javascript");
    logging.RequestBodyLogLimit = 4096;
    logging.ResponseBodyLogLimit = 4096;

});

builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddDataProtection()
    .SetApplicationName("Splitracker")
    .PersistKeysToRavenDb();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownNetworks.Add(new(IPAddress.Any, 0));
    options.KnownNetworks.Add(new(IPAddress.IPv6Any, 0));
});

builder.Services
    .AddSplitrackerUi()
    .AddCascadingAuthenticationState();

var app = builder.Build();

app.UseHttpLogging();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseForwardedHeaders();
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseForwardedHeaders();
    IdentityModelEventSource.ShowPII = true;
}

app.UseHttpsRedirection();
app.UseAuthentication();

app.UseStaticFiles();
app.MapPrometheusScrapingEndpoint().AllowAnonymous();
app.UseRouting();

app.UseAuthorization();

app.MapHealthChecks("/healthz").AllowAnonymous();
app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
