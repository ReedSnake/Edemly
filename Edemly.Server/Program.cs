using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Edemly.Server.Api.Services;
using Edemly.Server.Configuration;
using Edemly.Server.Data;
using Edemly.Server.Hubs;
using Edemly.Server.Services;
using Edemly.Server.Utils;
using Edemly.Server.Api.Middleware;

namespace Edemly.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Перевірка чи це EF Core tools через ProcessName
            //DaemonHelper.Daemonize();
            var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            bool isEfTools = processName.Contains("ef", StringComparison.OrdinalIgnoreCase) ||
                            processName.Contains("dotnet-ef", StringComparison.OrdinalIgnoreCase) ||
                            Environment.GetCommandLineArgs().Any(arg =>
                                arg.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
                                arg.Contains("ef.dll", StringComparison.OrdinalIgnoreCase));

            // Якщо EF Tools — просто виходимо, порт не потрібен
            if (isEfTools)
            {
                return;
            }

            if (args.Length == 0)
            {
                Console.WriteLine("Error: Port number is required.");
                ShowUsage();
                Environment.Exit(1);
                return;
            }
            // Determine port: prefer first CLI arg, then environment PORT/ASPNETCORE_PORT, otherwise default 8100
            string portStr = args[0];
            if (!int.TryParse(portStr, out int port) || port < 1 || port > 65535)
            {
                Console.WriteLine($"Error: Invalid port number '{portStr}'. Port must be between 1 and 65535.");
                ShowUsage();
                Environment.Exit(1);
                return;
            }

            var builder = WebApplication.CreateBuilder(args);

            // Allow overriding public base URL via config or environment
            string publicBaseUrl = builder.Configuration["PublicBaseUrl"]
                ?? Environment.GetEnvironmentVariable("EDEMLY_PUBLIC_URL");

            if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            {
                // normalize
                if (publicBaseUrl.EndsWith('/')) publicBaseUrl = publicBaseUrl.TrimEnd('/');
            }

            // Register provider for other services
            builder.Services.AddSingleton<IPublicUrlProvider>(new PublicUrlProvider(publicBaseUrl));

            // Налаштування Kestrel для використання переданого порту
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenAnyIP(port);
            });

            // Конфігурація JWT
            var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
                ?? throw new InvalidOperationException("JWT settings are not configured");

            builder.Services.AddMemoryCache();

            builder.Services.AddHttpContextAccessor();

            builder.Services.AddSingleton(jwtSettings);

            // Конфігурація Brevo
            var brevoSettings = builder.Configuration.GetSection("Brevo").Get<BrevoSettings>()
                ?? throw new InvalidOperationException("Brevo settings are not configured");

            builder.Services.AddSingleton(brevoSettings);

            // Конфігурація File Storage (замість MinIO)
            var fileStorageSettings = builder.Configuration.GetSection("FileStorage").Get<FileStorageSettings>()
                ?? new FileStorageSettings();

            builder.Services.AddSingleton(fileStorageSettings);

            builder.Services.AddSingleton<ChatCacheRegistry>();

            // Додаємо DbContext з MySQL
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found");

            builder.Services.AddDbContext<ServerDbContext>(options =>
                options.UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString),
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

            // Tenant provider & middleware
            builder.Services.AddScoped<ITenantProvider, TenantProvider>();

            // Register tenant provisioning service
            builder.Services.AddScoped<TenantProvisioningService>();

            // Register tenant DB context factory
            builder.Services.AddSingleton<ITenantDbContextFactory, TenantDbContextFactory>();

            // Реєстрація сервісів
            builder.Services.AddScoped<IJwtService, JwtService>();
            // --- Смарт-реєстрація Email Service ---
            var brevoKey = builder.Configuration["Brevo:ApiKey"];
            if (string.IsNullOrWhiteSpace(brevoKey) || brevoKey == "MOCK_MODE")
            {
                // Якщо ключа немає або включено Mock-режим — підкидаємо заглушку
                builder.Services.AddScoped<IEmailService, MockEmailService>();
                Console.WriteLine("[INFO] Email Service: Робота в тестовому режимі (Mock). Коди будуть виводитись у консоль.");
            }
            else
            {
                // Якщо є справжній ключ — використовуємо Brevo
                builder.Services.AddScoped<IEmailService, EmailService>();
                Console.WriteLine("[INFO] Email Service: Підключено реальний API (Brevo).");
            }
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IMessageService, MessageService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IChatMemberService, ChatMemberService>();
            builder.Services.AddScoped<INoteService, NoteService>();
            builder.Services.AddScoped<IRemindingService, RemindingService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<IPermissionService, PermissionService>();
            builder.Services.AddScoped<IFileStorageService, FileStorageService>();
            builder.Services.AddHttpClient<WayForPayService>();
            
            // Singleton сервіс для відстеження онлайн-статусу
            builder.Services.AddSingleton<Services.UserPresenceService>();

            // Додаємо Authentication з JWT
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

                // Для SignalR - дозволяємо отримувати токен через query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        // Accept token for main SignalR hub and call hub negotiations
                        if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/main") || path.StartsWithSegments("/call")) )
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddAuthorization();

            // Додаємо Controllers для REST API
            builder.Services.AddControllers();

            // Додаємо SignalR
            var signalRSettings = builder.Configuration.GetSection("SignalR").Get<SignalRSettings>()
                ?? new SignalRSettings();

            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = signalRSettings.EnableDetailedErrors;
            });

            // Register custom IUserIdProvider to map JWT claim to SignalR user identifier
            builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, Hubs.JwtUserIdProvider>();

            // Додаємо CORS - дозволяємо будь-який origin
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("DefaultPolicy", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            // Swagger/OpenAPI
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "Edemly API", Version = "v1" });

                // Додаємо JWT в Swagger для тестування API
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

            // Worker Service для фонових завдань
            builder.Services.AddHostedService<ServerMaintenanceWorker>();

            var app = builder.Build();

            // Ініціалізація бази даних та seeding при старті
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                try
                {
                    // Перевіряємо чи можна підключитися до бази даних
                    var canConnect = await dbContext.Database.CanConnectAsync();

                    if (canConnect)
                    {
                        // Застосовуємо міграції якщо є незастосовані
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

                    // Seed admin користувача
                    await DbSeeder.SeedAdminAsync(scope.ServiceProvider);

                    // Ініціалізація привітального чату
                    var welcomeChatLogger = scope.ServiceProvider.GetRequiredService<ILogger<WelcomeChatInitializer>>();
                    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                    var welcomeChatInitializer = new WelcomeChatInitializer(dbContext, welcomeChatLogger, configuration);
                    await welcomeChatInitializer.InitializeWelcomeChatAsync();

                    // Automatic tenant migrations: iterate companies and apply migrations to each tenant DB
                    try
                    {
                        var provisioning = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
                        var companies = await provisioning.ListCompaniesAsync();

                        foreach (var company in companies)
                        {
                            try
                            {
                                logger.LogInformation("Applying migrations to tenant DB for company {Company} -> {DbName}", company.Name, company.DbName);

                                var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
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
                catch
                {
                    throw;
                }
            }

            // Configure HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // CORS
            app.UseCors("DefaultPolicy");

            // static files
            app.UseStaticFiles();
            app.UseDefaultFiles();

            // NOW your tenant middleware - run BEFORE routing so path rewrite happens before endpoint matching
            app.UseMiddleware<TenantResolutionMiddleware>();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            // Ensure uploads auth middleware must run after authentication so context.User is populated
            app.UseMiddleware<EnsureUploadsAuthMiddleware>();

            // controllers AFTER rewrite
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<MainHub>("/main");
                endpoints.MapHub<CallHub>("/call");
            });


            app.Run();
        }

        /// <summary>
        /// Показати інструкцію використання
        /// </summary>
        private static void ShowUsage()
        {
            Console.WriteLine("Usage: Edemly.Server <port>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <port>    Port number to listen on (1-65535)");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  Edemly.Server 8100");
            Console.WriteLine("  Edemly.Server 9735");
        }
    }
}
