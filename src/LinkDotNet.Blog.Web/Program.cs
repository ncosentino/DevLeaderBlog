using Blazored.Toast;
using LinkDotNet.Blog.Web.Authentication.OpenIdConnect;
using LinkDotNet.Blog.Web.Authentication.Dummy;
using LinkDotNet.Blog.Web.Features;
using LinkDotNet.Blog.Web.Features.Layout;
using LinkDotNet.Blog.Web.RegistrationExtensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LinkDotNet.Blog.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorComponents()
            .AddServerComponents();
        var appConfiguration = AppConfigurationFactory.Create(builder.Configuration);
        builder.Services.AddSingleton(_ => appConfiguration);
        builder.Services.AddBlazoredToast();
        builder.Services.RegisterServices();
        builder.Services.AddStorageProvider(builder.Configuration);
        builder.Services.AddResponseCompression();
        builder.Services.AddBackgroundServices();
        builder.Services.AddControllers();

        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("Database");

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.UseDummyAuthentication();
        }
        else
        {
            builder.Services.UseAuthentication();
        }

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseStaticFiles();

        app.MapRazorComponents<App>()
            .AddServerRenderMode();
        app.MapControllers();

        app.Run();
    }
}
