using System.Net;
using System.Net.Mail;
using PoolPredict.Api.Infrastructure.Persistence;

namespace PoolPredict.Api.Modules.Email;

public sealed class SmtpEmailSender(EmailSettingsStore settingsStore, ILogger<SmtpEmailSender> logger)
{
    public async Task<EmailSendResult> SendAsync(
        string toEmail,
        string subject,
        string body,
        bool isHtml = false,
        IReadOnlyCollection<EmailAttachment>? attachments = null)
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
        foreach (var attachment in attachments ?? [])
        {
            var stream = new MemoryStream(attachment.Content, writable: false);
            message.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
        }

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = settings.UseStartTls,
            UseDefaultCredentials = false
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
            var status = ex.StatusCode == SmtpStatusCode.GeneralFailure ? "" : $"{ex.StatusCode}: ";
            return new EmailSendResult(false, $"SMTP send failed. {status}{ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "SMTP configuration is invalid.");
            return new EmailSendResult(false, $"SMTP configuration is invalid. {ex.Message}");
        }
    }
}

public sealed record EmailSendResult(bool Sent, string Message);

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);
