using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Server.Api.DTOs;
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

        public async Task<(bool Success, string? Error)> CreateUser(UserCreateDto model)
        {
            try
            {
                if (await _ctx.Set<LoginInfo>().AnyAsync(l => l.Email == model.Email))
                {
                    _logger.LogWarning("Email already exists during registration: {Email}", model.Email);
                    return (false, "User with this email already exists");
                }

                if (await _ctx.Set<User>().AnyAsync(u => u.Username == model.Username))
                {
                    _logger.LogWarning("Username already taken during registration: {Username}", model.Username);
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
                            Username = model.Username,
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

                        _logger.LogInformation("User created: {Username}, ID: {Id}", model.Username, user.Id);

                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error while creating user {Username}", model.Username);
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

        public async Task<(bool Success, string? Error, UserGetSelfDto? User)> GetFullInfo(int id)
        {
            try
            {
                var user = await _ctx.Set<User>()
                    .Include(u => u.LoginInfo)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return (false, "User not found", null);

                var dto = new UserGetSelfDto
                {
                    Id = user.Id,
                    Username = user.Username,
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

        public async Task<(bool Success, string? Error, UserGetDto? User)> GetById(int id)
        {
            try
            {
                var user = await _ctx.Set<User>()
                    .Include(u => u.LoginInfo)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return (false, "User not found", null);

                var dto = new UserGetDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.LoginInfo.Email,
                    PhoneNumber = user.PhoneNumber,
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

        public async Task<(bool Success, string? Error, List<UserGetDto> Users)> SearchUsers(string searchQuery)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchQuery))
                    return (false, "Search query cannot be empty", new List<UserGetDto>());

                var query = searchQuery.Trim().ToLower();

                var users = await _ctx.Set<User>()
                    .Include(u => u.LoginInfo)
                    .Where(u => u.Username.ToLower().Contains(query) ||
                                u.LoginInfo.Email.ToLower().Contains(query))
                    .Take(5)
                    .Select(u => new UserGetDto
                    {
                        Id = u.Id,
                        Username = u.Username,
                        Email = u.LoginInfo.Email,
                        PhoneNumber = u.PhoneNumber,
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
                return (false, ex.Message, new List<UserGetDto>());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error, List<UserGetDto> Users)> GetUsersBatch(List<int> userIds)
        {
            try
            {
                if (userIds == null || userIds.Count == 0)
                    return (false, "User IDs list is required", new List<UserGetDto>());

                var users = await _ctx.Set<User>()
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new UserGetDto
                    {
                        Id = u.Id,
                        Username = u.Username,
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
                return (false, ex.Message, new List<UserGetDto>());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error)> UpdateUser(int id, UserUpdateDto model)
        {
            try
            {
                var user = await _ctx.Set<User>().FindAsync(id);
                if (user == null)
                    return (false, "User not found");

                if (model.Username != null)
                    user.Username = model.Username;

                if (model.FirstName != null)
                    user.FirstName = model.FirstName;

                if (model.LastName != null)
                    user.LastName = model.LastName;

                if (model.PhoneNumber != null)
                    user.PhoneNumber = model.PhoneNumber;

                if (model.Location != null)
                    user.Location = model.Location;

                if (model.Description != null)
                    user.Description = model.Description;

                if (model.PfpUrl != null)
                    user.PfpUrl = model.PfpUrl;

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
    }
}
