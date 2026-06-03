using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Contracts.Auth;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;

namespace Edemly.Server.Api.Services
{
    public class AuthService : IAuthService
    {
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly ServerDbContext _serverDb;
        private readonly ILogger<AuthService> _logger;
        private readonly IEmailService _emailService;
        private readonly ITenantProvider _tenantProvider;
        private readonly ITenantDbContextFactory _tenantDbFactory;
        private readonly IAuthResponseFactory _authResponseFactory;
        private readonly IWelcomeChatService _welcomeChatService;

        public AuthService(
            ServerDbContext serverDb,
            ILogger<AuthService> logger,
            IEmailService emailService,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbFactory,
            IAuthResponseFactory authResponseFactory,
            IWelcomeChatService welcomeChatService)
        {
            _serverDb = serverDb;
            _logger = logger;
            _emailService = emailService;
            _tenantProvider = tenantProvider;
            _tenantDbFactory = tenantDbFactory;
            _authResponseFactory = authResponseFactory;
            _welcomeChatService = welcomeChatService;
        }

        public async Task<AuthMessageResult> GetLoginCodeAsync(LoginRequestDto? model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Email))
            {
                return MessageFailure(StatusCodes.Status400BadRequest, "Email must be provided");
            }

            if (!EmailRegex.IsMatch(model.Email))
            {
                return MessageFailure(StatusCodes.Status400BadRequest, "Invalid email format");
            }

            var company = _tenantProvider.CurrentCompany;
            if (company != null)
            {
                try
                {
                    await using var tenantDb = _tenantDbFactory.CreateCompanyDbContext(company);
                    var allowed = await tenantDb.Emails.AnyAsync(item => item.EmailAddress == model.Email);
                    if (!allowed)
                    {
                        _logger.LogWarning(
                            "Attempt to request verification code for non-allowed email {Email} in company {Company}",
                            model.Email,
                            company.Name);
                        return MessageFailure(StatusCodes.Status400BadRequest, "Email is not allowed for this company");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while checking allowed emails for company {Company}", company.Name);
                    return MessageFailure(StatusCodes.Status500InternalServerError, "Server error while validating email for company");
                }
            }

            try
            {
                _logger.LogInformation(
                    "Generating verification code for {Email} (company: {Company})",
                    model.Email,
                    company?.Name ?? "(master)");
                var code = await _emailService.GenerateCodeAsync(model.Email);
                await _emailService.SendVerificationCodeAsync(model.Email, code);
                return MessageSuccess("Verification code sent to your email");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send verification code to {Email} (company: {Company})",
                    model.Email,
                    company?.Name ?? "(master)");
                return MessageFailure(StatusCodes.Status500InternalServerError, "Failed to send verification email: " + ex.Message);
            }
        }

        public async Task<AuthResponseResult> LoginAsync(LoginWithCodeDto model)
        {
            if (!await _emailService.VerifyCodeAsync(model.Email, model.Code))
            {
                return ResponseFailure(StatusCodes.Status401Unauthorized, "Invalid verification code");
            }

            var company = _tenantProvider.CurrentCompany;
            if (company != null)
            {
                await using var tenantDb = _tenantDbFactory.CreateCompanyDbContext(company);
                var loginInfo = await tenantDb.LoginInfos
                    .Include(item => item.User)
                    .FirstOrDefaultAsync(item => item.Email == model.Email);

                if (loginInfo?.User == null)
                {
                    return ResponseFailure(StatusCodes.Status401Unauthorized, "User not found");
                }

                var response = await _authResponseFactory.CreateAuthResponseAsync(loginInfo.User, loginInfo, tenantDb);
                return ResponseSuccess(response);
            }

            var masterLoginInfo = await _serverDb.LoginInfos
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.Email == model.Email);

            if (masterLoginInfo?.User == null)
            {
                return ResponseFailure(StatusCodes.Status401Unauthorized, "User not found");
            }

            var masterResponse = await _authResponseFactory.CreateAuthResponseAsync(masterLoginInfo.User, masterLoginInfo, _serverDb);
            return ResponseSuccess(masterResponse);
        }

        public async Task<AuthResponseResult> RegisterAsync(RegistrationWithCodeDto model)
        {
            try
            {
                if (!await _emailService.VerifyCodeAsync(model.Email, model.Code))
                {
                    _logger.LogWarning("Invalid verification code for registration: {Email}", model.Email);
                    return ResponseFailure(StatusCodes.Status401Unauthorized, "Invalid verification code");
                }

                var username = UsernameRules.Normalize(model.Username);
                var usernameValidationError = UsernameRules.Validate(username);
                if (usernameValidationError != null)
                {
                    return ResponseFailure(StatusCodes.Status400BadRequest, usernameValidationError);
                }

                var company = _tenantProvider.CurrentCompany;

                if (company != null)
                {
                    await using var tenantDb = _tenantDbFactory.CreateCompanyDbContext(company);

                    var allowed = await tenantDb.Emails.AnyAsync(item => item.EmailAddress == model.Email);
                    if (!allowed)
                    {
                        return ResponseFailure(StatusCodes.Status400BadRequest, "Email is not allowed for registration in this company");
                    }

                    if (await tenantDb.LoginInfos.AnyAsync(item => item.Email == model.Email))
                    {
                        _logger.LogWarning("Email already exists during tenant registration: {Email}", model.Email);
                        return ResponseFailure(StatusCodes.Status400BadRequest, "User with this email already exists");
                    }

                    if (await UsernameExistsAsync(tenantDb, username))
                    {
                        return ResponseFailure(StatusCodes.Status400BadRequest, "Username already taken");
                    }

                    var created = await CreateUserAsync(tenantDb, model.Email, username, company.Name);

                    try
                    {
                        await _welcomeChatService.EnsureUserInWelcomeChatAsync(tenantDb, created.User.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to add tenant user {UserId} to welcome chat", created.User.Id);
                    }

                    var tenantResponse = await _authResponseFactory.CreateAuthResponseAsync(created.User, created.LoginInfo, tenantDb);
                    return ResponseSuccess(tenantResponse);
                }

                if (await _serverDb.LoginInfos.AnyAsync(item => item.Email == model.Email))
                {
                    _logger.LogWarning("Email already exists during master registration: {Email}", model.Email);
                    return ResponseFailure(StatusCodes.Status400BadRequest, "User with this email already exists");
                }

                if (await UsernameExistsAsync(_serverDb, username))
                {
                    _logger.LogWarning("Username already taken during master registration: {Username}", username);
                    return ResponseFailure(StatusCodes.Status400BadRequest, "Username already taken");
                }

                var masterCreated = await CreateUserAsync(_serverDb, model.Email, username, "master");
                _logger.LogInformation("New user registered successfully: {Username} ({Email})", username ?? "(empty)", model.Email);

                try
                {
                    await _welcomeChatService.EnsureUserInWelcomeChatAsync(_serverDb, masterCreated.User.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add master user {UserId} to welcome chat", masterCreated.User.Id);
                }

                var masterResponse = await _authResponseFactory.CreateAuthResponseAsync(masterCreated.User, masterCreated.LoginInfo, _serverDb);
                return ResponseSuccess(masterResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration for {Email}", model.Email);
                return ResponseFailure(StatusCodes.Status500InternalServerError, "An error occurred during registration: " + ex.Message);
            }
        }

        public async Task<AuthResponseResult> SessionLoginAsync(SessionLoginDto model)
        {
            var company = _tenantProvider.CurrentCompany;
            if (company != null)
            {
                await using var tenantDb = _tenantDbFactory.CreateCompanyDbContext(company);
                var session = await tenantDb.Sessions
                    .Include(item => item.User)
                        .ThenInclude(item => item.LoginInfo)
                    .FirstOrDefaultAsync(item => item.SessionToken == model.SessionToken);

                if (session?.User == null || session.ExpirationTime < DateTime.UtcNow)
                {
                    return ResponseFailure(StatusCodes.Status401Unauthorized, "Invalid or expired session token");
                }

                var response = await _authResponseFactory.CreateAuthResponseAsync(
                    session.User,
                    session.User.LoginInfo,
                    tenantDb,
                    rotateSessionToken: false,
                    existingSession: session);

                return ResponseSuccess(response);
            }

            var masterSession = await _serverDb.Sessions
                .Include(item => item.User)
                    .ThenInclude(item => item.LoginInfo)
                .FirstOrDefaultAsync(item => item.SessionToken == model.SessionToken);

            if (masterSession?.User == null || masterSession.ExpirationTime < DateTime.UtcNow)
            {
                return ResponseFailure(StatusCodes.Status401Unauthorized, "Invalid or expired session token");
            }

            var masterResponse = await _authResponseFactory.CreateAuthResponseAsync(
                masterSession.User,
                masterSession.User.LoginInfo,
                _serverDb,
                rotateSessionToken: false,
                existingSession: masterSession);

            return ResponseSuccess(masterResponse);
        }

        public async Task<AuthMessageResult> LogoutAsync(int userId)
        {
            var company = _tenantProvider.CurrentCompany;
            if (company != null)
            {
                await using var tenantDb = _tenantDbFactory.CreateCompanyDbContext(company);
                var tenantSession = await tenantDb.Sessions.FirstOrDefaultAsync(item => item.UserId == userId);
                if (tenantSession != null)
                {
                    tenantDb.Sessions.Remove(tenantSession);
                    await tenantDb.SaveChangesAsync();
                }

                return MessageSuccess("Logged out successfully");
            }

            var session = await _serverDb.Sessions.FirstOrDefaultAsync(item => item.UserId == userId);
            if (session != null)
            {
                _serverDb.Sessions.Remove(session);
                await _serverDb.SaveChangesAsync();
            }

            return MessageSuccess("Logged out successfully");
        }

        private async Task<(LoginInfo LoginInfo, User User)> CreateUserAsync(
            DbContext dbContext,
            string email,
            string? username,
            string scopeName)
        {
            LoginInfo? createdLoginInfo = null;
            User? createdUser = null;
            var strategy = dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync();

                try
                {
                    var loginInfo = new LoginInfo
                    {
                        Email = email,
                        IsEmailVerified = true
                    };
                    dbContext.Set<LoginInfo>().Add(loginInfo);
                    await dbContext.SaveChangesAsync();

                    var user = new User
                    {
                        Username = username,
                        LoginInfoId = loginInfo.Id,
                        PfpUrl = null,
                        LastOnline = DateTime.UtcNow,
                        FirstName = null,
                        LastName = null,
                        SubscriptionStatus = SubscriptionStatus.Free,
                        CreatedAt = DateTime.UtcNow
                    };
                    dbContext.Set<User>().Add(user);
                    await dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();
                    createdLoginInfo = loginInfo;
                    createdUser = user;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error while creating {Scope} user {Username}", scopeName, username);
                    throw;
                }
            });

            if (createdLoginInfo == null || createdUser == null)
            {
                throw new InvalidOperationException("User created but not available after registration");
            }

            return (createdLoginInfo, createdUser);
        }

        private static AuthMessageResult MessageSuccess(string message)
        {
            return new AuthMessageResult(true, StatusCodes.Status200OK, message);
        }

        private static AuthMessageResult MessageFailure(int statusCode, string message)
        {
            return new AuthMessageResult(false, statusCode, message);
        }

        private static AuthResponseResult ResponseSuccess(AuthResponseDto response)
        {
            return new AuthResponseResult(true, StatusCodes.Status200OK, response, null);
        }

        private static AuthResponseResult ResponseFailure(int statusCode, string message)
        {
            return new AuthResponseResult(false, statusCode, null, message);
        }

        private static async Task<bool> UsernameExistsAsync(DbContext dbContext, string? username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            var normalized = username.ToLower();
            return await dbContext.Set<User>()
                .AnyAsync(item => item.Username != null && item.Username.ToLower() == normalized);
        }
    }
}
