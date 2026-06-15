using System.Net.Http.Headers;
using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.DTOs.LLM;
using MathAnalysisAI.Server.DTOs.PhotoSolutions;
using MathAnalysisAI.Server.DTOs.Visualization;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Controllers;
using MathAnalysisAI.Server.Services.Analysis.Context;
using MathAnalysisAI.Server.Services.Analysis.Fallback;
using MathAnalysisAI.Server.Services.Analysis.LLM;
using MathAnalysisAI.Server.Services.Analysis.Mistakes;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.Analysis.Stats;
using MathAnalysisAI.Server.Services.Analysis.Structuring;
using MathAnalysisAI.Server.Services.Analysis.Verification;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.LLM;
using MathAnalysisAI.Server.Services.OCR;
using MathAnalysisAI.Server.Services.Visualization;
using MathAnalysisAI.Server.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace MathAnalysisAI.Server.Tests;

internal static class TestDb
{
    public static ApplicationDbContext Create(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}

internal sealed class FakeUserContext : IUserContext
{
    public AppUser? CurrentUser { get; set; }

    public Task<AppUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentUser);
    public Task<int?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentUser?.Id);
    public Task<string?> GetCurrentRoleAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentUser?.Role);
    public Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default) => Task.FromResult(CurrentUser != null && string.Equals(CurrentUser.Role, role, StringComparison.OrdinalIgnoreCase));
    public Task<bool> IsInAnyRoleAsync(IEnumerable<string> roles, CancellationToken cancellationToken = default) => Task.FromResult(CurrentUser != null && roles.Any(role => string.Equals(CurrentUser.Role, role, StringComparison.OrdinalIgnoreCase)));
    public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentUser != null);
    public string? GetImpersonatedRole() => null;
    public void SetImpersonatedRole(string? role) { }
}

internal sealed class FakeSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

    public bool IsAvailable => true;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value.ToArray();
    public bool TryGetValue(string key, out byte[] value)
    {
        if (_store.TryGetValue(key, out var bytes))
        {
            value = bytes.ToArray();
            return true;
        }

        value = Array.Empty<byte>();
        return false;
    }
}

internal sealed class FakeSessionFeature : ISessionFeature
{
    public ISession Session { get; set; } = new FakeSession();
}

internal sealed class FakeWebHostEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "MathAnalysisAI.Server.Tests";
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class FakePhotoSolutionOcrProvider : IPhotoSolutionOcrProvider
{
    public PhotoSolutionOcrResponseDto Response { get; set; } = new();

    public Task<PhotoSolutionOcrResponseDto> RecognizeAsync(PhotoSolutionOcrRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Clone(Response));
    }

    private static PhotoSolutionOcrResponseDto Clone(PhotoSolutionOcrResponseDto source)
    {
        return new PhotoSolutionOcrResponseDto
        {
            OcrRecordId = source.OcrRecordId,
            ProblemText = source.ProblemText,
            StudentSolutionText = source.StudentSolutionText,
            DetectedSections = source.DetectedSections.Select(x => new DetectedSectionDto
            {
                Type = x.Type,
                Content = x.Content
            }).ToList(),
            Formulas = source.Formulas.Select(x => new FormulaCandidateDto
            {
                Latex = x.Latex,
                Context = x.Context
            }).ToList(),
            Warnings = source.Warnings.ToList(),
            ReviewReasons = source.ReviewReasons.ToList(),
            Confidence = source.Confidence,
            Status = source.Status,
            NeedsManualReview = source.NeedsManualReview,
            IsConfirmed = source.IsConfirmed,
            CanAnalyze = source.CanAnalyze,
            IsSuccess = source.IsSuccess,
            ErrorCode = source.ErrorCode,
            IsRetryable = source.IsRetryable,
            StatusCode = source.StatusCode,
            AttemptCount = source.AttemptCount,
            ErrorMessage = source.ErrorMessage,
            RawProvider = source.RawProvider,
            ModelName = source.ModelName
        };
    }
}

internal sealed class FakeAnalysisContextBuilder : IAnalysisContextBuilder
{
    public Task<AnalysisContextDto> BuildAsync(AnalysisContextBuildRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(AnalysisContextDto.Empty);
}

internal sealed class FakeLlmRequestFactory : ILlmRequestFactory
{
    public Task<LLMChatRequestDto> BuildAsync(
        AnalysisRequestDto request,
        Course course,
        Chapter? chapter,
        Problem problem,
        StudentSolution? studentSolution,
        AnalysisContextDto? context,
        int analysisResultId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new LLMChatRequestDto
        {
            UserId = request.UserId,
            AnalysisResultId = analysisResultId,
            RequestType = "analysis",
            Messages =
            {
                new LLMChatMessageDto
                {
                    Role = "user",
                    Content = request.ProblemText
                }
            }
        });
    }
}

internal sealed class FakeMistakeRecordService : IMistakeRecordService
{
    public Task<List<int>> SaveMistakeRecordsAsync(
        int analysisResultId,
        int courseId,
        IReadOnlyList<string> normalizedKnowledgePointCodes,
        IReadOnlyList<string> mistakeTags,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new List<int>());
    }
}

internal sealed class FakeUserStatsUpdateService : IUserStatsUpdateService
{
    public bool? ResolveCorrectness(AnalysisResult? analysisResult) => null;

    public Task UpdateAfterAnalysisAsync(
        int? requestUserId,
        int? studentSolutionUserId,
        int courseId,
        IReadOnlyList<string> normalizedKnowledgePointCodes,
        IReadOnlyList<int> mistakeKnowledgePointIds,
        AnalysisResult analysisResult,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task UpdateCourseStatsAsync(int? userId, int courseId, bool? isCorrect, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

internal sealed class FakeFallbackService : IAnalysisFallbackService
{
    public void ApplyFallbacks(
        AnalysisResponseDto parsed,
        string analysisMode,
        string problemText,
        string? studentSolutionText,
        string? rawLlmContent,
        string? chapterName)
    {
    }
}

internal sealed class FakeGeoGebraCommandValidator : IGeoGebraCommandValidator
{
    public GeoGebraValidationResultDto Validate(IEnumerable<string>? commands)
    {
        return new GeoGebraValidationResultDto
        {
            IsValid = true,
            ValidCommands = commands?.ToList() ?? new List<string>()
        };
    }
}

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new DummyHandler());

    private sealed class DummyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"{\"ok\":true}"}}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""")
            });
        }
    }
}

internal static class TestServiceFactory
{
    public static AnalysisService CreateAnalysisService(
        ApplicationDbContext db,
        MathAnalysisAI.Server.Services.Analysis.Parsing.ILlmResponseParser? parser = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DeepSeek:BaseUrl"] = "http://127.0.0.1/fake",
            ["DeepSeek:ApiKey"] = "test-key"
        }).Build();
        var llmGateway = new LLMGateway(
            new FakeHttpClientFactory(),
            config,
            db,
            Microsoft.Extensions.Options.Options.Create(new LLMOptions()));
        var visualizationService = new VisualizationService(db, new FakeGeoGebraCommandValidator());

        return new AnalysisService(
            db,
            llmGateway,
            visualizationService,
            parser ?? new FakeLlmResponseParser(),
            new FakeFallbackService(),
            new AnalysisPersistenceService(db),
            new FakeMistakeRecordService(),
            new FakeUserStatsUpdateService(),
            new AnalysisVerificationService(db),
            new FakeAnalysisContextBuilder(),
            new FakeLlmRequestFactory(),
            NullLogger<AnalysisService>.Instance);
    }

    public static PhotoSolutionsController CreatePhotoSolutionsController(
        ApplicationDbContext db,
        IPhotoSolutionOcrProvider provider)
    {
        return CreatePhotoSolutionsController(db, provider, null);
    }

    public static PhotoSolutionsController CreatePhotoSolutionsController(
        ApplicationDbContext db,
        IPhotoSolutionOcrProvider provider,
        AppUser? user)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DeepSeek:BaseUrl"] = "http://127.0.0.1/fake",
            ["DeepSeek:ApiKey"] = "test-key"
        }).Build();
        var controller = new PhotoSolutionsController(
            db,
            provider,
            config,
            NullLogger<PhotoSolutionsController>.Instance);
        SetupControllerContext(controller, user);
        return controller;
    }

    public static void SetupControllerContext(ControllerBase controller, AppUser? user)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUserContext>(new FakeUserContext { CurrentUser = user });
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };
        httpContext.Features.Set<ISessionFeature>(new FakeSessionFeature());

        if (user != null)
        {
            httpContext.Items["CurrentUser"] = user;
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public static AuthController CreateAuthController(
        ApplicationDbContext db,
        IUserContext userContext,
        string environmentName = "Development",
        Dictionary<string, string?>? authConfig = null)
    {
        var options = new AuthOptions();
        if (authConfig != null)
        {
            if (authConfig.TryGetValue("Auth:Mode", out var mode))
            {
                options.Mode = mode;
            }

            if (authConfig.TryGetValue("Auth:EnableDevelopmentFallback", out var enableFallback))
            {
                options.EnableDevelopmentFallback = bool.TryParse(enableFallback, out var parsed) && parsed;
            }

            if (authConfig.TryGetValue("Auth:DevelopmentFallbackUser", out var fallbackUser))
            {
                options.DevelopmentFallbackUser = fallbackUser;
            }
        }

        return new AuthController(
            db,
            new FakeWebHostEnvironment { EnvironmentName = environmentName },
            Microsoft.Extensions.Options.Options.Create(options),
            userContext);
    }

    public static LearningAnalysisController CreateLearningAnalysisController(
        ApplicationDbContext db,
        MathAnalysisAI.Server.Services.Analysis.Parsing.ILlmResponseParser? parser = null)
    {
        return CreateLearningAnalysisController(db, null, parser);
    }

    public static LearningAnalysisController CreateLearningAnalysisController(
        ApplicationDbContext db,
        AppUser? user,
        MathAnalysisAI.Server.Services.Analysis.Parsing.ILlmResponseParser? parser = null)
    {
        var controller = new LearningAnalysisController(
            CreateAnalysisService(db, parser),
            new ProblemStructuringService(db),
            db,
            NullLogger<LearningAnalysisController>.Instance);
        SetupControllerContext(controller, user);
        return controller;
    }

    public static IFormFile CreateFormFile(string fileName = "test.png", string contentType = "image/png", byte[]? bytes = null)
    {
        var content = bytes ?? [1, 2, 3, 4];
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    public static AppUser SeedUser(ApplicationDbContext db, int id, string username, string? passwordHash = null)
    {
        var user = new AppUser
        {
            Id = id,
            Username = username,
            PasswordHash = passwordHash,
            Role = AppUserRole.Student,
            CreatedAt = DateTime.UtcNow
        };
        db.AppUsers.Add(user);
        db.SaveChanges();
        return user;
    }

    public static Course SeedCourse(ApplicationDbContext db, int id = 1001)
    {
        var subject = new Subject
        {
            Id = id + 1000,
            Name = "Math"
        };

        var course = new Course
        {
            Id = id,
            SubjectId = subject.Id,
            Name = "Math Analysis",
            Code = $"MA-{id}",
            Description = "Test course"
        };

        db.Subjects.Add(subject);
        db.Courses.Add(course);
        db.SaveChanges();
        return course;
    }

    public static PhotoSolutionOcrRecord SeedOcrRecord(
        ApplicationDbContext db,
        AppUser owner,
        Course course,
        string status,
        string recognizedProblemText = "题目文本",
        string recognizedStudentSolutionText = "解答文本",
        string? confirmedProblemText = null,
        string? confirmedStudentSolutionText = null,
        string? confirmedFormulasJson = null)
    {
        var record = new PhotoSolutionOcrRecord
        {
            UserId = owner.Id,
            CourseId = course.Id,
            ChapterId = null,
            OriginalFileName = "sample.png",
            ContentType = "image/png",
            FileSizeBytes = 4,
            ImageHash = Guid.NewGuid().ToString("N"),
            UploadedAt = DateTime.UtcNow,
            OcrProvider = "fake",
            OcrModelName = "test-model",
            RecognizedProblemText = recognizedProblemText,
            RecognizedStudentSolutionText = recognizedStudentSolutionText,
            DetectedSectionsJson = "[]",
            FormulasJson = "[]",
            WarningsJson = "[]",
            ReviewReasonsJson = "[]",
            Confidence = 0.95m,
            Status = status,
            ConfirmedProblemText = confirmedProblemText,
            ConfirmedStudentSolutionText = confirmedStudentSolutionText,
            ConfirmedFormulasJson = confirmedFormulasJson
        };

        db.PhotoSolutionOcrRecords.Add(record);
        db.SaveChanges();
        return record;
    }
}

internal sealed class FakeLlmResponseParser : MathAnalysisAI.Server.Services.Analysis.Parsing.ILlmResponseParser
{
    public MathAnalysisAI.Server.Services.Analysis.Parsing.LlmParseResult Parse(string? rawContent)
    {
        return new MathAnalysisAI.Server.Services.Analysis.Parsing.LlmParseResult
        {
            Success = false,
            ErrorMessage = "parser not used in tests"
        };
    }
}

internal sealed class FakeSuccessfulLlmResponseParser : MathAnalysisAI.Server.Services.Analysis.Parsing.ILlmResponseParser
{
    public bool IncludeStandardSolution { get; set; } = true;
    public bool IsCorrect { get; set; } = true;

    public MathAnalysisAI.Server.Services.Analysis.Parsing.LlmParseResult Parse(string? rawContent)
    {
        var standardSolution = IncludeStandardSolution
            ? new List<StandardSolutionStepDto>
            {
                new()
                {
                    Title = "步骤1",
                    Content = "检查定义域并代入。"
                }
            }
            : new List<StandardSolutionStepDto>();

        return new MathAnalysisAI.Server.Services.Analysis.Parsing.LlmParseResult
        {
            Success = true,
            Parsed = new AnalysisResponseDto
            {
                Course = "Math Analysis",
                Chapter = "Limits",
                ProblemType = "limit",
                Difficulty = "easy",
                KnowledgePoints = new List<string> { "极限" },
                SolutionOverview = "先判断定义域，再直接代入。",
                StandardSolution = standardSolution,
                StudentSolutionReview = new StudentSolutionReviewDto
                {
                    IsCorrect = IsCorrect,
                    MainIssue = "无",
                    LogicGaps = new List<string>(),
                    Suggestions = new List<string>()
                },
                MistakeTags = new List<string>(),
                ReviewSuggestions = new List<string>(),
                Visualization = new VisualizationDto
                {
                    ShouldUse = false,
                    Engine = "none",
                    VisualizationType = "none",
                    GeoGebraCommands = new List<string>()
                }
            },
            NormalizedJson = rawContent
        };
    }
}
