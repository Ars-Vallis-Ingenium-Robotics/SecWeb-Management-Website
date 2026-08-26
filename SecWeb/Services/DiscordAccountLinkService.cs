using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecWeb.Bot;
using SecWeb.Data;

namespace SecWeb.Services
{
    public sealed class DiscordAccountLinkService
    {
        // =========================================================
        // DEPENDENCIES
        // =========================================================

        private readonly ApplicationDbContext _db;

        private readonly IHttpClientFactory _httpClientFactory;

        private readonly ChipyOptions _options;

        private readonly IDataProtector _stateProtector;


        // =========================================================
        // DISCORD ENDPOINTS
        // =========================================================

        private const string DiscordAuthorizeUrl =
            "https://discord.com/oauth2/authorize";


        private const string DiscordTokenUrl =
            "https://discord.com/api/v10/oauth2/token";


        private const string DiscordCurrentUserUrl =
            "https://discord.com/api/v10/users/@me";


        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public DiscordAccountLinkService(
            ApplicationDbContext db,
            IHttpClientFactory httpClientFactory,
            IOptions<ChipyOptions> options,
            IDataProtectionProvider dataProtectionProvider)
        {
            _db =
                db;


            _httpClientFactory =
                httpClientFactory;


            _options =
                options.Value;


            // Data Protection encrypts and signs the OAuth state.
            //
            // This prevents somebody from changing the SecWeb
            // user ID contained inside the OAuth request.
            _stateProtector =
                dataProtectionProvider.CreateProtector(
                    "SecWeb.DiscordAccountLink.State.v1");
        }


        // =========================================================
        // CREATE DISCORD AUTHORIZATION URL
        // =========================================================

        public string CreateAuthorizationUrl(
            string secWebUserId)
        {
            ValidateConfiguration();


            DiscordOAuthState state =
                new()
                {
                    SecWebUserId =
                        secWebUserId,

                    Nonce =
                        Guid.NewGuid()
                            .ToString("N"),

                    ExpiresAtUtc =
                        DateTimeOffset.UtcNow
                            .AddMinutes(10)
                };


            string serializedState =
                JsonSerializer.Serialize(
                    state);


            string protectedState =
                _stateProtector.Protect(
                    serializedState);


            Dictionary<string, string?> query =
                new()
                {
                    ["client_id"] =
                        _options.ClientId,

                    ["response_type"] =
                        "code",

                    ["redirect_uri"] =
                        _options.RedirectUri,

                    ["scope"] =
                        "identify",

                    ["state"] =
                        protectedState
                };


            return QueryHelpers.AddQueryString(
                DiscordAuthorizeUrl,
                query);
        }


        // =========================================================
        // COMPLETE ACCOUNT LINK
        // =========================================================

        public async Task<DiscordLinkResult>
            CompleteLinkAsync(
                string currentSecWebUserId,
                string code,
                string protectedState)
        {
            ValidateConfiguration();


            // -----------------------------------------------------
            // VALIDATE STATE
            // -----------------------------------------------------

            DiscordOAuthState? state;


            try
            {
                string serializedState =
                    _stateProtector.Unprotect(
                        protectedState);


                state =
                    JsonSerializer.Deserialize<
                        DiscordOAuthState>(
                            serializedState);
            }
            catch
            {
                return DiscordLinkResult.Failure(
                    "The Discord authorization request was invalid or expired.");
            }


            if (state == null)
            {
                return DiscordLinkResult.Failure(
                    "The Discord authorization request was invalid.");
            }


            // -----------------------------------------------------
            // MAKE SURE THE CALLBACK BELONGS TO THE SAME SECWEB USER
            // -----------------------------------------------------

            if (!string.Equals(
                    state.SecWebUserId,
                    currentSecWebUserId,
                    StringComparison.Ordinal))
            {
                return DiscordLinkResult.Failure(
                    "The Discord authorization does not belong to the currently logged-in SecWeb account.");
            }


            // -----------------------------------------------------
            // STATE EXPIRATION
            // -----------------------------------------------------

            if (state.ExpiresAtUtc <
                DateTimeOffset.UtcNow)
            {
                return DiscordLinkResult.Failure(
                    "The Discord authorization request expired. Please try linking your account again.");
            }


            // -----------------------------------------------------
            // EXCHANGE DISCORD CODE FOR TEMPORARY ACCESS TOKEN
            // -----------------------------------------------------

            DiscordTokenResponse? token =
                await ExchangeCodeAsync(
                    code);


            if (token == null ||
                string.IsNullOrWhiteSpace(
                    token.AccessToken))
            {
                return DiscordLinkResult.Failure(
                    "SecWeb could not complete the Discord authorization.");
            }


            // -----------------------------------------------------
            // GET DISCORD USER
            // -----------------------------------------------------

            DiscordUserResponse? discordUser =
                await GetDiscordUserAsync(
                    token.AccessToken);


            if (discordUser == null ||
                string.IsNullOrWhiteSpace(
                    discordUser.Id))
            {
                return DiscordLinkResult.Failure(
                    "SecWeb could not retrieve your Discord account.");
            }


            // -----------------------------------------------------
            // MAKE SURE THIS DISCORD ACCOUNT IS NOT LINKED
            // TO ANOTHER SECWEB ACCOUNT
            // -----------------------------------------------------

            bool alreadyLinked =
                await _db.Users
                    .AsNoTracking()
                    .AnyAsync(
                        user =>
                            user.DiscordUserId ==
                                discordUser.Id &&

                            user.Id !=
                                currentSecWebUserId);


            if (alreadyLinked)
            {
                return DiscordLinkResult.Failure(
                    "This Discord account is already linked to another SecWeb account.");
            }


            // -----------------------------------------------------
            // GET CURRENT SECWEB USER
            // -----------------------------------------------------

            ApplicationUser? secWebUser =
                await _db.Users
                    .FirstOrDefaultAsync(
                        user =>
                            user.Id ==
                            currentSecWebUserId);


            if (secWebUser == null)
            {
                return DiscordLinkResult.Failure(
                    "The SecWeb account could not be found.");
            }


            // -----------------------------------------------------
            // SAVE DISCORD ACCOUNT INFORMATION
            // -----------------------------------------------------

            secWebUser.DiscordUserId =
                discordUser.Id;


            secWebUser.DiscordUsername =
                discordUser.Username;


            secWebUser.DiscordDisplayName =
                !string.IsNullOrWhiteSpace(
                    discordUser.GlobalName)

                    ? discordUser.GlobalName

                    : discordUser.Username;


            secWebUser.DiscordAvatarHash =
                discordUser.Avatar;


            secWebUser.DiscordLinkedAtUtc =
                DateTimeOffset.UtcNow;


            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // The database also has a unique constraint on
                // DiscordUserId.
                //
                // This protects against two users attempting to
                // link the same Discord account simultaneously.

                return DiscordLinkResult.Failure(
                    "This Discord account is already linked to another SecWeb account.");
            }


            return DiscordLinkResult.Success(
                secWebUser.DiscordUsername ??
                "Discord User");
        }


        // =========================================================
        // GET CURRENT DISCORD LINK INFORMATION
        // =========================================================

        public async Task<DiscordLinkInfo?>
            GetLinkInfoAsync(
                string secWebUserId)
        {
            ApplicationUser? user =
                await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        user =>
                            user.Id ==
                            secWebUserId);


            if (user == null)
            {
                return null;
            }


            return new DiscordLinkInfo(
                user.DiscordUserId,
                user.DiscordUsername,
                user.DiscordDisplayName,
                user.DiscordAvatarHash,
                user.DiscordLinkedAtUtc);
        }


        // =========================================================
        // DISCONNECT DISCORD
        // =========================================================

        public async Task<bool>
            DisconnectAsync(
                string secWebUserId)
        {
            ApplicationUser? user =
                await _db.Users
                    .FirstOrDefaultAsync(
                        user =>
                            user.Id ==
                            secWebUserId);


            if (user == null)
            {
                return false;
            }


            user.DiscordUserId =
                null;


            user.DiscordUsername =
                null;


            user.DiscordDisplayName =
                null;


            user.DiscordAvatarHash =
                null;


            user.DiscordLinkedAtUtc =
                null;


            await _db.SaveChangesAsync();


            return true;
        }


        // =========================================================
        // EXCHANGE AUTHORIZATION CODE
        // =========================================================

        private async Task<DiscordTokenResponse?>
            ExchangeCodeAsync(
                string code)
        {
            HttpClient client =
                _httpClientFactory.CreateClient();


            // Discord supports authenticating the application
            // during the OAuth token exchange using the
            // application's Client ID and Client Secret.

            string clientCredentials =
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{_options.ClientId}:{_options.ClientSecret}"));


            using HttpRequestMessage request =
                new(
                    HttpMethod.Post,
                    DiscordTokenUrl);


            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Basic",
                    clientCredentials);


            request.Content =
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["grant_type"] =
                            "authorization_code",

                        ["code"] =
                            code,

                        ["redirect_uri"] =
                            _options.RedirectUri
                    });


            using HttpResponseMessage response =
                await client.SendAsync(
                    request);


            if (!response.IsSuccessStatusCode)
            {
                return null;
            }


            string json =
                await response.Content
                    .ReadAsStringAsync();


            return JsonSerializer.Deserialize<
                DiscordTokenResponse>(
                    json);
        }


        // =========================================================
        // GET DISCORD USER IDENTITY
        // =========================================================

        private async Task<DiscordUserResponse?>
            GetDiscordUserAsync(
                string accessToken)
        {
            HttpClient client =
                _httpClientFactory.CreateClient();


            using HttpRequestMessage request =
                new(
                    HttpMethod.Get,
                    DiscordCurrentUserUrl);


            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);


            using HttpResponseMessage response =
                await client.SendAsync(
                    request);


            if (!response.IsSuccessStatusCode)
            {
                return null;
            }


            string json =
                await response.Content
                    .ReadAsStringAsync();


            return JsonSerializer.Deserialize<
                DiscordUserResponse>(
                    json);
        }


        // =========================================================
        // CONFIGURATION VALIDATION
        // =========================================================

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(
                    _options.ClientId))
            {
                throw new InvalidOperationException(
                    "Chipy Discord ClientId is missing.");
            }


            if (string.IsNullOrWhiteSpace(
                    _options.ClientSecret))
            {
                throw new InvalidOperationException(
                    "Chipy Discord ClientSecret is missing.");
            }


            if (string.IsNullOrWhiteSpace(
                    _options.RedirectUri))
            {
                throw new InvalidOperationException(
                    "Chipy Discord RedirectUri is missing.");
            }
        }


        // =========================================================
        // INTERNAL OAUTH STATE
        // =========================================================

        private sealed class DiscordOAuthState
        {
            public string SecWebUserId { get; set; } =
                string.Empty;


            public string Nonce { get; set; } =
                string.Empty;


            public DateTimeOffset ExpiresAtUtc { get; set; }
        }


        // =========================================================
        // DISCORD TOKEN RESPONSE
        // =========================================================

        private sealed class DiscordTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } =
                string.Empty;


            [JsonPropertyName("token_type")]
            public string TokenType { get; set; } =
                string.Empty;


            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }


            [JsonPropertyName("scope")]
            public string Scope { get; set; } =
                string.Empty;
        }


        // =========================================================
        // DISCORD USER RESPONSE
        // =========================================================

        private sealed class DiscordUserResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } =
                string.Empty;


            [JsonPropertyName("username")]
            public string Username { get; set; } =
                string.Empty;


            [JsonPropertyName("global_name")]
            public string? GlobalName { get; set; }


            [JsonPropertyName("avatar")]
            public string? Avatar { get; set; }
        }
    }


    // =============================================================
    // LINK RESULT
    // =============================================================

    public sealed record DiscordLinkResult(
        bool Succeeded,
        string Message)
    {
        public static DiscordLinkResult Success(
            string username)
        {
            return new DiscordLinkResult(
                true,
                $"Discord account {username} was linked successfully.");
        }


        public static DiscordLinkResult Failure(
            string message)
        {
            return new DiscordLinkResult(
                false,
                message);
        }
    }


    // =============================================================
    // LINK INFORMATION
    // =============================================================

    public sealed record DiscordLinkInfo(
        string? DiscordUserId,
        string? DiscordUsername,
        string? DiscordDisplayName,
        string? DiscordAvatarHash,
        DateTimeOffset? DiscordLinkedAtUtc)
    {
        public bool IsLinked =>
            !string.IsNullOrWhiteSpace(
                DiscordUserId);
    }
}