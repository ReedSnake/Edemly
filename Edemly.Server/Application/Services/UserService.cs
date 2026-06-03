using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Contracts.Users;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Services;
using Edemly.Server.Utils;

namespace Edemly.Server.Api.Services
{
    public class UserService : IUserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly DbContext _ctx;
        private readonly bool _isTenant;

        public UserService(ServerDbContext serverDb, ILogger<UserService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
        {
            _logger = logger;
            _ctx = DbContextResolver.Resolve(out var isTenant, serverDb, tenantProvider, tenantDbFactory);
            _isTenant = isTenant;
        }

        public async Task<(bool Success, string? Error)> CreateUser(CreateUserDto model)
        {
            try
            {
                var username = UsernameRules.Normalize(model.Username);
                var usernameValidationError = UsernameRules.Validate(username);
                if (usernameValidationError != null)
                    return (false, usernameValidationError);

                if (await _ctx.Set<LoginInfo>().AnyAsync(l => l.Email == model.Email))
                {
                    _logger.LogWarning("Email already exists during registration: {Email}", model.Email);
                    return (false, "User with this email already exists");
                }

                if (await UsernameExistsAsync(username))
                {
                    _logger.LogWarning("Username already taken during registration: {Username}", username);
                    return (false, "Username already taken");
                }

                var strategy = _ctx.Database.CreateExecutionStrategy();

                User? user = null;

                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _ctx.Database.BeginTransactionAsync();

                    try
                    {
                        var loginInfo = new LoginInfo
                        {
                            Email = model.Email,
                            IsEmailVerified = true
                        };
                        _ctx.Set<LoginInfo>().Add(loginInfo);
                        await _ctx.SaveChangesAsync();

                        _logger.LogInformation("LoginInfo created for {Email}, ID: {Id}", model.Email, loginInfo.Id);

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
                        _ctx.Set<User>().Add(user);
                        await _ctx.SaveChangesAsync();

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

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateUser failed");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error, UserInfoDto? User)> GetFullInfo(int id)
        {
            try
            {
                var user = await _ctx.Set<User>()
                    .Include(u => u.LoginInfo)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return (false, "User not found", null);

                var dto = new UserInfoDto
                {
                    Id = user.Id,
                    Username = user.Username ?? string.Empty,
                    Email = user.LoginInfo.Email,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    Location = user.Location ?? string.Empty,
                    Description = user.Description ?? string.Empty,
                    PfpUrl = user.PfpUrl ?? string.Empty,
                    CreatedAt = user.CreatedAt,
                    // convert enum to string for client compatibility
                    SubscriptionStatus = user.SubscriptionStatus.ToString(),
                    SubscriptionExpiration = user.SubscriptionExpiration
                };

                return (true, null, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get full info for user {UserId}", id);
                return (false, ex.Message, null);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error, UserDto? User)> GetById(int id)
        {
            try
            {
                var user = await _ctx.Set<User>()
                    .Include(u => u.LoginInfo)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return (false, "User not found", null);

                var dto = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username ?? string.Empty,
                    Email = user.LoginInfo.Email,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    PfpUrl = user.PfpUrl ?? string.Empty,
                    Description = user.Description ?? string.Empty
                };

                return (true, null, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user by id {UserId}", id);
                return (false, ex.Message, null);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error, List<UserDto> Users)> SearchUsers(string searchQuery)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchQuery))
                    return (false, "Search query cannot be empty", new List<UserDto>());

                var query = searchQuery.Trim().ToLower();

                var users = await _ctx.Set<User>()
                    .Include(u => u.LoginInfo)
                    .Where(u => (u.Username != null && u.Username.ToLower().Contains(query)) ||
                                u.LoginInfo.Email.ToLower().Contains(query))
                    .Take(5)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        Username = u.Username ?? string.Empty,
                        Email = u.LoginInfo.Email,
                        PhoneNumber = u.PhoneNumber ?? string.Empty,
                        PfpUrl = u.PfpUrl ?? string.Empty,
                        Description = u.Description ?? string.Empty
                    })
                    .ToListAsync();

                _logger.LogInformation("Found {Count} users for search query: {Query}", users.Count, searchQuery);
                return (true, null, users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search users with query: {Query}", searchQuery);
                return (false, ex.Message, new List<UserDto>());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error, List<UserDto> Users)> GetUsersBatch(List<int> userIds)
        {
            try
            {
                if (userIds == null || userIds.Count == 0)
                    return (false, "User IDs list is required", new List<UserDto>());

                var users = await _ctx.Set<User>()
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        Username = u.Username ?? string.Empty,
                        PfpUrl = u.PfpUrl ?? string.Empty,
                        Description = u.Description ?? string.Empty
                    })
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} users from batch request of {RequestedCount} IDs", 
                    users.Count, userIds.Count);

                return (true, null, users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get users batch");
                return (false, ex.Message, new List<UserDto>());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error)> UpdateUser(int id, UpdateUserDto model)
        {
            try
            {
                var user = await _ctx.Set<User>().FindAsync(id);
                if (user == null)
                    return (false, "User not found");

                if (model.Username != null)
                {
                    var username = UsernameRules.Normalize(model.Username);
                    var usernameValidationError = UsernameRules.Validate(username);
                    if (usernameValidationError != null)
                        return (false, usernameValidationError);

                    if (await UsernameExistsAsync(username, excludeUserId: id))
                        return (false, "Username already taken");

                    user.Username = username;
                }

                if (model.FirstName != null)
                    user.FirstName = NormalizeOptionalValue(model.FirstName);

                if (model.LastName != null)
                    user.LastName = NormalizeOptionalValue(model.LastName);

                if (model.PhoneNumber != null)
                    user.PhoneNumber = NormalizeOptionalValue(model.PhoneNumber);

                if (model.Location != null)
                    user.Location = NormalizeOptionalValue(model.Location);

                if (model.Description != null)
                    user.Description = NormalizeOptionalValue(model.Description);

                if (model.PfpUrl != null)
                    user.PfpUrl = NormalizeOptionalValue(model.PfpUrl);

                await _ctx.SaveChangesAsync();

                _logger.LogInformation("User {UserId} updated successfully. FirstName: {FirstName}, LastName: {LastName}", 
                    id, user.FirstName, user.LastName);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user {UserId}", id);
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error)> DeleteUser(int id)
        {
            try
            {
                var user = await _ctx.Set<User>().FindAsync(id);
                if (user == null)
                    return (false, "User not found");

                _ctx.Set<User>().Remove(user);
                await _ctx.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user {UserId}", id);
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        private static string? NormalizeOptionalValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private Task<bool> UsernameExistsAsync(string? username, int? excludeUserId = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                return Task.FromResult(false);

            var normalized = username.ToLower();
            return _ctx.Set<User>().AnyAsync(user =>
                user.Username != null &&
                user.Username.ToLower() == normalized &&
                (!excludeUserId.HasValue || user.Id != excludeUserId.Value));
        }
    }
}
