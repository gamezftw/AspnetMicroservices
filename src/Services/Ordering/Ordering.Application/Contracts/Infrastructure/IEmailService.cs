using Ordering.Application.Model;

namespace Ordering.Application.Infrastructure
{
    public interface IEmailService
    {
        Task<bool> SendEmail(Email email);
    }
}