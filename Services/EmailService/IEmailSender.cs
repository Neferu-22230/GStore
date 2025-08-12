namespace GStore.Services.EmailService;

public interface IEmailSender
{
    Task SendEmailAsync(Message message);
}
