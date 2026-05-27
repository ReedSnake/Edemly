using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using uchat_server.Api.DTOs;
using uchat_server.Api.Services;
using uchat_server.Configuration;
using uchat_server.Data;
using uchat_server.Data.Entities;
using uchat_server.Services;
using uchat_server.Api.Middleware;

namespace uchat_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JwtSettings _jwtSettings;
        private readonly ServerDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;
        private readonly ITenantProvider _tenantProvider;
        private readonly IConfiguration _configuration;
        private readonly ITenantDbContextFactory _tenantDbFactory;

        public AuthController(
            JwtSettings jwtSettings,
            ServerDbContext context,
            IJwtService jwtService,
            IEmailService emailService,
            ILogger<AuthController> logger,
            IUserService userService,
            ITenantProvider tenantProvider,
            IConfiguration configuration,
            ITenantDbContextFactory tenantDbFactory)
        {
            _jwtSettings = jwtSettings;
            _context = context;
            _jwtService = jwtService;
            _emailService = emailService;
            _logger = logger;
            _userService = userService;
            _tenantProvider = tenantProvider;
            _configuration = configuration;
            _tenantDbFactory = tenantDbFactory;
        }

        private Company? ResolveCompanyFromRequest()
        {
            // 1) Prefer tenant provider if set
            try
            {
                if (_tenantProvider != null && _tenantProvider.IsTenant && _tenantProvider.CurrentCompany != null)
                {
                    _logger.LogDebug("ResolveCompanyFromRequest: tenant from TenantProvider: {Company}", _tenantProvider.CurrentCompany?.Name);
                    return _tenantProvider.CurrentCompany;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ResolveCompanyFromRequest: error reading TenantProvider");
            }

            try
            {
                // 2) Try HttpContext.Items (set by middleware)
                var http = HttpContext;
                if (http != null)
                {
                    if (http.Items.TryGetValue("TenantCompany", out var itm) && itm is Company companyFromItems)
                    {
                        try { _tenantProvider.CurrentCompany = companyFromItems; } catch (Exception ex) { _logger.LogWarning(ex, "ResolveCompanyFromRequest: failed to set TenantProvider.CurrentCompany from HttpContext.Items"); }
                        _logger.LogDebug("ResolveCompanyFromRequest: tenant from HttpContext.Items: {Company}", companyFromItems.Name);
                        return companyFromItems;
                    }

                    // 3) Try query parameter 'tenant'
                    var tenantQuery = http.Request?.Query["tenant"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(tenantQuery))
                    {
                        var found = _context.Companies.AsNoTracking().FirstOrDefault(c => c.Name == tenantQuery);
                        if (found != null)
                        {
                            try { _tenantProvider.CurrentCompany = found; } catch (Exception ex) { _logger.LogWarning(ex, "ResolveCompanyFromRequest: failed to set TenantProvider.CurrentCompany from query"); }
                            _logger.LogDebug("ResolveCompanyFromRequest: tenant from query string: {Company}", found.Name);
                            return found;
                        }
                    }

                    // 4) Fallback: try to parse path first segment (note: middleware may rewrite path)
                    var path = http.Request?.Path.Value ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (segments.Length > 0)
                        {
                            var first = segments[0];
                            if (!string.Equals(first, "api", StringComparison.OrdinalIgnoreCase))
                            {
                                var company = _context.Companies.AsNoTracking().FirstOrDefault(c => c.Name == first);
                                if (company != null)
                                {
                                    try { _tenantProvider.CurrentCompany = company; } catch (Exception ex) { _logger.LogWarning(ex, "ResolveCompanyFromRequest: failed to set TenantProvider.CurrentCompany from path"); }
                                    _logger.LogDebug("ResolveCompanyFromRequest: tenant from path segment: {Company}", company.Name);
                                    return company;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ResolveCompanyFromRequest failed");
            }

            return null;
        }

        /// <summary>
        /// Крок 1 входу: надсилає verification код на email
        /// </summary>
        [HttpPost("get-code")]
        public async Task<IActionResult> GetLoginCode([FromBody] LoginRequestDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Email))
            {
                return BadRequest(new { message = "Email must be provided" });
            }

            // basic email format check
            try
            {
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
                if (!emailRegex.IsMatch(model.Email))
                    return BadRequest(new { message = "Invalid email format" });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GetLoginCode: email regex validation failed");
            }

            var company = ResolveCompanyFromRequest();

            // If tenant present, check whether email is allowed for this company
            if (company != null)
            {
                try
                {
                    await using var tenantCtx = _tenantDbFactory.CreateCompanyDbContext(company);
                    var allowed = await tenantCtx.Emails.AnyAsync(e => e.EmailAddress == model.Email);
                    if (!allowed)
                    {
                        _logger.LogWarning("Attempt to request verification code for non-allowed email {Email} in company {Company}", model.Email, company.Name);
                        return BadRequest(new { message = "Email is not allowed for this company" });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while checking allowed emails for company {Company}", company.Name);
                    return StatusCode(500, new { message = "Server error while validating email for company" });
                }
            }

            try
            {
                _logger.LogInformation("Generating verification code for {Email} (company: {Company})", model.Email, company?.Name ?? "(master)");
                var code = await _emailService.GenerateCodeAsync(model.Email);

                _logger.LogDebug("Sending verification email to {Email} (company: {Company})", model.Email, company?.Name ?? "(master)");
                await _emailService.SendVerificationCodeAsync(model.Email, code);

                return Ok(new { message = "Verification code sent to your email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification code to {Email} (company: {Company})", model.Email, company?.Name ?? "(master)");
                return StatusCode(500, new { message = "Failed to send verification email: " + ex.Message });
            }
        }

        /// <summary>
        /// Крок 2 входу: перевіряє код та повертає JWT токени
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginWithCodeDto model)
        {
            // Перевіряємо код
            if (!await _emailService.VerifyCodeAsync(model.Email, model.Code))
            {
                return Unauthorized(new { message = "Invalid verification code" });
            }

            // Resolve company either from tenant provider or request path
            var company = ResolveCompanyFromRequest();

            if (company != null)
            {
                await using var tenantCtx = _tenantDbFactory.CreateCompanyDbContext(company);
                var loginInfo = await tenantCtx.LoginInfos
                    .Include(l => l.User)
                    .FirstOrDefaultAsync(l => l.Email == model.Email);

                if (loginInfo?.User == null)
                {
                    return Unauthorized(new { message = "User not found" });
                }

                var response = await GenerateAuthResponseAsync(loginInfo.User, loginInfo, tenantCtx);
                return Ok(response);
            }

            // Інакше працюємо з master DB
            var masterLoginInfo = await _context.LoginInfos
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Email == model.Email);

            if (masterLoginInfo?.User == null)
            {
                return Unauthorized(new { message = "User not found" });
            }

            var masterResponse = await GenerateAuthResponseAsync(masterLoginInfo.User, masterLoginInfo, _context);
            return Ok(masterResponse);
        }

        /// <summary>
        /// Крок 2 реєстрації: реєструє нового користувача та повертає токени
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistrationWithCodeDto model)
        {
            try
            {
                if (!await _email_service_VerifyWrapper(model.Email, model.Code))
                {
                    _logger.LogWarning($"Invalid verification code for registration: {model.Email}");
                    return Unauthorized(new { message = "Invalid verification code" });
                }

                // Parse provided name into first/last for display; we'll generate a username
                string firstName = string.Empty;
                string lastName = string.Empty;
                if (!string.IsNullOrWhiteSpace(model.Username))
                {
                    var parts = model.Username.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0) firstName = parts[0];
                    if (parts.Length > 1) lastName = string.Join(' ', parts.Skip(1));
                }

                var company = ResolveCompanyFromRequest();

                // If tenant - create user in tenant DB and generate tenant-unique username
                if (company != null)
                {
                    var companyLocal = company;
                    await using var tenantCtx = _tenantDbFactory.CreateCompanyDbContext(companyLocal);

                    var exists = await tenantCtx.Emails.AnyAsync(e => e.EmailAddress == model.Email);
                    if (!exists)
                        return BadRequest(new { message = "Email is not allowed for registration in this company" });

                    if (await tenantCtx.LoginInfos.AnyAsync(l => l.Email == model.Email))
                    {
                        _logger.LogWarning("Email already exists during tenant registration: {Email}", model.Email);
                        return BadRequest(new { message = "User with this email already exists" });
                    }

                    // Generate a tenant-unique username based on firstName
                    var baseName = string.IsNullOrWhiteSpace(firstName) ? "user" : firstName;
                    var generatedUsername = await GenerateUniqueUsernameAsync(baseName, tenantCtx);

                    if (await tenantCtx.Users.AnyAsync(u => u.Username == generatedUsername))
                    {
                        // should not happen, but just in case
                        return BadRequest(new { message = "Failed to generate unique username" });
                    }

                    var strategy = tenantCtx.Database.CreateExecutionStrategy();

                    User? user = null;

                    await strategy.ExecuteAsync(async () =>
                    {
                        await using var transaction = await tenantCtx.Database.BeginTransactionAsync();
                        try
                        {
                            var loginInfo = new LoginInfo { Email = model.Email, IsEmailVerified = true };
                            tenantCtx.LoginInfos.Add(loginInfo);
                            await tenantCtx.SaveChangesAsync();

                            user = new User
                            {
                                Username = generatedUsername,
                                LoginInfoId = loginInfo.Id,
                                PfpUrl = null,
                                LastOnline = DateTime.UtcNow,
                                FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName,
                                LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName,
                                SubscriptionStatus = SubscriptionStatus.Free,
                                CreatedAt = DateTime.UtcNow
                            };
                            tenantCtx.Users.Add(user);
                            await tenantCtx.SaveChangesAsync();

                            await transaction.CommitAsync();
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(ex, "Error while creating tenant user {Username}", model.Username);
                            throw;
                        }
                    });

                    // Отримуємо створеного користувача
                    var loginInfoCreated = await tenantCtx.LoginInfos.Include(l => l.User).FirstOrDefaultAsync(l => l.Email == model.Email);
                    if (loginInfoCreated?.User == null)
                        return StatusCode(500, new { message = "User created but not found in tenant database" });

                    // Ensure user is member of welcome chat in tenant DB (no welcome message sent)
                    try
                    {
                        var welcomeChat = await tenantCtx.Chats.FirstOrDefaultAsync(c => c.Name == "Edemly" && c.Type == ChatType.Group);
                        if (welcomeChat == null)
                        {
                            welcomeChat = new Chat
                            {
                                Name = "Edemly",
                                Description = "Official Edemly chat",
                                IconUrl = "pack://application:,,,/Assets/logo.png",
                                Type = ChatType.Group,
                                CreatedAt = DateTime.UtcNow
                            };
                            tenantCtx.Chats.Add(welcomeChat);
                            await tenantCtx.SaveChangesAsync();
                        }

                        var existsMember = await tenantCtx.ChatMembers.AnyAsync(cm => cm.ChatId == welcomeChat.Id && cm.UserId == loginInfoCreated.User.Id);
                        if (!existsMember)
                        {
                            var member = new ChatMember
                            {
                                ChatId = welcomeChat.Id,
                                UserId = loginInfoCreated.User.Id,
                                Role = ChatMemberRole.Base,
                                JoinedAt = DateTime.UtcNow
                            };
                            tenantCtx.ChatMembers.Add(member);
                            await tenantCtx.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to add tenant user {UserId} to welcome chat", loginInfoCreated.User.Id);
                    }

                    var response = await GenerateAuthResponseAsync(loginInfoCreated.User, loginInfoCreated, tenantCtx);
                    return Ok(response);
                }

                // Master registration: generate master-unique username and create the user in the master DB directly.
                var masterBase = string.IsNullOrWhiteSpace(firstName) ? "user" : firstName;
                var masterGenerated = await GenerateUniqueUsernameAsync(masterBase, _context);

                if (await _context.LoginInfos.AnyAsync(l => l.Email == model.Email))
                {
                    _logger.LogWarning($"Email already exists during master registration: {model.Email}");
                    return BadRequest(new { message = "User with this email already exists" });
                }

                if (await _context.Users.AnyAsync(u => u.Username == masterGenerated))
                {
                    _logger.LogWarning($"Username already taken during master registration: {masterGenerated}");
                    return BadRequest(new { message = "Username already taken" });
                }

                var masterStrategy = _context.Database.CreateExecutionStrategy();
                await masterStrategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        var loginInfo = new LoginInfo
                        {
                            Email = model.Email,
                            IsEmailVerified = true
                        };
                        _context.LoginInfos.Add(loginInfo);
                        await _context.SaveChangesAsync();

                        var user = new User
                        {
                            Username = masterGenerated,
                            LoginInfoId = loginInfo.Id,
                            PfpUrl = null,
                            LastOnline = DateTime.UtcNow,
                            FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName,
                            LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName,
                            SubscriptionStatus = SubscriptionStatus.Free,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Users.Add(user);
                        await _context.SaveChangesAsync();

                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error while creating master user {Username}", masterGenerated);
                        throw;
                    }
                });

                _logger.LogInformation($"New user registered successfully: {masterGenerated} ({model.Email})");

                // Отримуємо створеного користувача з master DB
                var loginInfo = await _context.LoginInfos.Include(l => l.User).FirstOrDefaultAsync(l => l.Email == model.Email);
                if (loginInfo?.User == null)
                    return StatusCode(500, new { message = "User created but not found in database" });

                // Ensure user is member of welcome chat in master DB (no welcome message sent)
                try
                {
                    var welcomeChat = await _context.Chats.FirstOrDefaultAsync(c => c.Name == "Edemly" && c.Type == ChatType.Group);
                    if (welcomeChat == null)
                    {
                        welcomeChat = new Chat
                        {
                            Name = "Edemly",
                            Description = "Official Edemly chat",
                            IconUrl = "pack://application:,,,/Assets/logo.png",
                            Type = ChatType.Group,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Chats.Add(welcomeChat);
                        await _context.SaveChangesAsync();
                    }

                    var existsMember = await _context.ChatMembers.AnyAsync(cm => cm.ChatId == welcomeChat.Id && cm.UserId == loginInfo.User.Id);
                    if (!existsMember)
                    {
                        var member = new ChatMember
                        {
                            ChatId = welcomeChat.Id,
                            UserId = loginInfo.User.Id,
                            Role = ChatMemberRole.Base,
                            JoinedAt = DateTime.UtcNow
                        };
                        _context.ChatMembers.Add(member);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add master user {UserId} to welcome chat", loginInfo.User.Id);
                }

                var responseMaster = await GenerateAuthResponseAsync(loginInfo.User, loginInfo, _context);
                return Ok(responseMaster);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration for {Email}", model.Email);
                return StatusCode(500, new { message = "An error occurred during registration: " + ex.Message });
            }
        }

        private Task<bool> _email_service_VerifyWrapper(string email, string code)
        {
            // wrapper for potential future tenant-scoped verification; currently EmailService is global
            return _emailService.VerifyCodeAsync(email, code);
        }

        private async Task<string> GenerateUniqueUsernameAsync(string preferred, DbContext ctx)
        {
            // normalize: keep letters and digits, lower-case
            var baseName = Regex.Replace(preferred.ToLowerInvariant(), "[^a-z0-9]", string.Empty);
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "user";
            if (baseName.Length > 40) baseName = baseName.Substring(0, 40);

            var username = baseName;
            var rnd = Random.Shared;
            int attempt = 0;
            while (await ctx.Set<User>().AnyAsync(u => u.Username == username))
            {
                attempt++;
                username = baseName;
                username += rnd.Next(1000, 9999).ToString();
                if (username.Length > 50)
                    username = username.Substring(0, 50);

                if (attempt > 10)
                {
                    username = baseName + Guid.NewGuid().ToString("n").Substring(0, 6);
                    if (username.Length > 50) username = username.Substring(0, 50);
                }
            }

            return username;
        }

        /// <summary>
        /// Вхід через session token (для автоматичного входу)
        /// </summary>
        [HttpPost("session-login")]
        public async Task<IActionResult> SessionLogin([FromBody] SessionLoginDto model)
        {
            var company = ResolveCompanyFromRequest();

            // Якщо tenant присутній, перевіряємо тільки tenant DB
            if (company != null)
            {
                await using var tenantCtx = _tenantDbFactory.CreateCompanyDbContext(company);
                var session = await tenantCtx.Sessions
                    .Include(s => s.User)
                        .ThenInclude(u => u.LoginInfo)
                    .FirstOrDefaultAsync(s => s.SessionToken == model.SessionToken);

                if (session?.User == null || session.ExpirationTime < DateTime.UtcNow)
                    return Unauthorized(new { message = "Invalid or expired session token" });

                session.User.LastOnline = DateTime.UtcNow;
                await tenantCtx.SaveChangesAsync();

                bool isAdmin = string.Equals(session.User.LoginInfo.Email, _configuration["AdminEmail"], StringComparison.OrdinalIgnoreCase);

                var token = _jwtService.GenerateToken(session.User.Id, session.User.Username, session.User.LoginInfo.Email, isAdmin);

                return Ok(new AuthResponseDto
                {
                    Token = token,
                    SessionToken = session.SessionToken,
                    UserId = session.User.Id,
                    Username = session.User.Username,
                    Email = session.User.LoginInfo.Email
                });
            }

            // Інакше працюємо з master DB
            var masterSession = await _context.Sessions
                .Include(s => s.User)
                    .ThenInclude(u => u.LoginInfo)
                .FirstOrDefaultAsync(s => s.SessionToken == model.SessionToken);

            if (masterSession?.User == null || masterSession.ExpirationTime < DateTime.UtcNow)
                return Unauthorized(new { message = "Invalid or expired session token" });

            masterSession.User.LastOnline = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            bool masterIsAdmin = string.Equals(masterSession.User.LoginInfo.Email, _configuration["AdminEmail"], StringComparison.OrdinalIgnoreCase);

            var masterToken = _jwt_service_GenerateTokenWrapper(masterSession.User.Id, masterSession.User.Username, masterSession.User.LoginInfo.Email, masterIsAdmin);

            return Ok(new AuthResponseDto
            {
                Token = masterToken,
                SessionToken = masterSession.SessionToken,
                UserId = masterSession.User.Id,
                Username = masterSession.User.Username,
                Email = masterSession.User.LoginInfo.Email
            });
        }

        private string _jwt_service_GenerateTokenWrapper(int userId, string username, string email, bool isAdmin)
        {
            return _jwtService.GenerateToken(userId, username, email, isAdmin);
        }

        /// <summary>
        /// Вихід з системи (видалити session)
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            // Якщо tenant присутній, видаляємо сесію з tenant DB
            var company = ResolveCompanyFromRequest();
            if (company != null)
            {
                await using var tenantCtx = _tenantDbFactory.CreateCompanyDbContext(company);
                var tenantSession = await tenantCtx.Sessions.FirstOrDefaultAsync(s => s.UserId == userId);
                if (tenantSession != null)
                {
                    tenantCtx.Sessions.Remove(tenantSession);
                    await tenantCtx.SaveChangesAsync();
                }

                return Ok(new { message = "Logged out successfully" });
            }

            // Інакше видаляємо сесію з master DB
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.UserId == userId);
            if (session != null)
            {
                _context.Sessions.Remove(session);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Logged out successfully" });
        }

        /// <summary>
        /// Приватний метод для генерації відповіді з токенами
        /// Сесія завжди створюється у master DB. Якщо користувач знайдений у tenant DB,
        /// його запис про LastOnline оновлюється у tenant DB, але сесія створюється у master DB.
        /// Якщо у master нема відповідного LoginInfo/User - вони створюються як shadow записи.
        /// </summary>
        private async Task<AuthResponseDto> GenerateAuthResponseAsync(User user, LoginInfo loginInfo, DbContext? dbContext = null)
        {
            var ctx = dbContext ?? _context;

            // Оновлюємо LastOnline у джерельній БД (tenant або master)
            user.LastOnline = DateTime.UtcNow;
            try
            {
                ctx.Update(user);
                await ctx.SaveChangesAsync();
            }
            catch
            {
                // ignore update failures for detached entities
            }

            // Створюємо або оновлюємо сесію в тій же БД, де живе користувач
            var sessions = ctx.Set<Session>();
            var session = await sessions.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (session == null)
            {
                session = new Session
                {
                    UserId = user.Id,
                    SessionToken = Guid.NewGuid().ToString(),
                    ExpirationTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiresInDays)
                };
                sessions.Add(session);
            }
            else
            {
                session.SessionToken = Guid.NewGuid().ToString();
                session.ExpirationTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiresInDays);
            }

            await ctx.SaveChangesAsync();

            var adminEmail = _configuration["AdminEmail"];
            if (string.IsNullOrWhiteSpace(adminEmail)) adminEmail = "admin@uchat.com";

            bool isAdmin = string.Equals(loginInfo.Email, adminEmail, StringComparison.OrdinalIgnoreCase);

            var token = _jwtService.GenerateToken(user.Id, user.Username, loginInfo.Email, isAdmin);

            return new AuthResponseDto
            {
                Token = token,
                SessionToken = session.SessionToken,
                UserId = user.Id,
                Username = user.Username,
                Email = loginInfo.Email
            };
        }
    }
}