using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using uchat_server.Api.Middleware;
using uchat_server.Data;
using uchat_server.Data.Entities;
using uchat_server.Services;
using uchat_server.Utils;
using static uchat_server.Api.DTOs.ChatMemberDtos;

namespace uchat_server.Api.Services
{
    public class ChatMemberService : IChatMemberService
    {
        private readonly ILogger<ChatMemberService> _logger;
        private readonly DbContext _ctx;
        private readonly bool _isTenant;

        public ChatMemberService(ServerDbContext serverDb, ILogger<ChatMemberService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
        {
            _logger = logger;
            _ctx = DbContextResolver.Resolve(out var isTenant, serverDb, tenantProvider, tenantDbFactory);
            _isTenant = isTenant;
        }

        // Add a member to a chat (DTO version)
        public async Task<(bool Success, string? Error)> AddMember(ChatMemberCreateDto model)
        {
            try
            {
                var member = new ChatMember
                {
                    UserId = model.UserId,
                    ChatId = model.ChatId,
                    Role = model.Role,
                    JoinedAt = DateTime.UtcNow
                };

                _ctx.Set<ChatMember>().Add(member);
                await _ctx.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add chat member");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // ✅ Додаємо перевантажений метод для простого додавання
        public async Task<(bool Success, string? Error)> AddMember(int chatId, int userId, ChatMemberRole role)
        {
            try
            {
                // Перевіряємо, чи користувач вже є членом
                var existingMember = await _ctx.Set<ChatMember>()
                    .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

                if (existingMember != null)
                {
                    _logger.LogInformation($"User {userId} is already a member of chat {chatId}");
                    return (true, null); // Вже є членом, повертаємо успіх
                }

                var member = new ChatMember
                {
                    UserId = userId,
                    ChatId = chatId,
                    Role = role,
                    JoinedAt = DateTime.UtcNow
                };

                _ctx.Set<ChatMember>().Add(member);
                await _ctx.SaveChangesAsync();

                _logger.LogInformation($"Added user {userId} to chat {chatId} with role {role}");
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to add user {userId} to chat {chatId}");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Update a member's role
        public async Task<(bool Success, string? Error)> UpdateMember(ChatMemberUpdateDto model)
        {
            try
            {
                var member = await _ctx.Set<ChatMember>().FindAsync(model.Id);
                if (member == null)
                    return (false, "Member not found");

                if (model.Role.HasValue)
                    member.Role = model.Role.Value;

                await _ctx.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update chat member");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Delete a member
        public async Task<(bool Success, string? Error)> DeleteMember(int id)
        {
            try
            {
                var member = await _ctx.Set<ChatMember>().FindAsync(id);
                if (member == null)
                    return (false, "Member not found");

                _ctx.Set<ChatMember>().Remove(member);
                await _ctx.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete chat member");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Get a single member
        public async Task<(bool Success, string? Error, ChatMemberGetDto Member)> GetMember(int id)
        {
            try
            {
                var member = await _ctx.Set<ChatMember>().FindAsync(id);
                if (member == null)
                    return (false, "Member not found", null!);

                var dto = new ChatMemberGetDto
                {
                    Id = member.Id,
                    UserId = member.UserId,
                    ChatId = member.ChatId,
                    Role = member.Role,
                    JoinedAt = member.JoinedAt
                };

                return (true, null, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chat member");
                return (false, ex.Message, null!);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Get all members of a chat
        public async Task<(bool Success, string? Error, List<ChatMemberGetDto> Members)> GetMembers(int chatId)
        {
            try
            {
                var members = await _ctx.Set<ChatMember>()
                    .Where(m => m.ChatId == chatId)
                    .Select(m => new ChatMemberGetDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        ChatId = m.ChatId,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt
                    })
                    .ToListAsync();

                return (true, null, members);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chat members");
                return (false, ex.Message, new List<ChatMemberGetDto>());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Get all chats a user is in
        public async Task<(bool Success, string? Error, List<ChatMemberGetDto> Memberships)> GetMemberships(int userId)
        {
            try
            {
                var memberships = await _ctx.Set<ChatMember>()
                    .Where(m => m.UserId == userId)
                    .Select(m => new ChatMemberGetDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        ChatId = m.ChatId,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt
                    })
                    .ToListAsync();

                return (true, null, memberships);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user memberships");
                return (false, ex.Message, new List<ChatMemberGetDto>());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
    }
}
