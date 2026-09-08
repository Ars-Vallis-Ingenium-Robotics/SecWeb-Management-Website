using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SecWeb.Bot;
using SecWeb.Components;
using SecWeb.Components.Account;
using SecWeb.Data;
using SecWeb.Services;

var builder =
    WebApplication.CreateBuilder(
        args);


// =============================================================
// BLAZOR
// =============================================================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();


builder.Services
    .AddCascadingAuthenticationState();


// =============================================================
// IDENTITY SUPPORT SERVICES
// =============================================================

builder.Services
    .AddScoped<
        IdentityRedirectManager>();


builder.Services
    .AddScoped<
        AuthenticationStateProvider,
        IdentityRevalidatingAuthenticationStateProvider>();


// =============================================================
// AUTHENTICATION
// =============================================================

builder.Services
    .AddAuthentication(
        options =>
        {
            options.DefaultScheme =
                IdentityConstants.ApplicationScheme;


            options.DefaultSignInScheme =
                IdentityConstants.ExternalScheme;
        })
    .AddIdentityCookies();


// =============================================================
// AUTHORIZATION
// =============================================================

builder.Services
    .AddAuthorization();


// =============================================================
// NGINX / REVERSE PROXY
// =============================================================
//
// In production, nginx receives the public HTTP/HTTPS request and
// forwards it to Kestrel on localhost.
//
// These settings let SecWeb use the original:
//     - Client IP address
//     - HTTP / HTTPS scheme
//
// nginx and SecWeb run on the same Linux server, so the connection
// to Kestrel comes from loopback. ASP.NET Core trusts loopback
// proxies by default.
//
// ForwardLimit = 1 because there is one reverse proxy:
// Internet -> nginx -> SecWeb
//

builder.Services
    .Configure<ForwardedHeadersOptions>(
        options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;


            options.ForwardLimit =
                1;
        });


// =============================================================
// DATABASE
// =============================================================

string connectionString =
    builder.Configuration
        .GetConnectionString(
            "DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");


builder.Services
    .AddDbContext<ApplicationDbContext>(
        options =>
            options.UseSqlServer(
                connectionString));


builder.Services
    .AddDatabaseDeveloperPageExceptionFilter();


// =============================================================
// ASP.NET CORE IDENTITY
// =============================================================

builder.Services
    .AddIdentityCore<ApplicationUser>(
        options =>
        {
            // -----------------------------------------------------
            // EMAIL CONFIRMATION
            // -----------------------------------------------------

            options.SignIn.RequireConfirmedAccount =
                true;


            // -----------------------------------------------------
            // LOCKOUT
            // -----------------------------------------------------

            options.Lockout.AllowedForNewUsers =
                true;


            options.Lockout.MaxFailedAccessAttempts =
                5;


            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(
                    15);


            // -----------------------------------------------------
            // IDENTITY SCHEMA VERSION
            // -----------------------------------------------------

            options.Stores.SchemaVersion =
                IdentitySchemaVersions.Version3;
        })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();


// =============================================================
// EMAIL CONFIGURATION
// =============================================================

builder.Services
    .Configure<EmailSettings>(
        builder.Configuration
            .GetSection(
                "Email"));


// =============================================================
// EMAIL SERVICES
// =============================================================

builder.Services
    .AddTransient<SmtpEmailSender>();


builder.Services
    .AddTransient<
        IEmailSender<ApplicationUser>>(
            services =>
                services.GetRequiredService<
                    SmtpEmailSender>());


builder.Services
    .AddTransient<
        ILoginCodeEmailSender>(
            services =>
                services.GetRequiredService<
                    SmtpEmailSender>());


builder.Services
    .AddTransient<
        IAccountAdministrationEmailSender>(
            services =>
                services.GetRequiredService<
                    SmtpEmailSender>());


// =============================================================
// HTTP CLIENT
// =============================================================
//
// Used for Discord OAuth.
//

builder.Services
    .AddHttpClient();


// =============================================================
// SECWEB SERVICES
// =============================================================

// Meeting task uploads are stored outside wwwroot so they cannot
// be downloaded without SecWeb authorization.

builder.Services
    .AddSingleton<MeetingFileStorageService>();


builder.Services
    .AddScoped<MeetingService>();


builder.Services
    .AddScoped<ProjectService>();


builder.Services
    .AddScoped<WorkLogService>();


builder.Services
    .AddScoped<DiscordAccountLinkService>();


// =============================================================
// CHIPY CONFIGURATION
// =============================================================

builder.Services
    .Configure<ChipyOptions>(
        builder.Configuration
            .GetSection(
                "Chipy"));


// =============================================================
// CHIPY SERVICES
// =============================================================

builder.Services
    .AddSingleton<ChipyReactionService>();


builder.Services
    .AddHostedService<ChipyBotService>();


// =============================================================
// BUILD APPLICATION
// =============================================================

var app =
    builder.Build();


// =============================================================
// NGINX / FORWARDED HEADERS MIDDLEWARE
// =============================================================
//
// This must run before HSTS, HTTPS redirection, authentication,
// authorization, and any other middleware that needs to know the
// original request scheme or client address.
//

app.UseForwardedHeaders();


// =============================================================
// DATABASE SEEDING
// =============================================================

using (
    IServiceScope scope =
        app.Services.CreateScope())
{
    await IdentitySeeder
        .SeedAsync(
            scope.ServiceProvider);
}


// =============================================================
// DEVELOPMENT / PRODUCTION
// =============================================================

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);


    app.UseHsts();
}


// =============================================================
// STATUS PAGES
// =============================================================

app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);


// =============================================================
// HTTPS
// =============================================================

app.UseHttpsRedirection();


// =============================================================
// AUTHENTICATION / AUTHORIZATION
// =============================================================

app.UseAuthentication();

app.UseAuthorization();


// =============================================================
// ANTIFORGERY
// =============================================================

app.UseAntiforgery();


// =============================================================
// DISCORD ACCOUNT LINK — START
// =============================================================
//
// Browser requests:
//
// /account/manage/discord/link
//
// SecWeb creates the Discord OAuth URL and redirects the browser.
//

app.MapGet(
    "/account/manage/discord/link",

    (
        HttpContext context,
        DiscordAccountLinkService discordLinkService) =>
    {
        // ---------------------------------------------------------
        // CURRENT SECWEB USER
        // ---------------------------------------------------------

        string? userId =
            context.User
                .FindFirstValue(
                    ClaimTypes.NameIdentifier);


        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return Results.Redirect(
                "/Account/Login");
        }


        try
        {
            // -----------------------------------------------------
            // CREATE DISCORD OAUTH URL
            // -----------------------------------------------------

            string authorizationUrl =
                discordLinkService
                    .CreateAuthorizationUrl(
                        userId);


            // -----------------------------------------------------
            // REDIRECT BROWSER TO DISCORD
            // -----------------------------------------------------

            return Results.Redirect(
                authorizationUrl);
        }
        catch (Exception exception)
        {
            // -----------------------------------------------------
            // CONFIGURATION ERROR
            // -----------------------------------------------------

            string returnUrl =
                QueryHelpers.AddQueryString(
                    "/Account/Manage/Discord",

                    new Dictionary<string, string?>
                    {
                        ["discordStatus"] =
                            "error",

                        ["message"] =
                            exception.Message
                    });


            return Results.Redirect(
                returnUrl);
        }
    })
    .RequireAuthorization();


// =============================================================
// DISCORD ACCOUNT LINK — CALLBACK
// =============================================================
//
// Discord redirects the browser back to this endpoint after
// authorization.
//

app.MapGet(
    "/account/manage/discord/callback",

    async (
        HttpContext context,
        DiscordAccountLinkService discordLinkService) =>
    {
        // ---------------------------------------------------------
        // CURRENT SECWEB USER
        // ---------------------------------------------------------

        string? userId =
            context.User
                .FindFirstValue(
                    ClaimTypes.NameIdentifier);


        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return Results.Redirect(
                "/Account/Login");
        }


        // ---------------------------------------------------------
        // CHECK WHETHER USER CANCELLED
        // ---------------------------------------------------------

        string? discordError =
            context.Request
                .Query["error"]
                .FirstOrDefault();


        if (!string.IsNullOrWhiteSpace(
            discordError))
        {
            return Results.Redirect(
                "/Account/Manage/Discord?discordStatus=cancelled");
        }


        // ---------------------------------------------------------
        // GET DISCORD AUTHORIZATION CODE
        // ---------------------------------------------------------

        string? code =
            context.Request
                .Query["code"]
                .FirstOrDefault();


        // ---------------------------------------------------------
        // GET PROTECTED OAUTH STATE
        // ---------------------------------------------------------

        string? state =
            context.Request
                .Query["state"]
                .FirstOrDefault();


        if (string.IsNullOrWhiteSpace(
                code) ||
            string.IsNullOrWhiteSpace(
                state))
        {
            return Results.Redirect(
                "/Account/Manage/Discord?discordStatus=error&message=Discord%20did%20not%20return%20a%20valid%20authorization.");
        }


        // ---------------------------------------------------------
        // COMPLETE DISCORD ACCOUNT LINK
        // ---------------------------------------------------------

        DiscordLinkResult result =
            await discordLinkService
                .CompleteLinkAsync(
                    userId,
                    code,
                    state);


        // ---------------------------------------------------------
        // SUCCESS
        // ---------------------------------------------------------

        if (result.Succeeded)
        {
            return Results.Redirect(
                "/Account/Manage/Discord?discordStatus=linked");
        }


        // ---------------------------------------------------------
        // FAILURE
        // ---------------------------------------------------------

        string failureUrl =
            QueryHelpers.AddQueryString(
                "/Account/Manage/Discord",

                new Dictionary<string, string?>
                {
                    ["discordStatus"] =
                        "error",

                    ["message"] =
                        result.Message
                });


        return Results.Redirect(
            failureUrl);
    })
    .RequireAuthorization();


// =============================================================
// DISCORD ACCOUNT — DISCONNECT
// =============================================================
//
// This is POST rather than GET because it modifies the user's
// account.
//

app.MapPost(
    "/account/manage/discord/disconnect",

    async (
        HttpContext context,
        IAntiforgery antiforgery,
        DiscordAccountLinkService discordLinkService) =>
    {
        // ---------------------------------------------------------
        // VERIFY ANTIFORGERY TOKEN
        // ---------------------------------------------------------

        try
        {
            await antiforgery
                .ValidateRequestAsync(
                    context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(
                "The Discord disconnect request could not be verified.");
        }


        // ---------------------------------------------------------
        // CURRENT SECWEB USER
        // ---------------------------------------------------------

        string? userId =
            context.User
                .FindFirstValue(
                    ClaimTypes.NameIdentifier);


        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return Results.Redirect(
                "/Account/Login");
        }


        // ---------------------------------------------------------
        // DISCONNECT
        // ---------------------------------------------------------

        bool disconnected =
            await discordLinkService
                .DisconnectAsync(
                    userId);


        if (!disconnected)
        {
            string failureUrl =
                QueryHelpers.AddQueryString(
                    "/Account/Manage/Discord",

                    new Dictionary<string, string?>
                    {
                        ["discordStatus"] =
                            "error",

                        ["message"] =
                            "SecWeb could not disconnect the Discord account."
                    });


            return Results.Redirect(
                failureUrl);
        }


        // ---------------------------------------------------------
        // RETURN TO DISCORD SETTINGS
        // ---------------------------------------------------------

        return Results.Redirect(
            "/Account/Manage/Discord?discordStatus=disconnected");
    })
    .RequireAuthorization();


// =============================================================
// MEETING TASK FILE DOWNLOADS
// =============================================================

app.MapMeetingFileEndpoints();


// =============================================================
// STATIC ASSETS
// =============================================================

app.MapStaticAssets();


// =============================================================
// BLAZOR
// =============================================================

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


// =============================================================
// IDENTITY ENDPOINTS
// =============================================================

app.MapAdditionalIdentityEndpoints();


// =============================================================
// RUN SECWEB + CHIPY
// =============================================================

app.Run();