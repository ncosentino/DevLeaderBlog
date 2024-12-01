using Blazor.Analytics;

using Blazored.Toast;

using HealthChecks.UI.Client;

using LinkDotNet.Blog.Web.Analytics;
using LinkDotNet.Blog.Web.Authentication.Dummy;
using LinkDotNet.Blog.Web.Authentication.OpenIdConnect;
using LinkDotNet.Blog.Web.RegistrationExtensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Threading.Tasks;

namespace LinkDotNet.Blog.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        RegisterServices(builder);

        await using var app = builder.Build();
        ConfigureApp(app);

        await app.RunAsync();
    }

    private static void RegisterServices(WebApplicationBuilder builder)
    {
        builder.Services
            .AddHostingServices()
            .AddConfiguration()
            .AddRateLimiting()
            .AddApplicationServices()
            .AddStorageProvider(builder.Configuration)
            .AddImageUploadProvider(builder.Configuration)
            .AddBlazoredToast()
            .AddBlazoriseWithBootstrap()
            .AddResponseCompression()
            .AddHealthCheckSetup();

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.UseDummyAuthentication();
            builder.Services.UseDummyAnalytics();
        }
        else
        {
            builder.Logging.AddApplicationInsights(configureTelemetryConfiguration: (config) =>
                config.ConnectionString = builder.Configuration.GetConnectionString("APPLICATIONINSIGHTS_CONNECTION_STRING"),
                configureApplicationInsightsLoggerOptions: (options) =>
                {
                }
            );

            builder.Services.UseAuthentication();
            builder.Services.AddGoogleAnalytics("G-F9P4TGM7EZ");
        }
    }

    private static void ConfigureApp(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseResponseCompression();
        app.UseHttpsRedirection();
        app.MapStaticAssets();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        })
        .RequireAuthorization();

        app.UseRouting();

        app.UseUserCulture();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseRateLimiter();
        app.MapControllers();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
        app.MapFallbackToPage("/tags/{tag}", "/_Host");
        app.MapFallbackToPage("/search/{searchTerm}", "/_Host");
    }
}
