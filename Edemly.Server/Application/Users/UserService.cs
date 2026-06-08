using Edemly.Contracts.Users;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Common.Mappers;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Application.Users
{
    public class UserService : TenantAwareServiceBase, IUserService
    {
        private readonly ILogger<UserService> _logger;

        public UserService(ServerDbContext serverDbContext, ILogger<UserService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbContextFactory)
            : base(serverDbContext, tenantProvider, tenantDbContextFactory)
        {
            _logger = logger;
        }

        public async Task<ServiceResult> CreateAsync(CreateUserDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var username = UsernameRules.Normalize(request.Username);
                var usernameValidationError = UsernameRules.Validate(username);
                if (usernameValidationError != null)
                    return ServiceResult.BadRequest(usernameValidationError);

                if (await ctx.Set<LoginInfo>().AnyAsync(l => l.Email == request.Email))
                {
                    _logger.LogWarning("Email already exists during registration: {Email}", request.Email);
                    return ServiceResult.Conflict("User with this email already exists");
                }

                if (await UsernameExistsAsync(ctx, username))
                {
                    _logger.LogWarning("Username already taken during registration: {Username}", username);
                    return ServiceResult.Conflict("Username already taken");
                }

                var strategy = ctx.Database.CreateExecutionStrategy();

                User? user = null;

                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await ctx.Database.BeginTransactionAsync();

                    try
                    {
                        var loginInfo = new LoginInfo
                        {
                            Email = request.Email,
                            IsEmailVerified = true
                        };
                        ctx.Set<LoginInfo>().Add(loginInfo);
                        await ctx.SaveChangesAsync();

                        _logger.LogInformation("LoginInfo created for {Email}, ID: {Id}", request.Email, loginInfo.Id);

                        user = new User
                        {
                            Username = username,
                            LoginInfoId = loginInfo.Id,
                            PfpUrl = null,
                            LastOnline = DateTime.UtcNow,
                            FirstName = null,
                            LastName = null,
                            Description = null,
                            PhoneNumber = null,
                            Location = null,
                            SubscriptionStatus = SubscriptionStatus.Free,
                            SubscriptionExpiration = null,
                            CreatedAt = DateTime.UtcNow
                        };
                        ctx.Set<User>().Add(user);
                        await ctx.SaveChangesAsync();

                        _logger.LogInformation("User created: {Username}, ID: {Id}", username ?? "(empty)", user.Id);

                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error while creating user {Username}", username ?? "(empty)");
                        throw;
                    }
                });

                return ServiceResult.Ok("User created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateUser failed");
                return ServiceResult.Unexpected("Failed to create user");
            }
        }

        public async Task<ServiceResult<UserInfoDto>> GetFullInfoAsync(int currentUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var user = await ctx.Set<User>()
                    .AsNoTracking()
                    .Include(u => u.LoginInfo)
                    .FirstOrDefaultAsync(u => u.Id == currentUserId);

                if (user == null)
                {
                    return ServiceResult<UserInfoDto>.NotFound("User not found");
                }

                return ServiceResult<UserInfoDto>.Ok(UserMappings.ToInfoDto(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get full info for user {UserId}", currentUserId);
                return ServiceResult<UserInfoDto>.Unexpected("Failed to get user info");
            }
        }

        public async Task<ServiceResult<UserDto>> GetByIdAsync(int targetUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var user = await ctx.Set<User>()
                    .AsNoTracking()
                    .Include(u => u.LoginInfo)
                    .FirstOrDefaultAsync(u => u.Id == targetUserId);

                if (user == null)
                {
                    return ServiceResult<UserDto>.NotFound("User not found");
                }

                return ServiceResult<UserDto>.Ok(UserMappings.ToDto(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user by id {UserId}", targetUserId);
                return ServiceResult<UserDto>.Unexpected("Failed to get user");
            }
        }

        public async Task<ServiceResult<List<UserDto>>> SearchUsersAsync(string searchQuery)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (string.IsNullOrWhiteSpace(searchQuery))
                {
                    return ServiceResult<List<UserDto>>.BadRequest("Search query is required");
                }

                var query = searchQuery.Trim().ToLower();

                var users = await ctx.Set<User>()
                    .Include(u => u.LoginInfo)
                    .Where(u => (u.Username != null && u.Username.ToLower().Contains(query)) ||
                                u.LoginInfo.Email.ToLower().Contains(query))
                    .Take(5)
                    .Select(UserMappings.SearchProjection)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} users for search query: {Query}", users.Count, searchQuery);
                return ServiceResult<List<UserDto>>.Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search users with query: {Query}", searchQuery);
                return ServiceResult<List<UserDto>>.Unexpected("Failed to search users");
            }
        }

        public async Task<ServiceResult<List<UserDto>>> GetUsersBatchAsync(List<int> targetUserIds)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (targetUserIds == null || targetUserIds.Count == 0)
                {
                    return ServiceResult<List<UserDto>>.BadRequest("User IDs list is required");
                }

                var users = await ctx.Set<User>()
                    .AsNoTracking()
                    .Where(u => targetUserIds.Contains(u.Id))
                    .Select(UserMappings.BatchProjection)
                    .ToListAsync();

                _logger.LogInformation(
                    "Retrieved {Count} users from batch request of {RequestedCount} IDs",
                    users.Count,
                    targetUserIds.Count);

                return ServiceResult<List<UserDto>>.Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get users batch");
                return ServiceResult<List<UserDto>>.Unexpected("Failed to get users");
            }
        }

        public async Task<ServiceResult> UpdateAsync(int currentUserId, UpdateUserDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var user = await ctx.Set<User>().FindAsync(currentUserId);
                if (user == null)
                {
                    return ServiceResult.NotFound("User not found");
                }

                if (request.Username != null)
                {
                    var username = UsernameRules.Normalize(request.Username);
                    var usernameValidationError = UsernameRules.Validate(username);
                    if (usernameValidationError != null)
                    {
                        return ServiceResult.BadRequest(usernameValidationError);
                    }

                    if (await UsernameExistsAsync(ctx, username, excludeUserId: currentUserId))
                    {
                        return ServiceResult.Conflict("Username already taken");
                    }

                    user.Username = username;
                }

                if (request.FirstName != null)
                    user.FirstName = NormalizeOptionalValue(request.FirstName);

                if (request.LastName != null)
                    user.LastName = NormalizeOptionalValue(request.LastName);

                if (request.PhoneNumber != null)
                    user.PhoneNumber = NormalizeOptionalValue(request.PhoneNumber);

                if (request.Location != null)
                    user.Location = NormalizeOptionalValue(request.Location);

                if (request.Description != null)
                    user.Description = NormalizeOptionalValue(request.Description);

                if (request.PfpUrl != null)
                    user.PfpUrl = NormalizeOptionalValue(request.PfpUrl);

                await ctx.SaveChangesAsync();

                _logger.LogInformation(
                    "User {UserId} updated successfully. FirstName: {FirstName}, LastName: {LastName}",
                    currentUserId,
                    user.FirstName,
                    user.LastName);

                return ServiceResult.Ok("User updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user {UserId}", currentUserId);
                return ServiceResult.Unexpected("Failed to update user");
            }
        }

        public async Task<ServiceResult> DeleteAsync(int requesterId, int targetUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (requesterId != targetUserId)
                {
                    return ServiceResult.Forbidden();
                }

                var user = await ctx.Set<User>().FindAsync(targetUserId);
                if (user == null)
                {
                    return ServiceResult.NotFound("User not found");
                }

                ctx.Set<User>().Remove(user);
                await ctx.SaveChangesAsync();
                return ServiceResult.Ok("User deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user {UserId}", targetUserId);
                return ServiceResult.Unexpected("Failed to delete user");
            }
        }

        private static string? NormalizeOptionalValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private static Task<bool> UsernameExistsAsync(DbContext ctx, string? username, int? excludeUserId = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                return Task.FromResult(false);

            var normalized = username.ToLower();
            return ctx.Set<User>().AnyAsync(user =>
                user.Username != null &&
                user.Username.ToLower() == normalized &&
                (!excludeUserId.HasValue || user.Id != excludeUserId.Value));
        }
    }
}