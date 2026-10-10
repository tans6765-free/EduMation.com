namespace EduMation.Services;

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string textBody, CancellationToken cancellationToken = default);
}
