namespace SecWeb.Bot
{
    public sealed class ChipyOptions
    {
        // =========================================================
        // CHIPY BOT
        // =========================================================

        // Discord bot token.
        //
        // Keep this in User Secrets.
        public string BotToken { get; set; } =
            string.Empty;


        // AVI Robotics Discord server ID.
        public ulong GuildId { get; set; }


        // Channel where Chipy accepts:
        //
        // !log
        // !stop
        // !time
        // !all
        public ulong ChannelId { get; set; }


        // How long reaction menus remain active.
        public int SelectionTimeoutSeconds { get; set; } =
            60;



        // =========================================================
        // DISCORD OAUTH ACCOUNT LINKING
        // =========================================================

        // Discord Application ID / OAuth Client ID.
        //
        // This is the same as the Application ID shown in
        // the Discord Developer Portal.
        public string ClientId { get; set; } =
            string.Empty;


        // Discord OAuth Client Secret.
        //
        // NEVER put the actual value directly in source code.
        //
        // Store it in User Secrets.
        public string ClientSecret { get; set; } =
            string.Empty;


        // Discord sends users back here after authorization.
        //
        // Development:
        //
        // https://localhost:7265/account/manage/discord/callback
        public string RedirectUri { get; set; } =
            string.Empty;
    }
}