using Auth0.ManagementApi;

using DevLeader.Functions;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using RestSharp;

using Stripe;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddSingleton<IStripeClient>(new StripeClient(
    apiKey: Environment.GetEnvironmentVariable("STRIPE_API_KEY")));
builder.Services.AddSingleton<SubscriptionService>();
builder.Services.AddSingleton<CustomerService>();
builder.Services.AddSingleton(new NewsletterConfig(
    MemberRoleId: Environment.GetEnvironmentVariable("NEWSLETTER_CONFIG_MEMBER_ROLE_ID")
        ?? throw new InvalidOperationException("No value set for NEWSLETTER_CONFIG_MEMBER_ROLE_ID."),
    Auth0ConnectionName: Environment.GetEnvironmentVariable("NEWSLETTER_CONFIG_AUTH0_CONNECTION_NAME")
        ?? throw new InvalidOperationException("No value set for NEWSLETTER_CONFIG_AUTH0_CONNECTION_NAME.")));

#pragma warning disable CA2000 // Dispose objects before losing scope
builder.Services.AddSingleton(new Auth0ManagementApiAccessor(
    restClient: new RestClient(
        baseUrl: new Uri(Environment.GetEnvironmentVariable("AUTH0_DOMAIN")
            ?? throw new InvalidOperationException("No value set for AUTH0_DOMAIN.")),
        useClientFactory: true),
    clientId: Environment.GetEnvironmentVariable("AUTH0_CLIENT_ID")
        ?? throw new InvalidOperationException("No value set for AUTH0_CLIENT_ID."),
    clientSecret: Environment.GetEnvironmentVariable("AUTH0_CLIENT_SECRET")
        ?? throw new InvalidOperationException("No value set for AUTH0_CLIENT_SECRET."),
    audience: Environment.GetEnvironmentVariable("AUTH0_AUDIENCE")
        ?? throw new InvalidOperationException("No value set for AUTH0_AUDIENCE."),
    apiEndpoint: Environment.GetEnvironmentVariable("AUTH0_API_ENDPOINT")
        ?? throw new InvalidOperationException("No value set for AUTH0_API_ENDPOINT.")));
#pragma warning restore CA2000 // Dispose objects before losing scope

builder.ConfigureFunctionsWebApplication();

await builder.Build().RunAsync();
