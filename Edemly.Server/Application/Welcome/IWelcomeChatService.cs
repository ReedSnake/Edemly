using Edemly.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Application.Welcome
{
    public interface IWelcomeChatService
    {
        Task<(Chat Chat, bool Created)> EnsureWelcomeChatAsync(DbContext dbContext);

        Task EnsureUserInWelcomeChatAsync(DbContext dbContext, int userId);
    }
}