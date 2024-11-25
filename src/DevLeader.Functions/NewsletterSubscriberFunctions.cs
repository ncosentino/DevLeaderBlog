using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using Stripe;

using System.Threading;

namespace DevLeader.Functions;

public class NewsletterSubscriberFunctions(
    ILogger<NewsletterSubscriberFunctions> logger,
    CustomerService customerService,
    Auth0ManagementApiAccessor auth0ManagementApiAccessor,
    NewsletterConfig newsletterConfig)
{
    private static readonly Action<ILogger, Exception> LogStripeException = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, nameof(NewsletterSubscriberFunctions)),
        "Error processing webhook");

    [Function("NewsletterSubscriber")]
    public async Task Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);

        using var reader = new StreamReader(httpRequest.Body);
        var json = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            var stripeEvent = EventUtility.ParseEvent(json);

            if (stripeEvent.Type == EventTypes.CustomerSubscriptionCreated)
            {
                var subscription = (Subscription)stripeEvent.Data.Object;
                await CreateNewsletterSubscriberAsync(
                    subscription!,
                    cancellationToken);
            }
            else if (stripeEvent.Type == EventTypes.CustomerSubscriptionResumed)
            {
                var subscription = (Subscription)stripeEvent.Data.Object;
                await ResumeNewsletterSubscriberAsync(
                    subscription!,
                    cancellationToken);
            }
            else if (stripeEvent.Type is
                EventTypes.CustomerSubscriptionPaused or
                EventTypes.CustomerSubscriptionDeleted)
            {
                var subscription = (Subscription)stripeEvent.Data.Object;
                await PauseNewsletterSubscriberAsync(
                    subscription!,
                    cancellationToken);
            }
        }
        catch (StripeException ex)
        {
            LogStripeException(logger, ex);
        }
    }

    private async Task CreateNewsletterSubscriberAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Creating newsletter subscriber for Stripe subscription ID {SubscriptionId}...",
            subscription.Id);

        var customer = await customerService.GetAsync(
            subscription.CustomerId,
            cancellationToken: cancellationToken);
        if (customer == null)
        {
            logger.LogWarning(
                "Could not create newsletter subscriber. " +
                "Customer not found for Stripe subscription ID {SubscriptionId}",
                subscription.Id);
            return;
        }

        logger.LogInformation(
            "Creating newsletter subscriber for Stripe subscription ID {SubscriptionId} " +
            "and customer ID {CustomerId}...",
            subscription.Id,
            customer.Id);

        using var auth0ManagementApi = await auth0ManagementApiAccessor.CreateManagementApiClientAsync(
            cancellationToken);

        var email = GetCustomerEmail(customer);
        var user = await GetAuth0UserForStripeCustomerAsync(
            auth0ManagementApi,
            customer,
            cancellationToken);

        if (user is null)
        {
            logger.LogInformation(
                "Creating Auth0 user for newsletter subscriber with email {Email}...",
                email);
            user = await auth0ManagementApi.Users.CreateAsync(
                new UserCreateRequest
                {
                    Email = email,
                    //UserName = email,
                    Password = Guid.NewGuid().ToString("N") + "!",
                    FullName = customer.Name,
                    PhoneNumber = customer.Phone,
                    AppMetadata = new
                    {
                        StripeCustomerId = customer.Id,
                        StripeSubscriptionId = subscription.Id,
                        StripeCustomerCreatedDate = customer.Created
                    },
                    Connection = newsletterConfig.Auth0ConnectionName,
                },
                cancellationToken);

            logger.LogInformation(
                "Updating Stripe customer {CustomerId} with Auth0 user ID {Auth0UserId}...",
                customer.Id,
                user.UserId);
            await customerService.UpdateAsync(
                customer.Id,
                new CustomerUpdateOptions()
                {
                    Metadata = new Dictionary<string, string>
                    {
                        ["Auth0UserId"] = user.UserId
                    },
                },
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Resetting password for newsletter subscriber with email {Email}...",
                email);
            await auth0ManagementApiAccessor.ResetPasswordAsync(
                email,
                newsletterConfig.Auth0ConnectionName,
                cancellationToken);
        }

        logger.LogInformation(
            "Assigning newsletter subscriber role {RoleId} to Auth0 user {Auth0UserId}...",
            newsletterConfig.MemberRoleId,
            user.UserId);
        await auth0ManagementApi.Users.AssignRolesAsync(
            user.UserId,
            new AssignRolesRequest
            {
                Roles = [newsletterConfig.MemberRoleId]
            },
            cancellationToken);
    }

    private async Task ResumeNewsletterSubscriberAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Resuming newsletter subscription for Stripe subscription ID {SubscriptionId}...",
            subscription.Id);

        var customer = await customerService.GetAsync(
            subscription.CustomerId,
            cancellationToken: cancellationToken);
        if (customer is null)
        {
            logger.LogWarning(
                "Could not resume newsletter subscription for user. " +
                "Customer not found for Stripe subscription ID {SubscriptionId}",
                subscription.Id);
            return;
        }

        using var auth0ManagementApi = await auth0ManagementApiAccessor.CreateManagementApiClientAsync(
            cancellationToken);
        var user = await GetAuth0UserForStripeCustomerAsync(
            auth0ManagementApi,
            customer,
            cancellationToken);

        if (user is null)
        {
            logger.LogWarning(
                "Could not resume newsletter subscription for user. " +
                "User not found for Stripe customer ID {CustomerId}",
                customer.Id);
            return;
        }

        logger.LogInformation(
            "Assigning newsletter subscriber role {RoleId} to Auth0 user {Auth0UserId}...",
            newsletterConfig.MemberRoleId,
            user.UserId);
        await auth0ManagementApi.Users.AssignRolesAsync(
            user.UserId,
            new AssignRolesRequest
            {
                Roles = [newsletterConfig.MemberRoleId]
            },
            cancellationToken);
    }

    private async Task PauseNewsletterSubscriberAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Pausing newsletter subscription for Stripe subscription ID {SubscriptionId}...",
            subscription.Id);

        var customer = await customerService.GetAsync(
            subscription.CustomerId,
            cancellationToken: cancellationToken);
        if (customer is null)
        {
            logger.LogWarning(
                "Could not pause newsletter subscription for user. " +
                "Customer not found for Stripe subscription ID {SubscriptionId}",
                subscription.Id);
            return;
        }

        using var auth0ManagementApi = await auth0ManagementApiAccessor.CreateManagementApiClientAsync(
            cancellationToken);
        var user = await GetAuth0UserForStripeCustomerAsync(
            auth0ManagementApi,
            customer,
            cancellationToken);
        if (user is null)
        {
            logger.LogWarning(
                "Could not pause newsletter subscription for user. " +
                "User not found for Stripe customer ID {CustomerId}",
                customer.Id);
            return;
        }

        logger.LogInformation(
            "Removing newsletter subscriber role {RoleId} from Auth0 user {Auth0UserId}...",
            newsletterConfig.MemberRoleId,
            user.UserId);
        await auth0ManagementApi.Users.RemoveRolesAsync(
            user.UserId,
            new AssignRolesRequest
            {
                Roles = [newsletterConfig.MemberRoleId]
            },
            cancellationToken);
    }

    private static async Task<User?> GetAuth0UserForStripeCustomerAsync(
        ManagementApiClient auth0ManagementApi,
        Customer customer,
        CancellationToken cancellationToken)
    {
        if (customer.Metadata.TryGetValue("Auth0UserId", out var auth0UserId) &&
            !string.IsNullOrWhiteSpace(auth0UserId))
        {
            return await auth0ManagementApi.Users.GetAsync(
                auth0UserId,
                cancellationToken: cancellationToken);
        }

        var email = GetCustomerEmail(customer);
        return (await auth0ManagementApi.Users
            .GetUsersByEmailAsync(
                email,
                cancellationToken: cancellationToken))
            .FirstOrDefault();
    }

#pragma warning disable S3358 // Ternary operators should not be nested
    private static string GetCustomerEmail(Customer customer) =>
        customer.Email is not null
            ? customer.Email
            : customer.Livemode
                ? customer.Email!
                : $"nbcosentino+stripe+{customer.Id}@gmail.com";
#pragma warning restore S3358 // Ternary operators should not be nested
}
