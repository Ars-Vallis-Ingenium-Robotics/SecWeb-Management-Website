namespace SecWeb.Services
{
    public interface IAccountAdministrationEmailSender
    {
        Task SendAccountInvitationAsync(
            string email,
            string firstName,
            string role,
            string invitationLink);

        Task SendRoleChangedAsync(
            string email,
            string firstName,
            string newRole);
    }
}