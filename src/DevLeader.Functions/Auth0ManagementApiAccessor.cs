using Auth0.ManagementApi;

using RestSharp;

using static System.Net.WebRequestMethods;

namespace DevLeader.Functions;

public sealed class Auth0ManagementApiAccessor(
    IRestClient restClient,
    string clientId,
    string clientSecret,
    string audience,
    string apiEndpoint) :
    IDisposable
{
    private readonly SemaphoreSlim @lock = new(1);
    private Auth0Access? currentAccess;
    private bool disposed;

    public async Task<ManagementApiClient> CreateManagementApiClientAsync(
        CancellationToken cancellationToken)
    {
        var auth0ApiAccess = await GetAccessAsync(cancellationToken);
        var client = CreateManagementApiClient(auth0ApiAccess.AccessToken);
        return client;
    }

    public async Task ResetPasswordAsync(
        string email,
        string connection,
        CancellationToken cancellationToken)
    {
        var request = new RestRequest("dbconnections/change_password");
        request.AddHeader("content-type", "application/json");
        request.AddParameter(
            "application/json",
            "{" +
            $"\"client_id\": \"{clientId}\"," +
            $"\"email\": \"{email}\"," +
            $"\"connection\": \"{connection}\"" +
            "}",
            ParameterType.RequestBody);
        await restClient.PostAsync(request, cancellationToken);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        @lock.Dispose();
        disposed = true;
    }

    private ManagementApiClient CreateManagementApiClient(string accessToken) =>
        new(accessToken, new Uri(apiEndpoint));

    private async Task<Auth0Access> GetAccessAsync(
        CancellationToken cancellationToken)
    {
        await @lock
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            if (currentAccess is not null &&
                currentAccess.Expiration > DateTimeOffset.UtcNow)
            {
                return currentAccess;
            }

            var request = new RestRequest("oauth/token", Method.Post);
            request.AddHeader("content-type", "application/json");
            request.AddParameter(
                "application/json",
                "{" +
                $"\"client_id\":\"{clientId}\"," +
                $"\"client_secret\":\"{clientSecret}\"," +
                $"\"audience\":\"{audience}\"," +
                "\"grant_type\":\"client_credentials\"" +
                "}",
                ParameterType.RequestBody);
            var restResult = await restClient.PostAsync<AccessTokenRestResult>(
                request,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Result from Auth0 oauth call was null.");

            Auth0Access access = new(
                restResult.access_token,
                restResult.token_type,
                restResult.scope.Split(' '),
                DateTimeOffset.UtcNow.AddSeconds(restResult.expires_in));
            currentAccess = access;

            return access;
        }
        finally
        {
            @lock.Release();
        }
    }

#pragma warning disable IDE1006 // Naming Styles
    private sealed record AccessTokenRestResult(
        string access_token,
        string token_type,
        string scope,
        int expires_in);
#pragma warning restore IDE1006 // Naming Styles

    private sealed record Auth0Access(
        string AccessToken,
        string TokenType,
        IReadOnlyList<string> Scopes,
        DateTimeOffset Expiration);
}
