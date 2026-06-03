using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Server.Utils;
using Edemly.Contracts.ChatMembers;

namespace Edemly.Server.Api.Services
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

        public async Task<ServiceMessageResult> AddMember(int currentUserId, CreateChatMemberDto model)
        {
            try
            {
                if (!await CanAddChatMemberAsync(currentUserId, model.ChatId))
                {
                    return ServiceMessageResult.Forbidden();
                }

                var result = await AddMember(model.ChatId, model.UserId, (ChatMemberRole)model.Role);
                if (!result.Success)
                {
                    return ServiceMessageResult.BadRequest(result.Error ?? "Failed to add member");
                }

                return ServiceMessageResult.Ok("Chat member added");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add chat member");
                return ServiceMessageResult.BadRequest(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        public async Task<(bool Success, string? Error)> AddMember(int chatId, int userId, ChatMemberRole role)
        {
            try
            {
                var existingMember = await _ctx.Set<ChatMember>()
                    .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

                if (existingMember != null)
                {
                    _logger.LogInformation("User {UserId} is already a member of chat {ChatId}", userId, chatId);
                    return (true, null);
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

                _logger.LogInformation("Added user {UserId} to chat {ChatId} with role {Role}", userId, chatId, role);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add user {UserId} to chat {ChatId}", userId, chatId);
                return (false, ex.Message);
            }
        }

        public async Task<ServiceMessageResult> UpdateMember(int currentUserId, UpdateChatMemberDto model)
        {
            try
            {
                var member = await _ctx.Set<ChatMember>().FindAsync(model.Id);
                if (member == null || !await CanManageMemberAsync(currentUserId, member, requireDifferentUser: true))
                {
                    return ServiceMessageResult.Forbidden();
                }

                if (model.Role.HasValue)
                {
                    member.Role = (ChatMemberRole)model.Role.Value;
                }

                await _ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Chat member updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update chat member");
                return ServiceMessageResult.BadRequest(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        public async Task<ServiceMessageResult> DeleteMember(int currentUserId, int id)
        {
            try
            {
                var member = await _ctx.Set<ChatMember>().FindAsync(id);
                if (member == null || !await CanManageMemberAsync(currentUserId, member, requireDifferentUser: true))
                {
                    return ServiceMessageResult.Forbidden();
                }

                _ctx.Set<ChatMember>().Remove(member);
                await _ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Chat member removed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete chat member");
                return ServiceMessageResult.BadRequest(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        public async Task<ServiceDataResult<ChatMemberDto>> GetMember(int id)
        {
            try
            {
                var member = await _ctx.Set<ChatMember>().FindAsync(id);
                if (member == null)
                {
                    return ServiceDataResult<ChatMemberDto>.NotFound("Member not found");
                }

                return ServiceDataResult<ChatMemberDto>.Ok(ToDto(member));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chat member");
                return ServiceDataResult<ChatMemberDto>.NotFound(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        public async Task<ServiceDataResult<List<ChatMemberDto>>> GetMembers(int currentUserId, int chatId)
        {
            try
            {
                if (!await IsInChatAsync(currentUserId, chatId))
                {
                    return ServiceDataResult<List<ChatMemberDto>>.Forbidden();
                }

                var members = await _ctx.Set<ChatMember>()
                    .Where(m => m.ChatId == chatId)
                    .Select(m => new ChatMemberDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        ChatId = m.ChatId,
                        Role = (int)m.Role,
                        JoinedAt = m.JoinedAt
                    })
                    .ToListAsync();

                return ServiceDataResult<List<ChatMemberDto>>.Ok(members);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chat members");
                return ServiceDataResult<List<ChatMemberDto>>.NotFound(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        public async Task<ServiceDataResult<List<ChatMemberDto>>> GetMemberships(int currentUserId)
        {
            try
            {
                var memberships = await _ctx.Set<ChatMember>()
                    .Where(m => m.UserId == currentUserId)
                    .Select(m => new ChatMemberDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        ChatId = m.ChatId,
                        Role = (int)m.Role,
                        JoinedAt = m.JoinedAt
                    })
                    .ToListAsync();

                return ServiceDataResult<List<ChatMemberDto>>.Ok(memberships);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user memberships");
                return ServiceDataResult<List<ChatMemberDto>>.NotFound(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        private async Task<bool> IsInChatAsync(int userId, int chatId)
        {
            return await _ctx.Set<ChatMember>().AnyAsync(cm => cm.UserId == userId && cm.ChatId == chatId);
        }

        private async Task<bool> CanAddChatMemberAsync(int currentUserId, int chatId)
        {
            var currentMember = await _ctx.Set<ChatMember>()
                .FirstOrDefaultAsync(cm => cm.UserId == currentUserId && cm.ChatId == chatId);

            if (currentMember == null)
            {
                return false;
            }

            return currentMember.Role == ChatMemberRole.Admin || currentMember.Role == ChatMemberRole.Creator;
        }

        private async Task<bool> CanManageMemberAsync(int currentUserId, ChatMember member, bool requireDifferentUser)
        {
            if (requireDifferentUser && member.UserId == currentUserId)
            {
                return false;
            }

            var currentMember = await _ctx.Set<ChatMember>()
                .FirstOrDefaultAsync(cm => cm.UserId == currentUserId && cm.ChatId == member.ChatId);

            if (currentMember == null)
            {
                return false;
            }

            if (currentMember.Role == ChatMemberRole.Creator)
            {
                return true;
            }

            if (currentMember.Role == ChatMemberRole.Admin)
            {
                return member.Role == ChatMemberRole.Base;
            }

            return false;
        }

        private static ChatMemberDto ToDto(ChatMember member)
        {
            return new ChatMemberDto
            {
                Id = member.Id,
                UserId = member.UserId,
                ChatId = member.ChatId,
                Role = (int)member.Role,
                JoinedAt = member.JoinedAt
            };
        }

        private void DisposeTenantContext()
        {
            if (_isTenant)
            {
                _ctx.Dispose();
            }
        }
    }
}
