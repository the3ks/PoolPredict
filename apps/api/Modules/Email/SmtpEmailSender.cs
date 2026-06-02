using System.Net;
using System.Net.Mail;
using PoolPredict.Api.Infrastructure.Persistence;

namespace PoolPredict.Api.Modules.Email;

public sealed class SmtpEmailSender(EmailSettingsStore settingsStore, ILogger<SmtpEmailSender> logger)
{
    public async Task<EmailSendResult> SendAsync(string toEmail, string subject, string body, bool isHtml = false)
    {
        var settings = settingsStore.GetEnabledSettings();
        if (settings is null)
        {
            logger.LogWarning("Email send skipped because SMTP is not enabled. Subject: {Subject}, To: {ToEmail}", subject, toEmail);
            return new EmailSendResult(false, "SMTP is not enabled.");
        }

        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromEmail))
        {
            return new EmailSendResult(false, "SMTP host and from email are required.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseStartTls
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password ?? "");
        }

        try
        {
            await client.SendMailAsync(message);
            return new EmailSendResult(true, "Email sent.");
        }
        catch (SmtpException ex)
        {
            logger.LogError(ex, "SMTP send failed. Subject: {Subject}, To: {ToEmail}", subject, toEmail);
            return new EmailSendResult(false, "SMTP send failed.");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "SMTP configuration is invalid.");
            return new EmailSendResult(false, "SMTP configuration is invalid.");
        }
    }
}

public sealed record EmailSendResult(bool Sent, string Message);
