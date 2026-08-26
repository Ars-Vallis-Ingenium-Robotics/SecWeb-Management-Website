using System.Collections.Concurrent;
using System.Text;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SecWeb.Data;
using SecWeb.Data.Models;
using SecWeb.Services;

namespace SecWeb.Bot
{
    public sealed class ChipyBotService
        : BackgroundService
    {
        // =========================================================
        // DEPENDENCIES
        // =========================================================

        private readonly IServiceScopeFactory _scopeFactory;

        private readonly ChipyReactionService _reactionService;

        private readonly ChipyOptions _options;

        private readonly ILogger<ChipyBotService> _logger;


        // =========================================================
        // DISCORD CLIENT
        // =========================================================

        private DiscordSocketClient? _client;

        private CancellationToken _stoppingToken;


        // =========================================================
        // ACTIVE INTERACTIVE COMMANDS
        // =========================================================
        //
        // Prevents one Discord user from opening multiple
        // project/activity selection menus at the same time.
        //

        private readonly ConcurrentDictionary<
            ulong,
            byte> _interactiveUsers =
                new();


        // =========================================================
        // SELECTION EMOJI
        // =========================================================
        //
        // These emoji are used for project selections.
        //

        private static readonly string[] SelectionEmojis =
        {
            "1️⃣",
            "2️⃣",
            "3️⃣",
            "4️⃣",
            "5️⃣",
            "6️⃣",
            "7️⃣",
            "8️⃣",
            "9️⃣",
            "🔟",
            "🇦",
            "🇧",
            "🇨",
            "🇩",
            "🇪",
            "🇫",
            "🇬",
            "🇭",
            "🇮",
            "🇯"
        };


        private const string CancelEmoji =
            "❌";


        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public ChipyBotService(
            IServiceScopeFactory scopeFactory,
            ChipyReactionService reactionService,
            IOptions<ChipyOptions> options,
            ILogger<ChipyBotService> logger)
        {
            _scopeFactory =
                scopeFactory;


            _reactionService =
                reactionService;


            _options =
                options.Value;


            _logger =
                logger;
        }


        // =========================================================
        // START CHIPY
        // =========================================================

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _stoppingToken =
                stoppingToken;


            // -----------------------------------------------------
            // CHECK REQUIRED CONFIGURATION
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(
                    _options.BotToken) ||
                _options.GuildId == 0 ||
                _options.ChannelId == 0)
            {
                _logger.LogWarning(
                    "Chipy is disabled because BotToken, GuildId, or ChannelId is missing.");

                return;
            }


            // -----------------------------------------------------
            // DISCORD CLIENT CONFIGURATION
            // -----------------------------------------------------
            //
            // MessageContent:
            //     Allows !log, !stop, !time and !all.
            //
            // GuildMessageReactions:
            //     Allows Chipy to receive emoji selections.
            //

            DiscordSocketConfig config =
                new()
                {
                    GatewayIntents =

                        GatewayIntents.Guilds |

                        GatewayIntents.GuildMessages |

                        GatewayIntents
                            .GuildMessageReactions |

                        GatewayIntents
                            .MessageContent,

                    MessageCacheSize =
                        200,

                    LogGatewayIntentWarnings =
                        true
                };


            _client =
                new DiscordSocketClient(
                    config);


            // -----------------------------------------------------
            // DISCORD EVENTS
            // -----------------------------------------------------

            _client.Log +=
                HandleDiscordLogAsync;


            _client.Ready +=
                HandleReadyAsync;


            _client.MessageReceived +=
                HandleMessageReceivedAsync;


            _client.ReactionAdded +=
                _reactionService
                    .HandleReactionAddedAsync;


            try
            {
                // -------------------------------------------------
                // CONNECT CHIPY TO DISCORD
                // -------------------------------------------------

                await _client.LoginAsync(
                    TokenType.Bot,
                    _options.BotToken);


                await _client.StartAsync();


                // Keep Chipy alive for as long as SecWeb is running.

                await Task.Delay(
                    Timeout.Infinite,
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal SecWeb shutdown.
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Chipy encountered a fatal Discord connection error.");
            }
            finally
            {
                if (_client != null)
                {
                    try
                    {
                        await _client.StopAsync();

                        await _client.LogoutAsync();
                    }
                    catch
                    {
                        // SecWeb is already shutting down.
                    }


                    _client.Dispose();

                    _client =
                        null;
                }
            }
        }


        // =========================================================
        // CHIPY READY
        // =========================================================

        private async Task HandleReadyAsync()
        {
            if (_client == null)
            {
                return;
            }


            _logger.LogInformation(
                "Chipy is online as {Username}.",
                _client.CurrentUser.Username);


            await _client.SetGameAsync(
                "tracking AVI Robotics hours");
        }


        // =========================================================
        // DISCORD LOGGING
        // =========================================================

        private Task HandleDiscordLogAsync(
            LogMessage message)
        {
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:

                    _logger.LogError(
                        message.Exception,
                        "{DiscordMessage}",
                        message.Message);

                    break;


                case LogSeverity.Warning:

                    _logger.LogWarning(
                        "{DiscordMessage}",
                        message.Message);

                    break;


                case LogSeverity.Info:

                    _logger.LogInformation(
                        "{DiscordMessage}",
                        message.Message);

                    break;


                default:

                    _logger.LogDebug(
                        "{DiscordMessage}",
                        message.Message);

                    break;
            }


            return Task.CompletedTask;
        }


        // =========================================================
        // RECEIVE MESSAGE
        // =========================================================

        private Task HandleMessageReceivedAsync(
            SocketMessage rawMessage)
        {
            // -----------------------------------------------------
            // ONLY NORMAL USER MESSAGES
            // -----------------------------------------------------

            if (rawMessage is not SocketUserMessage message)
            {
                return Task.CompletedTask;
            }


            // -----------------------------------------------------
            // IGNORE BOT MESSAGES
            // -----------------------------------------------------

            if (message.Author.IsBot)
            {
                return Task.CompletedTask;
            }


            // -----------------------------------------------------
            // ONLY ALLOW CHIPY IN THE CONFIGURED SERVER
            // -----------------------------------------------------

            if (message.Channel
                is not SocketGuildChannel guildChannel)
            {
                return Task.CompletedTask;
            }


            if (guildChannel.Guild.Id !=
                _options.GuildId)
            {
                return Task.CompletedTask;
            }


            // -----------------------------------------------------
            // ONLY ALLOW COMMANDS IN CHIPY'S CONFIGURED CHANNEL
            // -----------------------------------------------------

            if (message.Channel.Id !=
                _options.ChannelId)
            {
                return Task.CompletedTask;
            }


            string command =
                message.Content
                    .Trim()
                    .ToLowerInvariant();


            // -----------------------------------------------------
            // COMMAND HANDLING
            // -----------------------------------------------------

            switch (command)
            {
                case "!log":

                    _ =
                        HandleCommandSafelyAsync(
                            () =>
                                HandleLogAsync(
                                    message));

                    break;


                case "!stop":

                    _ =
                        HandleCommandSafelyAsync(
                            () =>
                                HandleStopAsync(
                                    message));

                    break;


                case "!time":

                    _ =
                        HandleCommandSafelyAsync(
                            () =>
                                HandleTimeAsync(
                                    message));

                    break;


                case "!all":

                    _ =
                        HandleCommandSafelyAsync(
                            () =>
                                HandleAllAsync(
                                    message));

                    break;
            }


            return Task.CompletedTask;
        }


        // =========================================================
        // COMMAND ERROR WRAPPER
        // =========================================================

        private async Task HandleCommandSafelyAsync(
            Func<Task> command)
        {
            try
            {
                await command();
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation or menu timeout.
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Chipy command failed.");
            }
        }


        // =========================================================
        // !LOG
        // =========================================================

        private async Task HandleLogAsync(
            SocketUserMessage message)
        {
            // -----------------------------------------------------
            // ONE ACTIVE MENU PER DISCORD USER
            // -----------------------------------------------------

            if (!_interactiveUsers.TryAdd(
                message.Author.Id,
                0))
            {
                await message.Channel
                    .SendMessageAsync(
                        "You already have an active Chipy selection. Finish or cancel it first.");

                return;
            }


            try
            {
                // -------------------------------------------------
                // GET LINKED SECWEB USER
                // -------------------------------------------------

                ApplicationUser? user =
                    await GetLinkedUserAsync(
                        message.Author.Id);


                if (user == null)
                {
                    await SendNotLinkedAsync(
                        message);

                    return;
                }


                // -------------------------------------------------
                // CHECK FOR ACTIVE TIMER
                // -------------------------------------------------

                List<WorkLog> activeTimers =
                    await GetActiveTimersAsync(
                        user.Id);


                if (activeTimers.Count > 0)
                {
                    StringBuilder existing =
                        new();


                    existing.AppendLine(
                        "**You already have an active timer.**");

                    existing.AppendLine();


                    foreach (
                        WorkLog timer
                        in activeTimers)
                    {
                        existing.AppendLine(
                            $"{timer.ProjectNameSnapshot} — {timer.Activity}");

                        existing.AppendLine(
                            $"Started: {DiscordTimestamp(timer.StartedAtUtc)}");
                    }


                    existing.AppendLine();

                    existing.Append(
                        "Use `!stop` before starting another timer.");


                    await message.Channel
                        .SendMessageAsync(
                            existing.ToString());


                    return;
                }


                // -------------------------------------------------
                // GET PROJECTS USER CAN LOG TIME TO
                // -------------------------------------------------

                List<Project> projects =
                    await GetLoggableProjectsAsync(
                        user.Id);


                if (projects.Count == 0)
                {
                    await message.Channel
                        .SendMessageAsync(
                            "You currently have no projects available for time logging.");

                    return;
                }


                List<SelectionOption<Project>>
                    projectOptions =
                        projects

                            .Select(
                                project =>
                                    new SelectionOption<Project>(
                                        project,
                                        project.Name))

                            .ToList();


                SelectionOption<Project>?
                    selectedProject =
                        await SelectFromReactionsAsync(
                            message,
                            "**Chipy — Select a Project**",
                            projectOptions);


                if (selectedProject == null)
                {
                    return;
                }


                // -------------------------------------------------
                // ACTIVITY OPTIONS
                // -------------------------------------------------

                List<SelectionOption<WorkActivity>>
                    activities =
                        new()
                        {
                            new(
                                WorkActivity.Meeting,
                                "Meeting"),

                            new(
                                WorkActivity.Booth,
                                "Booth"),

                            new(
                                WorkActivity.Working,
                                "Working"),

                            new(
                                WorkActivity.Volunteering,
                                "Volunteering")
                        };


                SelectionOption<WorkActivity>?
                    selectedActivity =
                        await SelectFromReactionsAsync(
                            message,
                            $"**{selectedProject.Label} — Select an Activity**",
                            activities);


                if (selectedActivity == null)
                {
                    return;
                }


                // -------------------------------------------------
                // START TIMER
                // -------------------------------------------------

                WorkLog log =
                    await StartTimerAsync(
                        user.Id,
                        selectedProject.Value.Id,
                        selectedActivity.Value);


                string displayName =
                    GetDisplayName(
                        user);


                StringBuilder response =
                    new();


                response.AppendLine(
                    $"**{displayName} started a timer.**");

                response.AppendLine();

                response.AppendLine(
                    $"**Project:** {log.ProjectNameSnapshot}");

                response.AppendLine(
                    $"**Activity:** {log.Activity}");

                response.AppendLine(
                    $"**Started:** {DiscordTimestamp(log.StartedAtUtc)}");

                response.AppendLine();

                response.Append(
                    "Use `!stop` when you finish.");


                await message.Channel
                    .SendMessageAsync(
                        response.ToString());
            }
            finally
            {
                _interactiveUsers.TryRemove(
                    message.Author.Id,
                    out _);
            }
        }


        // =========================================================
        // !STOP
        // =========================================================

        private async Task HandleStopAsync(
            SocketUserMessage message)
        {
            ApplicationUser? user =
                await GetLinkedUserAsync(
                    message.Author.Id);


            if (user == null)
            {
                await SendNotLinkedAsync(
                    message);

                return;
            }


            List<WorkLog> stoppedTimers =
                await StopAllTimersAsync(
                    user.Id);


            if (stoppedTimers.Count == 0)
            {
                await message.Channel
                    .SendMessageAsync(
                        "You do not have any active timers.");

                return;
            }


            StringBuilder response =
                new();


            response.AppendLine(
                $"**{GetDisplayName(user)} stopped {stoppedTimers.Count} active timer{(stoppedTimers.Count == 1 ? "" : "s")}.**");

            response.AppendLine();


            TimeSpan combinedTime =
                TimeSpan.Zero;


            foreach (
                WorkLog timer
                in stoppedTimers)
            {
                if (!timer.EndedAtUtc.HasValue)
                {
                    continue;
                }


                TimeSpan duration =
                    timer.EndedAtUtc.Value -
                    timer.StartedAtUtc;


                combinedTime +=
                    duration;


                response.AppendLine(
                    $"**{timer.ProjectNameSnapshot} — {timer.Activity}**");

                response.AppendLine(
                    $"Started: {DiscordTimestamp(timer.StartedAtUtc)}");

                response.AppendLine(
                    $"Ended: {DiscordTimestamp(timer.EndedAtUtc.Value)}");

                response.AppendLine(
                    $"Time logged: {FormatDuration(duration)}");

                response.AppendLine();
            }


            if (stoppedTimers.Count > 1)
            {
                response.AppendLine(
                    $"**Combined logged time:** {FormatDuration(combinedTime)}");
            }


            await message.Channel
                .SendMessageAsync(
                    response.ToString());
        }


        // =========================================================
        // !TIME
        // =========================================================

        private async Task HandleTimeAsync(
            SocketUserMessage message)
        {
            ApplicationUser? user =
                await GetLinkedUserAsync(
                    message.Author.Id);


            if (user == null)
            {
                await SendNotLinkedAsync(
                    message);

                return;
            }


            WorkLogUserSummary summary =
                await GetUserSummaryAsync(
                    user.Id);


            StringBuilder response =
                new();


            response.AppendLine(
                $"**{GetDisplayName(user)} — Time Summary**");

            response.AppendLine();


            response.AppendLine(
                $"**Completed Hours:** {FormatDuration(summary.TotalCompletedTime)}");


            // -----------------------------------------------------
            // ACTIVE TIMER
            // -----------------------------------------------------

            if (summary.ActiveTimers.Count > 0)
            {
                response.AppendLine();

                response.AppendLine(
                    "**Active Timer**");


                DateTimeOffset now =
                    DateTimeOffset.UtcNow;


                foreach (
                    WorkLog timer
                    in summary.ActiveTimers)
                {
                    TimeSpan elapsed =
                        now -
                        timer.StartedAtUtc;


                    response.AppendLine(
                        $"{timer.ProjectNameSnapshot} — {timer.Activity}");

                    response.AppendLine(
                        $"Started: {DiscordTimestamp(timer.StartedAtUtc)}");

                    response.AppendLine(
                        $"Elapsed: {FormatDuration(elapsed)}");
                }
            }


            // -----------------------------------------------------
            // RECENT COMPLETED LOGS
            // -----------------------------------------------------

            if (summary.RecentLogs.Count > 0)
            {
                response.AppendLine();

                response.AppendLine(
                    "**Recent Entries**");


                foreach (
                    WorkLog log
                    in summary.RecentLogs)
                {
                    if (!log.EndedAtUtc.HasValue)
                    {
                        continue;
                    }


                    TimeSpan duration =
                        log.EndedAtUtc.Value -
                        log.StartedAtUtc;


                    response.AppendLine(
                        $"{log.ProjectNameSnapshot} — {log.Activity} — {FormatDuration(duration)}");
                }
            }


            await message.Channel
                .SendMessageAsync(
                    response.ToString());
        }


        // =========================================================
        // !ALL
        // =========================================================

        private async Task HandleAllAsync(
            SocketUserMessage message)
        {
            if (!_interactiveUsers.TryAdd(
                message.Author.Id,
                0))
            {
                await message.Channel
                    .SendMessageAsync(
                        "You already have an active Chipy selection. Finish or cancel it first.");

                return;
            }


            try
            {
                // -------------------------------------------------
                // USER MUST HAVE LINKED SECWEB ACCOUNT
                // -------------------------------------------------

                ApplicationUser? requestingUser =
                    await GetLinkedUserAsync(
                        message.Author.Id);


                if (requestingUser == null)
                {
                    await SendNotLinkedAsync(
                        message);

                    return;
                }


                // -------------------------------------------------
                // GET ALL CURRENT PROJECTS
                // -------------------------------------------------

                List<Project> projects =
                    await GetAllProjectsAsync();


                if (projects.Count == 0)
                {
                    await message.Channel
                        .SendMessageAsync(
                            "There are currently no projects in SecWeb.");

                    return;
                }


                List<SelectionOption<Project>>
                    projectOptions =
                        projects

                            .Select(
                                project =>
                                    new SelectionOption<Project>(
                                        project,
                                        project.Name))

                            .ToList();


                SelectionOption<Project>?
                    selectedProject =
                        await SelectFromReactionsAsync(
                            message,
                            "**Chipy — Select a Project**",
                            projectOptions);


                if (selectedProject == null)
                {
                    return;
                }


                // -------------------------------------------------
                // GET PROJECT LEADERBOARD
                // -------------------------------------------------

                List<ProjectLeaderboardEntry>
                    leaderboard =
                        await GetProjectLeaderboardAsync(
                            selectedProject.Value.Id);


                if (leaderboard.Count == 0)
                {
                    await message.Channel
                        .SendMessageAsync(
                            $"No completed hours have been logged for **{selectedProject.Label}** yet.");

                    return;
                }


                List<string> lines =
                    new();


                TimeSpan overallTime =
                    TimeSpan.Zero;


                int position =
                    1;


                foreach (
                    ProjectLeaderboardEntry entry
                    in leaderboard)
                {
                    overallTime +=
                        entry.TotalTime;


                    lines.Add(
                        $"{position}. {entry.DisplayName} — {FormatDuration(entry.TotalTime)}");


                    position++;
                }


                lines.Add(
                    "");


                lines.Add(
                    $"Overall Project Hours — {FormatDuration(overallTime)}");


                await SendChunkedAsync(
                    message.Channel,
                    $"**{selectedProject.Label} — Work Hours**",
                    lines);
            }
            finally
            {
                _interactiveUsers.TryRemove(
                    message.Author.Id,
                    out _);
            }
        }


        // =========================================================
        // REACTION MENU
        // =========================================================

        private async Task<SelectionOption<T>?>
            SelectFromReactionsAsync<T>(
                SocketUserMessage commandMessage,
                string title,
                IReadOnlyList<SelectionOption<T>> options)
        {
            if (options.Count == 0)
            {
                return null;
            }


            if (options.Count >
                SelectionEmojis.Length)
            {
                await commandMessage.Channel
                    .SendMessageAsync(
                        $"Chipy currently supports up to {SelectionEmojis.Length} choices in one reaction menu.");

                return null;
            }


            // -----------------------------------------------------
            // BUILD MENU TEXT
            // -----------------------------------------------------

            StringBuilder content =
                new();


            content.AppendLine(
                title);

            content.AppendLine();


            for (
                int index = 0;
                index < options.Count;
                index++)
            {
                content.AppendLine(
                    $"{SelectionEmojis[index]} {options[index].Label}");
            }


            content.AppendLine();

            content.AppendLine(
                $"{CancelEmoji} Cancel");


            // -----------------------------------------------------
            // SEND MENU
            // -----------------------------------------------------

            IUserMessage menuMessage =
                await commandMessage.Channel
                    .SendMessageAsync(
                        content.ToString());


            // -----------------------------------------------------
            // VALID EMOJI
            // -----------------------------------------------------

            List<string> validEmojiNames =
                SelectionEmojis

                    .Take(
                        options.Count)

                    .ToList();


            validEmojiNames.Add(
                CancelEmoji);


            // -----------------------------------------------------
            // START WAITING FOR USER
            // -----------------------------------------------------

            Task<string?> selectionTask =
                _reactionService
                    .WaitForSelectionAsync(
                        menuMessage.Id,
                        commandMessage.Author.Id,
                        validEmojiNames,
                        TimeSpan.FromSeconds(
                            Math.Max(
                                10,
                                _options
                                    .SelectionTimeoutSeconds)),
                        _stoppingToken);


            // -----------------------------------------------------
            // ADD REACTIONS TO CHIPY'S MESSAGE
            // -----------------------------------------------------

            List<IEmote> reactions =
                validEmojiNames

                    .Select(
                        emoji =>
                            (IEmote)new Emoji(
                                emoji))

                    .ToList();


            await menuMessage
                .AddReactionsAsync(
                    reactions);


            // -----------------------------------------------------
            // WAIT FOR SELECTION
            // -----------------------------------------------------

            string? selectedEmoji =
                await selectionTask;


            // -----------------------------------------------------
            // TIMEOUT
            // -----------------------------------------------------

            if (selectedEmoji == null)
            {
                await menuMessage
                    .ModifyAsync(
                        properties =>
                        {
                            properties.Content =
                                $"{title}\n\nSelection expired. Run the command again.";
                        });


                return null;
            }


            // -----------------------------------------------------
            // CANCEL
            // -----------------------------------------------------

            if (selectedEmoji ==
                CancelEmoji)
            {
                await menuMessage
                    .ModifyAsync(
                        properties =>
                        {
                            properties.Content =
                                $"{title}\n\nSelection cancelled.";
                        });


                return null;
            }


            // -----------------------------------------------------
            // GET SELECTED INDEX
            // -----------------------------------------------------

            int selectedIndex =
                Array.IndexOf(
                    SelectionEmojis,
                    selectedEmoji);


            if (selectedIndex < 0 ||
                selectedIndex >=
                    options.Count)
            {
                return null;
            }


            SelectionOption<T> selected =
                options[selectedIndex];


            // -----------------------------------------------------
            // UPDATE MENU MESSAGE
            // -----------------------------------------------------

            await menuMessage
                .ModifyAsync(
                    properties =>
                    {
                        properties.Content =
                            $"{title}\n\nSelected: **{selected.Label}**";
                    });


            return selected;
        }


        // =========================================================
        // NOT LINKED MESSAGE
        // =========================================================

        private static async Task SendNotLinkedAsync(
            SocketUserMessage message)
        {
            await message.Channel
                .SendMessageAsync(
                    """
                    Your Discord account is not linked to SecWeb.

                    Log into SecWeb and open:

                    **My Account → Discord**

                    Then link this Discord account to your SecWeb account.
                    """);
        }


        // =========================================================
        // DATABASE HELPERS
        // =========================================================
        //
        // ChipyBotService is a hosted background service.
        //
        // ApplicationDbContext and WorkLogService are scoped.
        //
        // Therefore Chipy creates a new dependency injection
        // scope for each database operation.
        //

        private async Task<ApplicationUser?>
            GetLinkedUserAsync(
                ulong discordUserId)
        {
            using IServiceScope scope =
                _scopeFactory
                    .CreateScope();


            WorkLogService service =
                scope.ServiceProvider
                    .GetRequiredService<
                        WorkLogService>();


            return await service
                .GetUserByDiscordIdAsync(
                    discordUserId);
        }


        private async Task<List<Project>>
            GetLoggableProjectsAsync(
                string userId)
        {
            using IServiceScope scope =
                _scopeFactory
                    .CreateScope();


            WorkLogService service =
                scope.ServiceProvider
                    .GetRequiredService<
                        WorkLogService>();


            return await service
                .GetLoggableProjectsAsync(
                    userId);
        }


        private async Task<List<Project>>
            GetAllProjectsAsync()
        {
            using IServiceScope scope =
                _scopeFactory
                    .CreateScope();


            WorkLogService service =
                scope.ServiceProvider
                    .GetRequiredService<
                        WorkLogService>();


            return await service
                .GetAllProjectsAsync();
        }


        private async Task<List<WorkLog>>
            GetActiveTimersAsync(
                string userId)
        {
            using IServiceScope scope =
                _scopeFactory
                    .CreateScope();


            WorkLogService service =
                scope.ServiceProvider
                    .GetRequiredService<
                        WorkLogService>();


            return await service
                .GetActiveTimersAsync(
                    userId);
        }


        private async Task<WorkLog>
            StartTimerAsync(
                string userId,
                int projectId,
                WorkActivity activity)
        {
            using IServiceScope scope =
                _scopeFactory
                    .CreateScope();


            WorkLogService service =
                scope.ServiceProvider
                    .GetRequiredService<
                        WorkLogService>();


            return await service
                .StartTimerAsync(
                    userId,
                    projectId,
                    activity,
                    _options.GuildId,
                    _options.ChannelId);
        }


        private async Task<List<WorkLog>>
            StopAllTimersAsync(
                string userId)
        {
            using IServiceScope scope =
                _scopeFactory
                    .CreateScope();


            WorkLogService service =
                scope.ServiceProvider
                    .GetRequiredService<
                        WorkLogService>();


            return await service
                .StopAllTimersAsync(
                    userId);
        }


        private async Task<WorkLogUserSummary>
            GetUserSummaryAsync(
                string userId)
        {
            using IServiceScope scope =
                _scopeFactory
                    .CreateScope();


            WorkLogService service =
                scope.ServiceProvider
                    .GetRequiredService<
                        WorkLogService>();


            return await service
                .GetUserSummaryAsync(
                    userId);
        }


        private async Task<
            List<ProjectLeaderboardEntry>>
            GetProjectLeaderboardAsync(
                int projectId)
        {
            using IServiceScope scope =
                _scopeFactory
                    .CreateScope();


            WorkLogService service =
                scope.ServiceProvider
                    .GetRequiredService<
                        WorkLogService>();


            return await service
                .GetProjectLeaderboardAsync(
                    projectId);
        }


        // =========================================================
        // DISCORD TIMESTAMP
        // =========================================================
        //
        // Discord automatically displays this in each viewer's
        // local time zone.
        //

        private static string DiscordTimestamp(
            DateTimeOffset time)
        {
            return
                $"<t:{time.ToUnixTimeSeconds()}:F>";
        }


        // =========================================================
        // DURATION FORMAT
        // =========================================================

        private static string FormatDuration(
            TimeSpan duration)
        {
            if (duration <
                TimeSpan.Zero)
            {
                duration =
                    TimeSpan.Zero;
            }


            int totalHours =
                (int)Math.Floor(
                    duration.TotalHours);


            return
                $"{totalHours}h {duration.Minutes:D2}m";
        }


        // =========================================================
        // USER DISPLAY NAME
        // =========================================================

        private static string GetDisplayName(
            ApplicationUser user)
        {
            string fullName =
                $"{user.FirstName} {user.LastName}"
                    .Trim();


            if (!string.IsNullOrWhiteSpace(
                fullName))
            {
                return fullName;
            }


            if (!string.IsNullOrWhiteSpace(
                user.Email))
            {
                return user.Email;
            }


            return "SecWeb User";
        }


        // =========================================================
        // LONG DISCORD MESSAGE HELPER
        // =========================================================

        private static async Task SendChunkedAsync(
            IMessageChannel channel,
            string header,
            IEnumerable<string> lines)
        {
            const int safeMaximumLength =
                1800;


            StringBuilder current =
                new();


            current.AppendLine(
                header);

            current.AppendLine();


            foreach (
                string line
                in lines)
            {
                if (current.Length +
                    line.Length +
                    2 >
                    safeMaximumLength)
                {
                    await channel
                        .SendMessageAsync(
                            current.ToString());


                    current.Clear();


                    current.AppendLine(
                        $"{header} — Continued");

                    current.AppendLine();
                }


                current.AppendLine(
                    line);
            }


            if (current.Length > 0)
            {
                await channel
                    .SendMessageAsync(
                        current.ToString());
            }
        }


        // =========================================================
        // REACTION SELECTION OPTION
        // =========================================================

        private sealed record SelectionOption<T>(
            T Value,
            string Label);
    }
}