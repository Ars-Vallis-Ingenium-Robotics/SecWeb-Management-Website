using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;
using SecWeb.Data;

namespace SecWeb.Services
{
    public class SmtpEmailSender :
        IEmailSender<ApplicationUser>,
        ILoginCodeEmailSender,
        IAccountAdministrationEmailSender
    {
        private readonly EmailSettings _settings;

        public SmtpEmailSender(
            IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendLoginCodeAsync(
            string emailAddress,
            string code)
        {
            string htmlMessage = $"""
                <h2>SecWeb Login Verification</h2>
                <p>A login attempt was made for your SecWeb account.</p>
                <p>Your verification code is:</p>
                <h1 style="letter-spacing: 6px;">{code}</h1>
                <p>Enter this code on the SecWeb verification page to finish signing in.</p>
                <p>If you did not attempt to sign in, you can ignore this email.</p>
                """;

            await SendEmailAsync(
                emailAddress,
                "Your SecWeb login verification code",
                htmlMessage);
        }

        public async Task SendConfirmationLinkAsync(
            ApplicationUser user,
            string email,
            string confirmationLink)
        {
            string htmlMessage = $"""
                <h2>Confirm your SecWeb account</h2>
                <p>Hello {DisplayName(user)},</p>
                <p>Click the link below to confirm your email address.</p>
                <p><a href="{confirmationLink}">Confirm Email</a></p>
                <p>If you did not create this SecWeb account, you can ignore this email.</p>
                """;

            await SendEmailAsync(
                email,
                "Confirm your SecWeb account",
                htmlMessage);
        }

        public async Task SendPasswordResetLinkAsync(
            ApplicationUser user,
            string email,
            string resetLink)
        {
            string htmlMessage = $"""
                <h2>Reset your SecWeb password</h2>
                <p>Hello {DisplayName(user)},</p>
                <p>Click the link below to reset your password.</p>
                <p><a href="{resetLink}">Reset Password</a></p>
                <p>If you did not request this change, you can ignore this email.</p>
                """;

            await SendEmailAsync(
                email,
                "Reset your SecWeb password",
                htmlMessage);
        }

        public async Task SendPasswordResetCodeAsync(
            ApplicationUser user,
            string email,
            string resetCode)
        {
            string htmlMessage = $"""
                <h2>SecWeb Password Reset</h2>
                <p>Hello {DisplayName(user)},</p>
                <p>Your password reset code is:</p>
                <h1 style="letter-spacing: 6px;">{resetCode}</h1>
                <p>If you did not request a password reset, you can ignore this email.</p>
                """;

            await SendEmailAsync(
                email,
                "Your SecWeb password reset code",
                htmlMessage);
        }

        public async Task SendAccountInvitationAsync(
            string email,
            string firstName,
            string role,
            string invitationLink)
        {
            string normalizedRole =
                role == AccountRoles.Admin
                    ? AccountRoles.Admin
                    : AccountRoles.Member;

            string subject =
                normalizedRole == AccountRoles.Admin
                    ? "You now have Admin access to SecWeb"
                    : "You have been invited to SecWeb";

            string roleDescription =
                normalizedRole == AccountRoles.Admin
                    ? "You have been granted Admin access. Admin accounts can manage users and SecWeb administrative features."
                    : "You have been granted Member access. Project Lead assignments are managed separately inside each project.";

            string htmlMessage = $"""
                <h2>Welcome to SecWeb</h2>
                <p>Hello {firstName},</p>
                <p>A SecWeb administrator created an account for you.</p>
                <p>Your access level is:</p>
                <h3>{normalizedRole}</h3>
                <p>{roleDescription}</p>
                <p>Use the link below to finish setting up your account and create your password.</p>
                <p><a href="{invitationLink}">Set Up My SecWeb Account</a></p>
                <p>If you were not expecting this invitation, you can ignore this email.</p>
                """;

            await SendEmailAsync(
                email,
                subject,
                htmlMessage);
        }

        public async Task SendRoleChangedAsync(
            string email,
            string firstName,
            string newRole)
        {
            string normalizedRole =
                newRole == AccountRoles.Admin
                    ? AccountRoles.Admin
                    : AccountRoles.Member;

            string roleDescription =
                normalizedRole == AccountRoles.Admin
                    ? "You now have Admin access in SecWeb."
                    : "You now have Member access in SecWeb. Project Lead assignments are managed inside each project.";

            string htmlMessage = $"""
                <h2>SecWeb Access Updated</h2>
                <p>Hello {firstName},</p>
                <p>Your SecWeb access level has been changed to:</p>
                <h3>{normalizedRole}</h3>
                <p>{roleDescription}</p>
                <p>Your new access level will apply the next time you sign in.</p>
                """;

            await SendEmailAsync(
                email,
                "Your SecWeb access has been updated",
                htmlMessage);
        }

        private async Task SendEmailAsync(
            string destination,
            string subject,
            string htmlMessage)
        {
            ValidateSettings();

            MimeMessage message = new();

            message.From.Add(
                new MailboxAddress(
                    _settings.FromName,
                    _settings.FromAddress));

            message.To.Add(
                MailboxAddress.Parse(destination));

            message.Subject = subject;

            BodyBuilder bodyBuilder =
                new()
                {
                    HtmlBody = htmlMessage
                };

            message.Body = bodyBuilder.ToMessageBody();

            using SmtpClient smtpClient = new();

            SecureSocketOptions security =
                _settings.UseSslOnConnect
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

            await smtpClient.ConnectAsync(
                _settings.Host,
                _settings.Port,
                security);

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                await smtpClient.AuthenticateAsync(
                    _settings.Username,
                    _settings.Password);
            }

            await smtpClient.SendAsync(message);
            await smtpClient.DisconnectAsync(true);
        }

        private void ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(_settings.Host))
            {
                throw new InvalidOperationException(
                    "The SMTP host is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_settings.FromAddress))
            {
                throw new InvalidOperationException(
                    "The email sender address is not configured.");
            }
        }

        private static string DisplayName(ApplicationUser user)
        {
            return !string.IsNullOrWhiteSpace(user.FirstName)
                ? user.FirstName
                : "SecWeb user";
        }
    }
}
