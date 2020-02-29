using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;

namespace Gaia.Service
{
    public class AuthMessageSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string message)
        {
            return Task.FromResult(0);
        }
    }
}
