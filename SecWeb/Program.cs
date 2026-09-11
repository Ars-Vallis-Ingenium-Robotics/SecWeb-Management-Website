using System.Net;
using System.Security.Claims;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
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
// Robert's nginx server receives requests from the Internet and
// forwards them to SecWeb through localhost.
//
// Internet
//    |
//    v
// nginx
//    |
//    v
// 127.0.0.1:8124
//    |
//    v
// SecWeb
//
// Only local loopback proxies are trusted.
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


            options.KnownIPNetworks.Clear();

            options.KnownProxies.Clear();


            options.KnownProxies.Add(
                IPAddress.Loopback);


            options.KnownProxies.Add(
                IPAddress.IPv6Loopback);
        });


// =============================================================
// DATABASE CONNECTION STRING
// =============================================================

string connectionString =
    builder.Configuration
        .GetConnectionString(
            "DefaultConnection")

    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");


// =============================================================
// DATABASE PROVIDER
// =============================================================

if (builder.Environment.IsDevelopment())
{
    // =========================================================
    // WINDOWS DEVELOPMENT
    // =========================================================
    //
    // Visual Studio continues using the existing SQL Server
    // LocalDB database.
    //

    builder.Services
        .AddDbContext<ApplicationDbContext>(
            options =>
                options.UseSqlServer(
                    connectionString));
}
else
{
    // =========================================================
    // LINUX PRODUCTION
    // =========================================================
    //
    // Robert's Linux server uses PostgreSQL.
    //

    builder.Services
        .AddDbContext<PostgresApplicationDbContext>(
            options =>
                options.UseNpgsql(
                    connectionString));


    // ---------------------------------------------------------
    // APPLICATION DB CONTEXT ALIAS
    // ---------------------------------------------------------
    //
    // Existing services such as:
    //
    // MeetingService
    // ProjectService
    // WorkLogService
    //
    // request ApplicationDbContext.
    //
    // In production, give those services the PostgreSQL context.
    //

    builder.Services
        .AddScoped<ApplicationDbContext>(
            services =>
                services.GetRequiredService<
                    PostgresApplicationDbContext>());
}


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
// NGINX FORWARDED HEADERS
// =============================================================
//
// Must happen before HTTPS redirection and authentication.
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

app.MapGet(
    "/account/manage/discord/link",

    (
        HttpContext context,
        DiscordAccountLinkService discordLinkService) =>
    {
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
            string authorizationUrl =
                discordLinkService
                    .CreateAuthorizationUrl(
                        userId);


            return Results.Redirect(
                authorizationUrl);
        }
        catch (Exception exception)
        {
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

app.MapGet(
    "/account/manage/discord/callback",

    async (
        HttpContext context,
        DiscordAccountLinkService discordLinkService) =>
    {
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


        string? code =
            context.Request
                .Query["code"]
                .FirstOrDefault();


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


        DiscordLinkResult result =
            await discordLinkService
                .CompleteLinkAsync(
                    userId,
                    code,
                    state);


        if (result.Succeeded)
        {
            return Results.Redirect(
                "/Account/Manage/Discord?discordStatus=linked");
        }


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

app.MapPost(
    "/account/manage/discord/disconnect",

    async (
        HttpContext context,
        IAntiforgery antiforgery,
        DiscordAccountLinkService discordLinkService) =>
    {
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