using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Edemly.Server.Hubs
{
    public class JwtUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            if (connection?.User == null) return null;

            var id = connection.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? connection.User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value
                     ?? connection.User.FindFirst("sub")?.Value;

            return id;
        }
    }
}