using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.LLM;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Domain;
using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.Analysis.Persistence
{
    public interface IPersistenceService
    {
        Task<Course?> GetCourseAsync(CourseByIdQuery query, CancellationToken cancellationToken);

        Task<bool> CourseExistsAsync(CourseByIdQuery query, CancellationToken cancellationToken);

        Task<Chapter?> GetChapterAsync(ChapterByCourseQuery query, CancellationToken cancellationToken);

        Task<StructuredProblem?> GetStructuredProblemAsync(StructuredProblemByIdQuery query, CancellationToken cancellationToken);

        Task<PhotoSolutionOcrRecord?> GetPhotoSolutionOcrRecordAsync(PhotoSolutionOcrRecordByIdQuery query, CancellationToken cancellationToken);

        Task<PhotoSolutionOcrRecord> CreatePhotoSolutionOcrRecordAsync(
            CreatePhotoSolutionOcrRecordCommand command,
            CancellationToken cancellationToken);

        Task<PhotoSolutionOcrRecord?> ConfirmPhotoSolutionOcrRecordAsync(
            ConfirmPhotoSolutionOcrRecordCommand command,
            CancellationToken cancellationToken);

        Task<Problem?> GetProblemAsync(ProblemByIdQuery query, CancellationToken cancellationToken);

        Task<PagedQuestionResult> ListQuestionsAsync(
            QuestionListQuery query,
            CancellationToken cancellationToken);

        Task<Question?> GetQuestionAsync(QuestionByIdQuery query, CancellationToken cancellationToken);

        Task<Question> CreateQuestionAsync(CreateQuestionCommand command, CancellationToken cancellationToken);

        Task<Question?> UpdateQuestionAsync(UpdateQuestionCommand command, CancellationToken cancellationToken);

        Task<bool> DeleteQuestionAsync(DeleteQuestionCommand command, CancellationToken cancellationToken);

        Task<List<Question>> GetPublishedQuestionsByKnowledgePointIdsAsync(
            PublishedQuestionsByKnowledgePointsQuery query,
            CancellationToken cancellationToken);

        Task<MathAnalysisAI.Server.Models.AnalysisVisualization> CreateAnalysisVisualizationAsync(
            CreateAnalysisVisualizationCommand command,
            CancellationToken cancellationToken);

        Task<List<UserCourseStats>> GetLeaderboardUserCourseStatsAsync(
            LeaderboardQuery query,
            CancellationToken cancellationToken);

        Task AddMistakeRecordsAsync(
            AddMistakeRecordsCommand command,
            CancellationToken cancellationToken);

        Task<UserCourseStats?> GetUserCourseStatsAsync(
            UserCourseStatsByKeyQuery query,
            CancellationToken cancellationToken);

        Task<UserCourseStats> SaveUserCourseStatsAsync(
            SaveUserCourseStatsCommand command,
            CancellationToken cancellationToken);

        Task<List<UserKnowledgeState>> GetUserKnowledgeStatesAsync(
            UserKnowledgeStatesQuery query,
            CancellationToken cancellationToken);

        Task SaveUserKnowledgeStatesAsync(
            SaveUserKnowledgeStatesCommand command,
            CancellationToken cancellationToken);

        Task<List<UserCourseStats>> GetPersonalUserCourseStatsAsync(
            PersonalUserCourseStatsQuery query,
            CancellationToken cancellationToken);

        Task<List<UserKnowledgeState>> GetPersonalUserKnowledgeStatesAsync(
            PersonalUserKnowledgeStatesQuery query,
            CancellationToken cancellationToken);

        Task<List<MaterialChunk>> GetKnowledgeRetrievalMaterialChunksAsync(
            MaterialChunkRetrievalQuery query,
            CancellationToken cancellationToken);

        Task<List<MaterialChunkKnowledgePoint>> GetMaterialChunkKnowledgePointLinksAsync(
            MaterialChunkKnowledgePointLinksQuery query,
            CancellationToken cancellationToken);

        Task<List<KnowledgePoint>> GetKnowledgePointsByCodesAsync(
            KnowledgePointsByCodesQuery query,
            CancellationToken cancellationToken);

        Task<List<CourseMaterialListItemRecord>> ListCourseMaterialsAsync(
            CourseMaterialsListQuery query,
            CancellationToken cancellationToken);

        Task<List<NetworkResource>> ListNetworkResourcesAsync(
            NetworkResourcesListQuery query,
            CancellationToken cancellationToken);

        Task<NetworkResource?> GetNetworkResourceByIdAsync(
            NetworkResourceByIdQuery query,
            CancellationToken cancellationToken);

        Task<NetworkResource> CreateNetworkResourceAsync(
            CreateNetworkResourceCommand command,
            CancellationToken cancellationToken);

        Task<NetworkResource?> UpdateNetworkResourceAsync(
            UpdateNetworkResourceCommand command,
            CancellationToken cancellationToken);

        Task<bool> DeleteNetworkResourceAsync(
            DeleteNetworkResourceCommand command,
            CancellationToken cancellationToken);

        Task<List<string>> NormalizeKnowledgePointsAsync(
            NormalizeKnowledgePointsQuery query,
            CancellationToken cancellationToken);

        Task<AnalysisPersistenceSession?> InitializeAnalysisSessionAsync(
            UAOInputModel request,
            string analysisMode,
            CancellationToken cancellationToken);

        Task<(Problem Problem, StudentSolution? StudentSolution)> CreateProblemAggregateAsync(
            UAOInputModel request,
            Course course,
            Chapter? chapter,
            CancellationToken cancellationToken);

        Task<AnalysisResult> CreatePendingAnalysisResultAsync(
            Problem problem,
            StudentSolution? studentSolution,
            string analysisMode,
            Course course,
            Chapter? chapter,
            CancellationToken cancellationToken);

        Task<AnalysisResult> SaveLlmFailedAsync(
            AnalysisResult pending,
            UAOInputModel request,
            Course course,
            Chapter? chapter,
            LLMChatResponseDto llmResponse,
            CancellationToken cancellationToken);

        Task<AnalysisResult> SaveParseFailedAsync(
            AnalysisResult pending,
            UAOInputModel request,
            Course course,
            Chapter? chapter,
            LLMChatResponseDto llmResponse,
            string parseError,
            CancellationToken cancellationToken);

        Task<AnalysisResult> SaveSchemaInvalidAsync(
            AnalysisResult pending,
            UAOInputModel request,
            Course course,
            Chapter? chapter,
            LLMChatResponseDto llmResponse,
            AnalysisResultModel parsed,
            string validationError,
            CancellationToken cancellationToken);

        Task<AnalysisResult> SaveSuccessAsync(
            AnalysisResult pending,
            AnalysisResultModel parsed,
            LLMChatResponseDto llmResponse,
            CancellationToken cancellationToken);

        Task<AnalysisResult> SaveVerificationAsync(
            AnalysisResult analysisResult,
            StructuredProblem? structuredProblem,
            PhotoSolutionOcrRecord? ocrRecord,
            AnswerReliability answerReliability,
            bool needsReview,
            string reliabilityReasonsJson,
            string verifierWarningsJson,
            DateTime verifiedAt,
            CancellationToken cancellationToken);

        Task<AnalysisVerificationArtifacts> LoadVerificationArtifactsAsync(
            UAOInputModel request,
            Problem problem,
            CancellationToken cancellationToken);
    }

    public interface IPromptProfileReader
    {
        Task<PromptProfileTemplateContext?> GetActivePromptProfileTemplateContextAsync(
            ActivePromptProfileQuery query,
            CancellationToken cancellationToken);
    }

    public interface IAnalysisPersistenceService : IPersistenceService, IPromptProfileReader
    {
    }

    public sealed class AnalysisPersistenceSession
    {
        public required Course Course { get; init; }
        public Chapter? Chapter { get; init; }
        public required Problem Problem { get; init; }
        public StudentSolution? StudentSolution { get; init; }
        public required AnalysisResult PendingAnalysisResult { get; init; }
    }

    public sealed class AnalysisVerificationArtifacts
    {
        public StructuredProblem? StructuredProblem { get; init; }
        public PhotoSolutionOcrRecord? OcrRecord { get; init; }
    }

    public sealed class PromptProfileTemplateContext
    {
        public required string SystemPrompt { get; init; }
        public required string UserPromptTemplate { get; init; }
    }

    public sealed record CourseByIdQuery(int CourseId);
    public sealed record ActivePromptProfileQuery(int CourseId, string Mode);
    public sealed record ChapterByCourseQuery(int CourseId, int ChapterId);
    public sealed record StructuredProblemByIdQuery(int StructuredProblemId);
    public sealed record PhotoSolutionOcrRecordByIdQuery(int OcrRecordId);
    public sealed record CreatePhotoSolutionOcrRecordCommand(PhotoSolutionOcrRecord Record);
    public sealed record ConfirmPhotoSolutionOcrRecordCommand(
        int OcrRecordId,
        string ConfirmedProblemText,
        string? ConfirmedStudentSolutionText,
        string ConfirmedFormulasJson,
        string Status,
        DateTime ConfirmedAt,
        int ConfirmedByUserId);
    public sealed record ProblemByIdQuery(int ProblemId);
    public sealed record QuestionListQuery(
        int? CourseId,
        int? ChapterId,
        int? KnowledgePointId,
        string? Difficulty,
        string? QuestionType,
        string? Search,
        bool PublishedOnly,
        int Page,
        int PageSize);
    public sealed record QuestionByIdQuery(int QuestionId);
    public sealed record CreateQuestionCommand(Question Question);
    public sealed record UpdateQuestionCommand(
        int QuestionId,
        string Title,
        string Content,
        string? StandardAnswer,
        string? SolutionHint,
        string Difficulty,
        string QuestionType,
        int? CourseId,
        int? ChapterId,
        int? PrimaryKnowledgePointId,
        bool IsPublished,
        DateTime UpdatedAt);
    public sealed record DeleteQuestionCommand(int QuestionId);
    public sealed record PublishedQuestionsByKnowledgePointsQuery(
        IReadOnlyCollection<int> KnowledgePointIds,
        int MaxCount);
    public sealed record CreateAnalysisVisualizationCommand(MathAnalysisAI.Server.Models.AnalysisVisualization Visualization);
    public sealed record LeaderboardQuery(int CourseId, int Limit);
    public sealed record AddMistakeRecordsCommand(IReadOnlyCollection<MistakeRecord> Records);
    public sealed record UserCourseStatsByKeyQuery(int UserId, int CourseId);
    public sealed record SaveUserCourseStatsCommand(UserCourseStats Stats);
    public sealed record UserKnowledgeStatesQuery(int UserId, IReadOnlyCollection<int> KnowledgePointIds);
    public sealed record SaveUserKnowledgeStatesCommand(IReadOnlyCollection<UserKnowledgeState> States);
    public sealed record PersonalUserCourseStatsQuery(int UserId, int? CourseId);
    public sealed record PersonalUserKnowledgeStatesQuery(int UserId, int? CourseId);
    public sealed record MaterialChunkRetrievalQuery(int CourseId, int? ChapterId, int Take);
    public sealed record MaterialChunkKnowledgePointLinksQuery(IReadOnlyCollection<int> MaterialChunkIds);
    public sealed record KnowledgePointsByCodesQuery(int CourseId, IReadOnlyCollection<string> Codes);
    public sealed record CourseMaterialsListQuery(int CourseId, int? ChapterId, string? ParseStatus, int Take);
    public sealed record NetworkResourcesListQuery(int? CourseId, bool EnabledOnly);
    public sealed record NetworkResourceByIdQuery(int ResourceId);
    public sealed record CreateNetworkResourceCommand(NetworkResource Resource);
    public sealed record UpdateNetworkResourceCommand(
        int ResourceId,
        string? Category,
        string? Title,
        string? Description,
        string? Link,
        int? SortOrder,
        bool? IsEnabled,
        DateTime UpdatedAt);
    public sealed record DeleteNetworkResourceCommand(int ResourceId);
    public sealed record NormalizeKnowledgePointsQuery(
        IEnumerable<string>? RawKnowledgePoints,
        int CourseId,
        int? ChapterId,
        string ProblemText,
        string? StudentSolutionText);

    public sealed class PagedQuestionResult
    {
        public required List<Question> Items { get; init; }
        public required int TotalCount { get; init; }
    }

    public sealed class CourseMaterialListItemRecord
    {
        public int MaterialId { get; init; }
        public int CourseId { get; init; }
        public int? ChapterId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string MaterialKind { get; init; } = string.Empty;
        public string OriginalFileName { get; init; } = string.Empty;
        public string FileExtension { get; init; } = string.Empty;
        public long FileSizeBytes { get; init; }
        public string ParseStatus { get; init; } = string.Empty;
        public string? ParseMessage { get; init; }
        public int ChunkCount { get; init; }
        public DateTime UploadedAt { get; init; }
        public DateTime? ParsedAt { get; init; }
    }
}
