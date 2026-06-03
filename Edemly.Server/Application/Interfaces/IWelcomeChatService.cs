using Microsoft.EntityFrameworkCore;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.Services
{
    public interface IWelcomeChatService
    {
        Task<(Chat Chat, bool Created)> EnsureWelcomeChatAsync(DbContext dbContext);
        Task EnsureUserInWelcomeChatAsync(DbContext dbContext, int userId);
    }
}
