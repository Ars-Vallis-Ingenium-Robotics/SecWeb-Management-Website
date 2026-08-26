namespace SecWeb.Data
{
    public static class AccountRoles
    {
        public const string Member = "Member";

        public const string Admin = "Admin";

        // Kept only so existing databases can migrate users that were
        // previously assigned the old global role. New accounts never use it.
        public const string LegacySubsystemLead = "Subsystem Lead";

        // This account can never be downgraded from Admin.
        public const string PermanentAdminEmail =
            "secretary@avirobotics.org";

        public static readonly string[] All =
        {
            Member,
            Admin
        };
    }
}
