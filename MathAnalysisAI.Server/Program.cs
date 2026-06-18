using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using MathAnalysisAI.Server.Configuration;
using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.Data.Seed;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.Admin;
using MathAnalysisAI.Server.Services.Analysis;
using MathAnalysisAI.Server.Services.Analysis.Context;
using MathAnalysisAI.Server.Services.Analysis.Fallback;
using MathAnalysisAI.Server.Services.Analysis.LLM;
using MathAnalysisAI.Server.Services.Analysis.Mistakes;
using MathAnalysisAI.Server.Services.Analysis.Parsing;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.Analysis.Stats;
using MathAnalysisAI.Server.Services.Analysis.Structuring;
using MathAnalysisAI.Server.Services.Analysis.Verification;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.ExceptionHandling;
using MathAnalysisAI.Server.Services.Knowledge;
using MathAnalysisAI.Server.Services.LLM;
using MathAnalysisAI.Server.Services.Materials;
using MathAnalysisAI.Server.Services.OCR;
using MathAnalysisAI.Server.Services.Ranking;
using MathAnalysisAI.Server.Services.Security;
using MathAnalysisAI.Server.Services.Stats;
using MathAnalysisAI.Server.Services.Symbolic;
using MathAnalysisAI.Server.Services.Visualization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

ServerConfigurationLoader.Configure(builder.Configuration, builder.Environment, args);

builder.Host.UseSerilog((context, _, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

var isEfDesignTime = IsEfDesignTime();
var skipRuntimeStartup = isEfDesignTime ||
    string.Equals(
        Environment.GetEnvironmentVariable("MATHANALYSIS_SKIP_RUNTIME_STARTUP"),
        "true",
        StringComparison.OrdinalIgnoreCase);

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
var oidcOptions = builder.Configuration.GetSection(OidcOptions.SectionName).Get<OidcOptions>() ?? new OidcOptions();

ValidateAuthConfiguration(builder.Environment, authOptions, oidcOptions);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection must be configured explicitly. LocalDB fallback is no longer supported.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<OidcOptions>(builder.Configuration.GetSection(OidcOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<LLMOptions>(builder.Configuration.GetSection(LLMOptions.SectionName));
builder.Services.Configure<PhotoSolutionOcrOptions>(builder.Configuration.GetSection(PhotoSolutionOcrOptions.SectionName));
builder.Services.Configure<AnalysisContextOptions>(builder.Configuration.GetSection(AnalysisContextOptions.SectionName));

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100L * 1024L * 1024L;
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 100L * 1024L * 1024L;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(allowedOrigins))
        {
            policy.WithOrigins(allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "text/event-stream",
        "application/json"
    });
});

builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck>("database_readiness", tags: new[] { "ready" });

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("127.0.0.0/8"));

    var dockerNetworks = builder.Configuration["ForwardedHeaders:KnownNetworks"];
    if (!string.IsNullOrWhiteSpace(dockerNetworks))
    {
        foreach (var cidr in dockerNetworks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (System.Net.IPNetwork.TryParse(cidr, out var network))
            {
                options.KnownIPNetworks.Add(network);
            }
        }
    }

    var proxyIps = builder.Configuration["ForwardedHeaders:KnownProxies"];
    if (!string.IsNullOrWhiteSpace(proxyIps))
    {
        foreach (var ip in proxyIps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPAddress.TryParse(ip, out var addr))
            {
                options.KnownProxies.Add(addr);
            }
        }
    }
});

ConfigureAuthentication(builder.Services, authOptions, oidcOptions, builder.Environment);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.AuthenticatedUser, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthPolicies.TeacherOrAdmin, policy =>
        policy.RequireAuthenticatedUser()
            .AddRequirements(new AppRoleRequirement("teacher", "admin")));
    options.AddPolicy(AuthPolicies.AdminOnly, policy =>
        policy.RequireAuthenticatedUser()
            .AddRequirements(new AppRoleRequirement("admin")));
});

builder.Services.AddScoped<IAuthorizationHandler, AppRoleAuthorizationHandler>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        context.HttpContext.Response.Headers["X-Trace-Id"] = context.HttpContext.TraceIdentifier;
        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(new ApiErrorResponse
            {
                ErrorCode = "RATE_LIMITED",
                Message = "请求过于频繁，请稍后重试。",
                TraceId = context.HttpContext.TraceIdentifier,
                IsRetryable = true
            }),
            cancellationToken);
    };

    string GetPartitionKey(HttpContext context)
    {
        var userId = context.User.FindFirst("app_user_id")?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? context.User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(subject))
        {
            return $"subject:{subject}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"login:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 3
            }));

    options.AddPolicy("analyze", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"analyze:{GetPartitionKey(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));

    options.AddPolicy("ocr", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"ocr:{GetPartitionKey(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 2,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 1
            }));

    options.AddPolicy("symbolic", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"symbolic:{GetPartitionKey(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "MathAnalysisAI API",
        Version = "v1",
        Description = "数学分析智能体 — 解题分析、知识点评估与学习路径规划 API"
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IUserContext, CurrentUserService>();
builder.Services.AddScoped<ILocalJwtTokenService, LocalJwtTokenService>();
builder.Services.AddScoped<LLMGateway>();
builder.Services.AddScoped<AnalysisService>();
builder.Services.AddScoped<ILlmResponseParser, LlmResponseParser>();
builder.Services.AddScoped<IAnalysisContextBuilder, AnalysisContextBuilder>();
builder.Services.AddScoped<IAnalysisFallbackService, AnalysisFallbackService>();
builder.Services.AddScoped<ILlmRequestFactory, LlmRequestFactory>();
builder.Services.AddScoped<IAnalysisPersistenceService, AnalysisPersistenceService>();
builder.Services.AddScoped<IProblemStructuringService, ProblemStructuringService>();
builder.Services.AddScoped<IAnalysisVerificationService, AnalysisVerificationService>();
builder.Services.AddScoped<IMistakeRecordService, MistakeRecordService>();
builder.Services.AddScoped<IUserStatsUpdateService, UserStatsUpdateService>();
builder.Services.AddScoped<VisualizationService>();
builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddSingleton<IGeoGebraCommandValidator, GeoGebraCommandValidator>();
builder.Services.AddScoped<CourseMaterialStorageService>();
builder.Services.AddScoped<PdfTextExtractionService>();
builder.Services.AddScoped<MaterialChunkingService>();
builder.Services.AddScoped<CourseMaterialIngestionService>();
builder.Services.AddScoped<IPhotoSolutionOcrProvider, LiteLLMPhotoSolutionOcrProvider>();
builder.Services.AddScoped<IKnowledgeRetrievalService, KnowledgeRetrievalService>();
builder.Services.AddScoped<LearningPathService>();
builder.Services.AddScoped<QuestionService>();
builder.Services.AddScoped<PersonalStatsService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<ISymbolicMathService, SymPySymbolicMathService>();

var app = builder.Build();

if (!skipRuntimeStartup)
{
    await InitializeDatabaseAsync(app, authOptions);
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.PhysicalPath;
        if (!string.IsNullOrWhiteSpace(path)
            && path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            ctx.Context.Response.Headers.Pragma = "no-cache";
            ctx.Context.Response.Headers.Expires = "0";
        }
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseResponseCompression();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapGet("/health", () => Results.Json(new
{
    status = "ok",
    service = "MathAnalysisAI.Server",
    timestampUtc = DateTime.UtcNow
})).DisableRateLimiting();

app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            traceId = context.TraceIdentifier,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            }),
            duration = report.TotalDuration.ToString()
        });
        await context.Response.WriteAsync(payload);
    }
}).DisableRateLimiting();

if (!skipRuntimeStartup)
{
    app.Run();
}

static void ConfigureAuthentication(
    IServiceCollection services,
    AuthOptions authOptions,
    OidcOptions oidcOptions,
    IWebHostEnvironment environment)
{
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;

            if (authOptions.IsOidcMode())
            {
                options.Authority = oidcOptions.Authority;
                options.RequireHttpsMetadata = oidcOptions.RequireHttpsMetadata;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = !string.IsNullOrWhiteSpace(oidcOptions.Audience),
                    ValidAudience = oidcOptions.Audience,
                    NameClaimType = string.IsNullOrWhiteSpace(oidcOptions.NameClaimType)
                        ? "preferred_username"
                        : oidcOptions.NameClaimType,
                    RoleClaimType = string.IsNullOrWhiteSpace(oidcOptions.RoleClaimType)
                        ? "role"
                        : oidcOptions.RoleClaimType
                };
                return;
            }

            var signingKey = ResolveValidationSigningKey(authOptions, environment);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = string.IsNullOrWhiteSpace(authOptions.TokenIssuer)
                    ? "MathAnalysisAI.Server"
                    : authOptions.TokenIssuer.Trim(),
                ValidAudience = string.IsNullOrWhiteSpace(authOptions.TokenAudience)
                    ? "MathAnalysisAI.Client"
                    : authOptions.TokenAudience.Trim(),
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = "app_role"
            };
        });
}

static string ResolveValidationSigningKey(AuthOptions authOptions, IWebHostEnvironment environment)
{
    if (!string.IsNullOrWhiteSpace(authOptions.TokenSigningKey))
    {
        return authOptions.TokenSigningKey.Trim();
    }

    if (authOptions.IsDisabledMode())
    {
        return "disabled-mode-signing-key-not-used-in-production-2026";
    }

    if (environment.IsDevelopment())
    {
        return "development-only-signing-key-change-me-please-2026";
    }

    throw new InvalidOperationException(
        "Auth:TokenSigningKey must be configured when LocalPassword or DevelopmentUsername mode is enabled.");
}

static async Task InitializeDatabaseAsync(WebApplication app, AuthOptions authOptions)
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    logger.LogInformation("Applying pending EF Core migrations...");
    await db.Database.MigrateAsync();
    logger.LogInformation("EF Core migrations applied successfully.");

    try
    {
        var inserted = await PromptProfileSeeder.SeedAsync(db);
        logger.LogInformation("PromptProfile seeding completed. Inserted: {InsertedCount}", inserted);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "PromptProfile seeding failed after migrations. Continuing startup.");
    }

    try
    {
        var testUserId = await AppUserSeeder.SeedDevelopmentTestStudentAsync(db);
        logger.LogInformation("Development test user seeding completed. UserId: {UserId}", testUserId);

        var adminUsername = app.Configuration["Admin:Username"];
        var adminPassword = app.Configuration["Admin:Password"];
        if (!string.IsNullOrWhiteSpace(adminUsername) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var adminUserId = await AdminUserSeeder.SeedAdminAsync(db, adminUsername, adminPassword, authOptions.BcryptWorkFactor);
            logger.LogInformation(
                "Admin user seeding completed. UserId: {UserId}, Username: {Username}",
                adminUserId,
                adminUsername);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Optional user seeding failed after migrations. Continuing startup.");
    }
}

static void ValidateAuthConfiguration(
    IHostEnvironment environment,
    AuthOptions authOptions,
    OidcOptions oidcOptions)
{
    var mode = authOptions.GetNormalizedMode();
    var validModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AuthOptions.ModeDevelopmentUsername,
        AuthOptions.ModeLocalPassword,
        AuthOptions.ModeOidc,
        AuthOptions.ModeDisabled
    };

    if (string.IsNullOrWhiteSpace(mode))
    {
        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Unsafe auth configuration for Production: Auth:Mode must be explicitly set.");
        }

        return;
    }

    if (!validModes.Contains(mode))
    {
        throw new InvalidOperationException(
            $"Invalid auth configuration: Auth:Mode '{mode}' is not supported.");
    }

    if (authOptions.IsOidcMode())
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(oidcOptions.Authority)) missing.Add("Oidc:Authority");
        if (string.IsNullOrWhiteSpace(oidcOptions.ClientId)) missing.Add("Oidc:ClientId");
        if (string.IsNullOrWhiteSpace(oidcOptions.Audience)) missing.Add("Oidc:Audience");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"OIDC authentication is enabled but required settings are missing: {string.Join(", ", missing)}.");
        }
    }

    if ((authOptions.IsLocalPasswordMode() || authOptions.IsDevelopmentUsernameMode())
        && environment.IsProduction()
        && string.IsNullOrWhiteSpace(authOptions.TokenSigningKey))
    {
        throw new InvalidOperationException(
            "Auth:TokenSigningKey must be configured for Production when using LocalPassword or DevelopmentUsername mode.");
    }

    if (!environment.IsProduction())
    {
        return;
    }

    var violations = new List<string>();
    if (string.Equals(mode, AuthOptions.ModeDevelopmentUsername, StringComparison.OrdinalIgnoreCase))
    {
        violations.Add("Auth:Mode=DevelopmentUsername");
    }

    if (authOptions.EnableDevelopmentFallback)
    {
        violations.Add("Auth:EnableDevelopmentFallback=true");
    }

    if (authOptions.EnableDevelopmentMaterialAccessOverride)
    {
        violations.Add("Auth:EnableDevelopmentMaterialAccessOverride=true");
    }

    if (authOptions.EnableDevelopmentSymbolicAccessOverride)
    {
        violations.Add("Auth:EnableDevelopmentSymbolicAccessOverride=true");
    }

    if (violations.Count > 0)
    {
        throw new InvalidOperationException(
            $"Unsafe auth configuration for Production: {string.Join(", ", violations)}.");
    }
}

static bool IsEfDesignTime()
{
    return AppDomain.CurrentDomain.GetAssemblies()
        .Any(assembly => string.Equals(
            assembly.GetName().Name,
            "Microsoft.EntityFrameworkCore.Design",
            StringComparison.Ordinal));
}
