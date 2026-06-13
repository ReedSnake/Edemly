using Edemly.Server.Api.Hubs;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Application.Auth;
using Edemly.Server.Application.Calls;
using Edemly.Server.Application.ChatMembers;
using Edemly.Server.Application.Chats;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Companies;
using Edemly.Server.Application.Messages;
using Edemly.Server.Application.Notes;
using Edemly.Server.Application.Payments;
using Edemly.Server.Application.Remindings;
using Edemly.Server.Application.Users;
using Edemly.Server.Application.Welcome;
using Edemly.Server.Configuration;
using Edemly.Server.Data;
using Edemly.Server.Infrastructure.Auth;
using Edemly.Server.Infrastructure.BackgroundServices;
using Edemly.Server.Infrastructure.Caching;
using Edemly.Server.Infrastructure.Files;
using Edemly.Server.Infrastructure.Hosting;
using Edemly.Server.Infrastructure.Payments;
using Edemly.Server.Infrastructure.Presence;
using Edemly.Server.Infrastructure.Realtime;
using Edemly.Server.Infrastructure.Tenancy;
using Minio;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Edemly.Server
{
    public partial class Program
    {
        public static Task Main(string[] args)
        {
            return MainAsync(args);
        }

        private static async Task MainAsync(string[] args)
        {
            var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            bool isEfTools = processName.Contains("ef", StringComparison.OrdinalIgnoreCase) ||
                            processName.Contains("dotnet-ef", StringComparison.OrdinalIgnoreCase) ||
                            Environment.GetCommandLineArgs().Any(arg =>
                                arg.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
                                arg.Contains("ef.dll", StringComparison.OrdinalIgnoreCase));

            if (isEfTools)
            {
                return;
            }

            var builder = WebApplication.CreateBuilder(args);

            if (!TryGetPort(args, builder.Configuration, out int port, out string? invalidPort))
            {
                Console.WriteLine($"Error: Invalid port number '{invalidPort}'. Port must be between 1 and 65535.");
                ShowUsage();
                Environment.Exit(1);
                return;
            }

            string? publicBaseUrl = builder.Configuration["PublicBaseUrl"]
                ?? Environment.GetEnvironmentVariable("EDEMLY_PUBLIC_URL");

            if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            {
                if (publicBaseUrl.EndsWith('/')) publicBaseUrl = publicBaseUrl.TrimEnd('/');
            }

            builder.Services.AddSingleton<IPublicUrlProvider>(new PublicUrlProvider(publicBaseUrl));

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenAnyIP(port);
            });

            var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
                ?? throw new InvalidOperationException("JWT settings are not configured");

            builder.Services.AddMemoryCache();

            builder.Services.AddHttpContextAccessor();

            builder.Services.AddSingleton(jwtSettings);

            var brevoSettings = builder.Configuration.GetSection("Brevo").Get<BrevoSettings>()
                ?? throw new InvalidOperationException("Brevo settings are not configured");

            builder.Services.AddSingleton(brevoSettings);

            var fileStorageSettings = builder.Configuration.GetSection("FileStorage").Get<FileStorageSettings>()
                ?? new FileStorageSettings();

            ApplyMinioEnvironmentFallbacks(fileStorageSettings);
            builder.Services.AddSingleton(fileStorageSettings);

            if (fileStorageSettings.UseMinio)
            {
                if (string.IsNullOrWhiteSpace(fileStorageSettings.Minio.AccessKey) ||
                    string.IsNullOrWhiteSpace(fileStorageSettings.Minio.SecretKey))
                {
                    throw new InvalidOperationException("FileStorage:Minio access key and secret key must be configured when FileStorage:Provider is Minio.");
                }

                builder.Services.AddSingleton<IMinioClient>(_ =>
                    new MinioClient()
                        .WithEndpoint(fileStorageSettings.Minio.Endpoint)
                        .WithCredentials(fileStorageSettings.Minio.AccessKey, fileStorageSettings.Minio.SecretKey)
                        .WithSSL(fileStorageSettings.Minio.Secure)
                        .Build());
            }

            builder.Services.AddSingleton<ChatCacheRegistry>();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found");

            builder.Services.AddDbContext<ServerDbContext>(options =>
                options.UseMySql(
                    connectionString,
                    ServerVersion.Create(new Version(8, 0, 36), Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MySql),
                    mysqlOptions =>
                    {
                        mysqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null
                        );
                        mysqlOptions.MigrationsAssembly("Edemly.Server");
                    }
                )
            );

            builder.Services.AddScoped<ITenantProvider, TenantProvider>();

            builder.Services.AddScoped<TenantProvisioningService>();

            builder.Services.AddSingleton<ITenantDbContextFactory, TenantDbContextFactory>();

            builder.Services.AddScoped<IJwtService, JwtService>();
            var brevoKey = builder.Configuration["Brevo:ApiKey"];
            if (string.IsNullOrWhiteSpace(brevoKey) || brevoKey == "MOCK_MODE")
            {
                builder.Services.AddScoped<IEmailService, MockEmailService>();
                Console.WriteLine("[INFO] Email Service: Робота в тестовому режимі (Mock). Коди будуть виводитись у консоль.");
            }
            else
            {
                builder.Services.AddScoped<IEmailService, EmailService>();
                Console.WriteLine("[INFO] Email Service: Підключено реальний API (Brevo).");
            }
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IAuthResponseFactory, AuthResponseFactory>();
            builder.Services.AddScoped<IWelcomeChatService, WelcomeChatService>();
            builder.Services.AddScoped<IMessageService, MessageService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IChatRealtimeNotifier, SignalRChatRealtimeNotifier>();
            builder.Services.AddScoped<ICallService, CallService>();
            builder.Services.AddScoped<IChatMemberService, ChatMemberService>();
            builder.Services.AddScoped<INoteService, NoteService>();
            builder.Services.AddScoped<IRemindingService, RemindingService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<IPermissionService, PermissionService>();
            builder.Services.AddScoped<IFileStorageService, FileStorageService>();
            builder.Services.AddScoped<ICompanyService, CompanyService>();
            builder.Services.AddHttpClient<WayForPayService>();

            builder.Services.AddSingleton<UserPresenceService>();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/main") || path.StartsWithSegments("/call")))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddAuthorization();

            builder.Services.AddControllers();

            var signalRSettings = builder.Configuration.GetSection("SignalR").Get<SignalRSettings>()
                ?? new SignalRSettings();

            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = signalRSettings.EnableDetailedErrors;
            });

            builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, JwtUserIdProvider>();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("DefaultPolicy", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "Edemly API", Version = "v1" });

                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            builder.Services.AddHostedService<ServerMaintenanceWorker>();
            builder.Services.AddScoped<WelcomeChatInitializer>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var useDatabaseMigrations = builder.Configuration.GetValue("Startup:UseDatabaseMigrations", true)
                    && !app.Environment.IsEnvironment("Testing");
                var seedDatabase = builder.Configuration.GetValue("Startup:SeedDatabase", true);

                try
                {
                    if (useDatabaseMigrations)
                    {
                        var canConnect = await dbContext.Database.CanConnectAsync();

                        if (canConnect)
                        {
                            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

                            if (pendingMigrations.Any())
                            {
                                await dbContext.Database.MigrateAsync();
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException("Cannot connect to database");
                        }
                    }
                    else
                    {
                        await dbContext.Database.EnsureCreatedAsync();
                    }

                    if (seedDatabase)
                    {
                        await DbSeeder.SeedAdminAsync(scope.ServiceProvider);

                        var welcomeChatInitializer = scope.ServiceProvider.GetRequiredService<WelcomeChatInitializer>();
                        await welcomeChatInitializer.InitializeWelcomeChatAsync();
                    }

                    if (useDatabaseMigrations)
                    {
                        try
                        {
                            var provisioning = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
                            var companies = await provisioning.ListCompaniesAsync();

                            foreach (var company in companies)
                            {
                                try
                                {
                                    logger.LogInformation("Applying migrations to tenant DB for company {Company} -> {DbName}", company.Name, company.DbName);

                                    var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection")
                                        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found");
                                    var tenantConn = new MySqlConnector.MySqlConnectionStringBuilder(defaultConn) { Database = company.DbName }.ToString();

                                    var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();
                                    optionsBuilder.UseMySql(tenantConn, ServerVersion.AutoDetect(tenantConn), mysqlOptions =>
                                    {
                                        mysqlOptions.MigrationsAssembly("Edemly.Server");
                                    });

                                    using var tenantCtx = new CompanyDbContext(optionsBuilder.Options);
                                    await tenantCtx.Database.MigrateAsync();

                                    logger.LogInformation("Applied migrations to tenant DB {DbName}", company.DbName);
                                }
                                catch (Exception exTenant)
                                {
                                    logger.LogError(exTenant, "Failed to apply migrations for tenant {Company}", company.Name);
                                }
                            }
                        }
                        catch (Exception exAllTenants)
                        {
                            logger.LogWarning(exAllTenants, "Automatic tenant migrations failed");
                        }
                    }
                }
                catch
                {
                    throw;
                }
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors("DefaultPolicy");

            app.UseDefaultFiles();

            app.UseMiddleware<TenantResolutionMiddleware>();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<EnsureUploadsAuthMiddleware>();

            app.UseStaticFiles();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/health", () => Results.Ok(new
                {
                    status = "ok",
                    service = "Edemly.Server"
                }));

                endpoints.MapControllers();
                endpoints.MapHub<MainHub>("/main");
                endpoints.MapHub<CallHub>("/call");
            });

            app.Run();
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Usage: Edemly.Server [port]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  [port]    Port number to listen on (1-65535). Defaults to PORT, ASPNETCORE_PORT, or 8100.");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  Edemly.Server 8100");
            Console.WriteLine("  Edemly.Server 9735");
        }

        private static bool TryGetPort(string[] args, IConfiguration configuration, out int port, out string? invalidPort)
        {
            var portStr = args.FirstOrDefault(arg => !arg.StartsWith("-", StringComparison.Ordinal));
            portStr ??= configuration["PORT"];
            portStr ??= configuration["ASPNETCORE_PORT"];
            portStr ??= Environment.GetEnvironmentVariable("PORT");
            portStr ??= Environment.GetEnvironmentVariable("ASPNETCORE_PORT");
            portStr ??= "8100";

            invalidPort = null;

            if (int.TryParse(portStr, out port) && port is >= 1 and <= 65535)
            {
                return true;
            }

            invalidPort = portStr;
            port = 0;
            return false;
        }

        private static void ApplyMinioEnvironmentFallbacks(FileStorageSettings settings)
        {
            if (!settings.UseMinio)
            {
                return;
            }

            settings.Minio.Endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT")
                ?? settings.Minio.Endpoint;
            settings.Minio.AccessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY")
                ?? Environment.GetEnvironmentVariable("MINIO_ROOT_USER")
                ?? settings.Minio.AccessKey;
            settings.Minio.SecretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY")
                ?? Environment.GetEnvironmentVariable("MINIO_ROOT_PASSWORD")
                ?? settings.Minio.SecretKey;

            if (bool.TryParse(Environment.GetEnvironmentVariable("MINIO_SECURE"), out var secure))
            {
                settings.Minio.Secure = secure;
            }
        }
    }
}
