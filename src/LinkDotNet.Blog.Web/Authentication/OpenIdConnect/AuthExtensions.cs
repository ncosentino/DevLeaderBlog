using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using System;
using System.Threading.Tasks;
using System.Web;

namespace LinkDotNet.Blog.Web.Authentication.OpenIdConnect;

public static class AuthExtensions
{
    public static void UseAuthentication(this IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        var authInformation = provider.GetRequiredService<IOptions<AuthInformation>>();

        services.Configure<CookiePolicyOptions>(options =>
        {
            options.CheckConsentNeeded = _ => false;
            options.MinimumSameSitePolicy = SameSiteMode.None;
        });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(authInformation.Value.Provider, options =>
        {
            options.Authority = $"https://{authInformation.Value.Domain}";
            options.ClientId = authInformation.Value.ClientId;
            options.ClientSecret = authInformation.Value.ClientSecret;

            options.ResponseType = "code";

            options.Scope.Clear();
            options.Scope.Add("openid");

            // Set the callback path, so Auth provider will call back to http://localhost:1234/callback
            // Also ensure that you have added the URL as an Allowed Callback URL in your Auth provider dashboard
            options.CallbackPath = new PathString("/callback");

            // Configure the Claims Issuer to be Auth provider
            options.ClaimsIssuer = authInformation.Value.Provider;

            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProviderForSignOut = async context => await HandleRedirect(authInformation.Value, context),
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
            options.AddPolicy("Member", policy => policy.RequireRole("Member"));
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ILoginManager, AuthLoginManager>();
    }

    private static Task HandleRedirect(AuthInformation auth, RedirectContext context)
    {
        var postLogoutUri = context.Properties.RedirectUri;
        if (!string.IsNullOrEmpty(postLogoutUri))
        {
            if (postLogoutUri.StartsWith('/'))
            {
                var request = context.Request;
                postLogoutUri = request.Scheme + "://" + request.Host + request.PathBase + postLogoutUri;
            }

            auth.LogoutUri = ReplaceQueryParameter(auth.LogoutUri, "returnTo", postLogoutUri);
        }

        context.Response.Redirect(auth.LogoutUri);
        context.HandleResponse();

        return Task.CompletedTask;
    }

    private static string ReplaceQueryParameter(string originalUrl, string parameterName, string parameterValue)
    {
        var uri = new Uri(originalUrl);

        var queryParams = HttpUtility.ParseQueryString(uri.Query);
        queryParams.Remove(parameterName);
        queryParams.Add(parameterName, parameterValue);

        var uriBuilder = new UriBuilder(uri)
        {
            Query = queryParams.ToString()
        };

        return uriBuilder.ToString();
    }
}
