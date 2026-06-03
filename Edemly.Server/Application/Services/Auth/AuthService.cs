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

        private readonly ServerDbContext _serverDbContext;
        private readonly ILogger<AuthService> _logger;
        private readonly IEmailService _emailService;
        private readonly ITenantProvider _tenantProvider;
        private readonly ITenantDbContextFactory _tenantDbContextFactory;
        private readonly IAuthResponseFactory _authResponseFactory;
        private readonly IWelcomeChatService _welcomeChatService;

        public AuthService(
            ServerDbContext serverDbContext,
            ILogger<AuthService> logger,
            IEmailService emailService,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbContextFactory,
            IAuthResponseFactory authResponseFactory,
            IWelcomeChatService welcomeChatService)
        {
            _serverDbContext = serverDbContext;
            _logger = logger;
            _emailService = emailService;
            _tenantProvider = tenantProvider;
            _tenantDbContextFactory = tenantDbContextFactory;
            _authResponseFactory = authResponseFactory;
            _welcomeChatService = welcomeChatService;
        }

        public async Task<ServiceResult> GetLoginCodeAsync(LoginRequestDto? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return ServiceResult.BadRequest("Email must be provided");
            }

            if (!EmailRegex.IsMatch(request.Email))
            {
                return ServiceResult.BadRequest("Invalid email format");
            }

            var company = _tenantProvider.CurrentCompany;
            if (company != null)
            {
                try
                {
                    await using var tenantDb = _tenantDbContextFactory.CreateCompanyDbContext(company);
                    var allowed = await tenantDb.Emails.AnyAsync(item => item.EmailAddress == request.Email);
                    if (!allowed)
                    {
                        _logger.LogWarning(
                            "Attempt to request verification code for non-allowed email {Email} in company {Company}",
                            request.Email,
                            company.Name);
                        return ServiceResult.BadRequest("Email is not allowed for this company");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while checking allowed emails for company {Company}", company.Name);
                    return ServiceResult.Unexpected("Server error while validating email for company");
                }
            }

            try
            {
                _logger.LogInformation(
                    "Generating verification code for {Email} (company: {Company})",
                    request.Email,
                    company?.Name ?? "(master)");
                var code = await _emailService.GenerateCodeAsync(request.Email);
                await _emailService.SendVerificationCodeAsync(request.Email, code);
                return ServiceResult.Ok("Verification code sent to your email");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send verification code to {Email} (company: {Company})",
                    request.Email,
                    company?.Name ?? "(master)");
                return ServiceResult.Unexpected("Failed to send verification email");
            }
        }

        public async Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginWithCodeDto request)
        {
            if (!await _emailService.VerifyCodeAsync(request.Email, request.Code))
            {
                return ServiceResult<AuthResponseDto>.Unauthorized("Invalid verification code");
            }

            var company = _tenantProvider.CurrentCompany;
            if (company != null)
            {
                await using var tenantDb = _tenantDbContextFactory.CreateCompanyDbContext(company);
                var loginInfo = await tenantDb.LoginInfos
                    .Include(item => item.User)
                    .FirstOrDefaultAsync(item => item.Email == request.Email);

                if (loginInfo?.User == null)
                {
                    return ServiceResult<AuthResponseDto>.Unauthorized("User not found");
                }

                var response = await _authResponseFactory.CreateAuthResponseAsync(loginInfo.User, loginInfo, tenantDb);
                return ServiceResult<AuthResponseDto>.Ok(response);
            }

            var masterLoginInfo = await _serverDbContext.LoginInfos
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.Email == request.Email);

            if (masterLoginInfo?.User == null)
            {
                return ServiceResult<AuthResponseDto>.Unauthorized("User not found");
            }

            var masterResponse = await _authResponseFactory.CreateAuthResponseAsync(masterLoginInfo.User, masterLoginInfo, _serverDbContext);
            return ServiceResult<AuthResponseDto>.Ok(masterResponse);
        }

        public async Task<ServiceResult<AuthResponseDto>> RegisterAsync(RegistrationWithCodeDto request)
        {
            try
            {
                if (!await _emailService.VerifyCodeAsync(request.Email, request.Code))
                {
                    _logger.LogWarning("Invalid verification code for registration: {Email}", request.Email);
                    return ServiceResult<AuthResponseDto>.Unauthorized("Invalid verification code");
                }

                var username = UsernameRules.Normalize(request.Username);
                var usernameValidationError = UsernameRules.Validate(username);
                if (usernameValidationError != null)
                {
                    return ServiceResult<AuthResponseDto>.BadRequest(usernameValidationError);
                }

                var company = _tenantProvider.CurrentCompany;

                if (company != null)
                {
                    await using var tenantDb = _tenantDbContextFactory.CreateCompanyDbContext(company);

                    var allowed = await tenantDb.Emails.AnyAsync(item => item.EmailAddress == request.Email);
                    if (!allowed)
                    {
                        return ServiceResult<AuthResponseDto>.BadRequest("Email is not allowed for registration in this company");
                    }

                    if (await tenantDb.LoginInfos.AnyAsync(item => item.Email == request.Email))
                    {
                        _logger.LogWarning("Email already exists during tenant registration: {Email}", request.Email);
                        return ServiceResult<AuthResponseDto>.Conflict("User with this email already exists");
                    }

                    if (await UsernameExistsAsync(tenantDb, username))
                    {
                        return ServiceResult<AuthResponseDto>.Conflict("Username already taken");
                    }

                    var created = await CreateUserAsync(tenantDb, request.Email, username, company.Name);

                    try
                    {
                        await _welcomeChatService.EnsureUserInWelcomeChatAsync(tenantDb, created.User.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to add tenant user {UserId} to welcome chat", created.User.Id);
                    }

                    var tenantResponse = await _authResponseFactory.CreateAuthResponseAsync(created.User, created.LoginInfo, tenantDb);
                    return ServiceResult<AuthResponseDto>.Ok(tenantResponse);
                }

                if (await _serverDbContext.LoginInfos.AnyAsync(item => item.Email == request.Email))
                {
                    _logger.LogWarning("Email already exists during master registration: {Email}", request.Email);
                    return ServiceResult<AuthResponseDto>.Conflict("User with this email already exists");
                }

                if (await UsernameExistsAsync(_serverDbContext, username))
                {
                    _logger.LogWarning("Username already taken during master registration: {Username}", username);
                    return ServiceResult<AuthResponseDto>.Conflict("Username already taken");
                }

                var masterCreated = await CreateUserAsync(_serverDbContext, request.Email, username, "master");
                _logger.LogInformation("New user registered successfully: {Username} ({Email})", username ?? "(empty)", request.Email);

                try
                {
                    await _welcomeChatService.EnsureUserInWelcomeChatAsync(_serverDbContext, masterCreated.User.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add master user {UserId} to welcome chat", masterCreated.User.Id);
                }

                var masterResponse = await _authResponseFactory.CreateAuthResponseAsync(masterCreated.User, masterCreated.LoginInfo, _serverDbContext);
                return ServiceResult<AuthResponseDto>.Ok(masterResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration for {Email}", request.Email);
                return ServiceResult<AuthResponseDto>.Unexpected("An error occurred during registration.");
            }
        }

        public async Task<ServiceResult<AuthResponseDto>> SessionLoginAsync(SessionLoginDto request)
        {
            var company = _tenantProvider.CurrentCompany;
            if (company != null)
            {
                await using var tenantDb = _tenantDbContextFactory.CreateCompanyDbContext(company);
                var session = await tenantDb.Sessions
                    .Include(item => item.User)
                        .ThenInclude(item => item.LoginInfo)
                    .FirstOrDefaultAsync(item => item.SessionToken == request.SessionToken);

                if (session?.User == null || session.ExpirationTime < DateTime.UtcNow)
                {
                    return ServiceResult<AuthResponseDto>.Unauthorized("Invalid or expired session token");
                }

                var response = await _authResponseFactory.CreateAuthResponseAsync(
                    session.User,
                    session.User.LoginInfo,
                    tenantDb,
                    rotateSessionToken: false,
                    existingSession: session);

                return ServiceResult<AuthResponseDto>.Ok(response);
            }

            var masterSession = await _serverDbContext.Sessions
                .Include(item => item.User)
                    .ThenInclude(item => item.LoginInfo)
                .FirstOrDefaultAsync(item => item.SessionToken == request.SessionToken);

            if (masterSession?.User == null || masterSession.ExpirationTime < DateTime.UtcNow)
            {
                return ServiceResult<AuthResponseDto>.Unauthorized("Invalid or expired session token");
            }

            var masterResponse = await _authResponseFactory.CreateAuthResponseAsync(
                masterSession.User,
                masterSession.User.LoginInfo,
                _serverDbContext,
                rotateSessionToken: false,
                existingSession: masterSession);

            return ServiceResult<AuthResponseDto>.Ok(masterResponse);
        }

        public async Task<ServiceResult> LogoutAsync(int userId)
        {
            var company = _tenantProvider.CurrentCompany;
            if (company != null)
            {
                await using var tenantDb = _tenantDbContextFactory.CreateCompanyDbContext(company);
                var tenantSession = await tenantDb.Sessions.FirstOrDefaultAsync(item => item.UserId == userId);
                if (tenantSession != null)
                {
                    tenantDb.Sessions.Remove(tenantSession);
                    await tenantDb.SaveChangesAsync();
                }

                return ServiceResult.Ok("Logged out successfully");
            }

            var session = await _serverDbContext.Sessions.FirstOrDefaultAsync(item => item.UserId == userId);
            if (session != null)
            {
                _serverDbContext.Sessions.Remove(session);
                await _serverDbContext.SaveChangesAsync();
            }

            return ServiceResult.Ok("Logged out successfully");
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
