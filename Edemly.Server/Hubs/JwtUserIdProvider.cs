using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace uchat_server.Hubs
{
    /// <summary>
    /// Maps SignalR user identifier to JWT claim. Prefers standard NameIdentifier, then custom 'userId', then 'sub'.
    /// Ensures Clients.User(...) works when tokens contain different claim names.
    /// </summary>
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
