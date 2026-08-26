namespace SecWeb.Services
{
    public interface ILoginCodeEmailSender
    {
        Task SendLoginCodeAsync(
            string emailAddress,
            string code);
    }
}