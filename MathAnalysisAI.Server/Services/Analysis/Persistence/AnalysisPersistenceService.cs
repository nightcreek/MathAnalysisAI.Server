using System.Text.Json;
using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.LLM;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.SharedKernel.Analysis;
using MathAnalysisAI.Server.Services.Analysis.Domain;
using MathAnalysisAI.Server.Services.Analysis.UAO;
using MathAnalysisAI.Server.Services.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Services.Analysis.Persistence
{
    public sealed class AnalysisPersistenceService : IAnalysisPersistenceService, IPromptProfileReader
    {
        private readonly ApplicationDbContext _db;

        public AnalysisPersistenceService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<Course?> GetCourseAsync(CourseByIdQuery query, CancellationToken cancellationToken)
        {
            return await _db.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == query.CourseId, cancellationToken);
        }

        public async Task<PromptProfileTemplateContext?> GetActivePromptProfileTemplateContextAsync(
            ActivePromptProfileQuery query,
            CancellationToken cancellationToken)
        {
            return await _db.PromptProfiles
                .AsNoTracking()
                .Where(x => x.CourseId == query.CourseId && x.Mode == query.Mode && x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Version)
                .Select(x => new PromptProfileTemplateContext
                {
                    SystemPrompt = x.SystemPrompt,
                    UserPromptTemplate = x.UserPromptTemplate
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> CourseExistsAsync(CourseByIdQuery query, CancellationToken cancellationToken)
        {
            return await _db.Courses
                .AsNoTracking()
                .AnyAsync(x => x.Id == query.CourseId, cancellationToken);
        }

        public async Task<Chapter?> GetChapterAsync(ChapterByCourseQuery query, CancellationToken cancellationToken)
        {
            return await _db.Chapters
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == query.ChapterId && x.CourseId == query.CourseId, cancellationToken);
        }

        public async Task<StructuredProblem?> GetStructuredProblemAsync(StructuredProblemByIdQuery query, CancellationToken cancellationToken)
        {
            return await _db.StructuredProblems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == query.StructuredProblemId, cancellationToken);
        }

        public async Task<PhotoSolutionOcrRecord?> GetPhotoSolutionOcrRecordAsync(PhotoSolutionOcrRecordByIdQuery query, CancellationToken cancellationToken)
        {
            return await _db.PhotoSolutionOcrRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == query.OcrRecordId, cancellationToken);
        }

        public async Task<PhotoSolutionOcrRecord> CreatePhotoSolutionOcrRecordAsync(
            CreatePhotoSolutionOcrRecordCommand command,
            CancellationToken cancellationToken)
        {
            _db.PhotoSolutionOcrRecords.Add(command.Record);
            await _db.SaveChangesAsync(cancellationToken);
            return command.Record;
        }

        public async Task<PhotoSolutionOcrRecord?> ConfirmPhotoSolutionOcrRecordAsync(
            ConfirmPhotoSolutionOcrRecordCommand command,
            CancellationToken cancellationToken)
        {
            var record = await _db.PhotoSolutionOcrRecords
                .FirstOrDefaultAsync(x => x.Id == command.OcrRecordId, cancellationToken);

            if (record == null)
            {
                return null;
            }

            record.ConfirmedProblemText = command.ConfirmedProblemText;
            record.ConfirmedStudentSolutionText = command.ConfirmedStudentSolutionText;
            record.ConfirmedFormulasJson = command.ConfirmedFormulasJson;
            record.Status = command.Status;
            record.ConfirmedAt = command.ConfirmedAt;
            record.ConfirmedByUserId = command.ConfirmedByUserId;

            await _db.SaveChangesAsync(cancellationToken);
            return record;
        }

        public async Task<Problem?> GetProblemAsync(ProblemByIdQuery query, CancellationToken cancellationToken)
        {
            return await _db.Problems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == query.ProblemId, cancellationToken);
        }

        public async Task<PagedQuestionResult> ListQuestionsAsync(
            QuestionListQuery query,
            CancellationToken cancellationToken)
        {
            var questionQuery = _db.Questions
                .AsNoTracking()
                .Include(q => q.Chapter)
                .Include(q => q.KnowledgePoint)
                .AsQueryable();

            if (query.CourseId.HasValue)
            {
                questionQuery = questionQuery.Where(q => q.CourseId == query.CourseId.Value);
            }

            if (query.ChapterId.HasValue)
            {
                questionQuery = questionQuery.Where(q => q.ChapterId == query.ChapterId.Value);
            }

            if (query.KnowledgePointId.HasValue)
            {
                questionQuery = questionQuery.Where(q => q.PrimaryKnowledgePointId == query.KnowledgePointId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Difficulty))
            {
                questionQuery = questionQuery.Where(q => q.Difficulty == query.Difficulty);
            }

            if (!string.IsNullOrWhiteSpace(query.QuestionType))
            {
                questionQuery = questionQuery.Where(q => q.QuestionType == query.QuestionType);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                questionQuery = questionQuery.Where(q => q.Title.Contains(query.Search) || q.Content.Contains(query.Search));
            }

            if (query.PublishedOnly)
            {
                questionQuery = questionQuery.Where(q => q.IsPublished);
            }

            var total = await questionQuery.CountAsync(cancellationToken);
            var items = await questionQuery
                .OrderByDescending(q => q.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedQuestionResult
            {
                Items = items,
                TotalCount = total
            };
        }

        public async Task<Question?> GetQuestionAsync(QuestionByIdQuery query, CancellationToken cancellationToken)
        {
            return await _db.Questions
                .AsNoTracking()
                .Include(q => q.Chapter)
                .Include(q => q.KnowledgePoint)
                .FirstOrDefaultAsync(q => q.Id == query.QuestionId, cancellationToken);
        }

        public async Task<Question> CreateQuestionAsync(CreateQuestionCommand command, CancellationToken cancellationToken)
        {
            _db.Questions.Add(command.Question);
            await _db.SaveChangesAsync(cancellationToken);
            return command.Question;
        }

        public async Task<Question?> UpdateQuestionAsync(UpdateQuestionCommand command, CancellationToken cancellationToken)
        {
            var question = await _db.Questions
                .FirstOrDefaultAsync(q => q.Id == command.QuestionId, cancellationToken);

            if (question == null)
            {
                return null;
            }

            question.Title = command.Title;
            question.Content = command.Content;
            question.StandardAnswer = command.StandardAnswer;
            question.SolutionHint = command.SolutionHint;
            question.Difficulty = command.Difficulty;
            question.QuestionType = command.QuestionType;
            question.CourseId = command.CourseId;
            question.ChapterId = command.ChapterId;
            question.PrimaryKnowledgePointId = command.PrimaryKnowledgePointId;
            question.IsPublished = command.IsPublished;
            question.UpdatedAt = command.UpdatedAt;

            await _db.SaveChangesAsync(cancellationToken);
            return question;
        }

        public async Task<bool> DeleteQuestionAsync(DeleteQuestionCommand command, CancellationToken cancellationToken)
        {
            var question = await _db.Questions
                .FirstOrDefaultAsync(q => q.Id == command.QuestionId, cancellationToken);

            if (question == null)
            {
                return false;
            }

            _db.Questions.Remove(question);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<List<Question>> GetPublishedQuestionsByKnowledgePointIdsAsync(
            PublishedQuestionsByKnowledgePointsQuery query,
            CancellationToken cancellationToken)
        {
            if (query.KnowledgePointIds.Count == 0)
            {
                return new List<Question>();
            }

            return await _db.Questions
                .AsNoTracking()
                .Include(q => q.Chapter)
                .Include(q => q.KnowledgePoint)
                .Where(q => q.PrimaryKnowledgePointId != null && query.KnowledgePointIds.Contains(q.PrimaryKnowledgePointId.Value))
                .Where(q => q.IsPublished)
                .OrderBy(q => q.Difficulty == "easy" ? 0 : q.Difficulty == "medium" ? 1 : 2)
                .Take(query.MaxCount)
                .ToListAsync(cancellationToken);
        }

        public async Task<MathAnalysisAI.Server.Models.AnalysisVisualization> CreateAnalysisVisualizationAsync(
            CreateAnalysisVisualizationCommand command,
            CancellationToken cancellationToken)
        {
            _db.AnalysisVisualizations.Add(command.Visualization);
            await _db.SaveChangesAsync(cancellationToken);
            return command.Visualization;
        }

        public async Task<List<UserCourseStats>> GetLeaderboardUserCourseStatsAsync(
            LeaderboardQuery query,
            CancellationToken cancellationToken)
        {
            return await _db.UserCourseStats
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Course)
                .Where(x => x.CourseId == query.CourseId)
                .OrderByDescending(x => x.RankingScore)
                .ThenByDescending(x => x.AccuracyRate)
                .ThenByDescending(x => x.AttemptCount)
                .Take(query.Limit)
                .ToListAsync(cancellationToken);
        }

        public async Task AddMistakeRecordsAsync(
            AddMistakeRecordsCommand command,
            CancellationToken cancellationToken)
        {
            if (command.Records.Count == 0)
            {
                return;
            }

            _db.MistakeRecords.AddRange(command.Records);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<UserCourseStats?> GetUserCourseStatsAsync(
            UserCourseStatsByKeyQuery query,
            CancellationToken cancellationToken)
        {
            return await _db.UserCourseStats
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == query.UserId && x.CourseId == query.CourseId, cancellationToken);
        }

        public async Task<UserCourseStats> SaveUserCourseStatsAsync(
            SaveUserCourseStatsCommand command,
            CancellationToken cancellationToken)
        {
            if (command.Stats.Id > 0)
            {
                _db.UserCourseStats.Update(command.Stats);
            }
            else
            {
                _db.UserCourseStats.Add(command.Stats);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return command.Stats;
        }

        public async Task<List<UserKnowledgeState>> GetUserKnowledgeStatesAsync(
            UserKnowledgeStatesQuery query,
            CancellationToken cancellationToken)
        {
            if (query.KnowledgePointIds.Count == 0)
            {
                return new List<UserKnowledgeState>();
            }

            return await _db.UserKnowledgeStates
                .AsNoTracking()
                .Where(x => x.UserId == query.UserId && query.KnowledgePointIds.Contains(x.KnowledgePointId))
                .ToListAsync(cancellationToken);
        }

        public async Task SaveUserKnowledgeStatesAsync(
            SaveUserKnowledgeStatesCommand command,
            CancellationToken cancellationToken)
        {
            foreach (var state in command.States)
            {
                if (state.Id > 0)
                {
                    _db.UserKnowledgeStates.Update(state);
                }
                else
                {
                    _db.UserKnowledgeStates.Add(state);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<UserCourseStats>> GetPersonalUserCourseStatsAsync(
            PersonalUserCourseStatsQuery query,
            CancellationToken cancellationToken)
        {
            var statsQuery = _db.UserCourseStats
                .AsNoTracking()
                .Include(x => x.Course)
                .Where(x => x.UserId == query.UserId);

            if (query.CourseId.HasValue)
            {
                statsQuery = statsQuery.Where(x => x.CourseId == query.CourseId.Value);
            }

            return await statsQuery
                .OrderByDescending(x => x.AttemptCount)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<UserKnowledgeState>> GetPersonalUserKnowledgeStatesAsync(
            PersonalUserKnowledgeStatesQuery query,
            CancellationToken cancellationToken)
        {
            var statesQuery = _db.UserKnowledgeStates
                .AsNoTracking()
                .Include(x => x.KnowledgePoint)
                .Where(x => x.UserId == query.UserId);

            if (query.CourseId.HasValue)
            {
                statesQuery = statesQuery.Where(x => x.KnowledgePoint != null && x.KnowledgePoint.CourseId == query.CourseId.Value);
            }

            return await statesQuery
                .OrderByDescending(x => x.MasteryLevel)
                .ThenByDescending(x => x.PracticeCount)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<MaterialChunk>> GetKnowledgeRetrievalMaterialChunksAsync(
            MaterialChunkRetrievalQuery query,
            CancellationToken cancellationToken)
        {
            var materialQuery = _db.MaterialChunks
                .AsNoTracking()
                .Include(x => x.CourseMaterial)
                .Where(x => x.CourseId == query.CourseId)
                .Where(x => !string.IsNullOrWhiteSpace(x.ContentPreview))
                .Where(x => x.CourseMaterial != null && x.CourseMaterial.ParseStatus == "success");

            if (query.ChapterId.HasValue)
            {
                materialQuery = materialQuery.Where(x => x.ChapterId == query.ChapterId.Value);
            }

            return await materialQuery
                .OrderByDescending(x => x.CourseMaterial!.UploadedAt)
                .Take(query.Take)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<MaterialChunkKnowledgePoint>> GetMaterialChunkKnowledgePointLinksAsync(
            MaterialChunkKnowledgePointLinksQuery query,
            CancellationToken cancellationToken)
        {
            if (query.MaterialChunkIds.Count == 0)
            {
                return new List<MaterialChunkKnowledgePoint>();
            }

            return await _db.MaterialChunkKnowledgePoints
                .AsNoTracking()
                .Include(x => x.KnowledgePoint)
                .Where(x => query.MaterialChunkIds.Contains(x.MaterialChunkId))
                .ToListAsync(cancellationToken);
        }

        public async Task<List<KnowledgePoint>> GetKnowledgePointsByCodesAsync(
            KnowledgePointsByCodesQuery query,
            CancellationToken cancellationToken)
        {
            if (query.Codes.Count == 0)
            {
                return new List<KnowledgePoint>();
            }

            return await _db.KnowledgePoints
                .AsNoTracking()
                .Where(x => x.CourseId == query.CourseId && x.Code != null && query.Codes.Contains(x.Code))
                .ToListAsync(cancellationToken);
        }

        public async Task<List<CourseMaterialListItemRecord>> ListCourseMaterialsAsync(
            CourseMaterialsListQuery query,
            CancellationToken cancellationToken)
        {
            var materialQuery = _db.CourseMaterials
                .AsNoTracking()
                .Where(x => x.CourseId == query.CourseId);

            if (query.ChapterId.HasValue)
            {
                var selectedChapterId = query.ChapterId.Value;
                materialQuery = materialQuery.Where(
                    x => _db.MaterialChunks.Any(c => c.CourseMaterialId == x.Id && c.ChapterId == selectedChapterId));
            }

            if (!string.IsNullOrWhiteSpace(query.ParseStatus))
            {
                var normalizedStatus = query.ParseStatus.Trim().ToLowerInvariant();
                materialQuery = materialQuery.Where(x => x.ParseStatus.ToLower() == normalizedStatus);
            }

            return await materialQuery
                .OrderByDescending(x => x.UploadedAt)
                .Take(query.Take)
                .Select(x => new CourseMaterialListItemRecord
                {
                    MaterialId = x.Id,
                    CourseId = x.CourseId,
                    ChapterId = _db.MaterialChunks
                        .Where(c => c.CourseMaterialId == x.Id && c.ChapterId != null)
                        .OrderBy(c => c.ChunkIndex)
                        .Select(c => c.ChapterId)
                        .FirstOrDefault(),
                    Title = x.Title,
                    MaterialKind = x.MaterialKind,
                    OriginalFileName = x.OriginalFileName,
                    FileExtension = x.FileExtension,
                    FileSizeBytes = x.FileSizeBytes,
                    ParseStatus = x.ParseStatus,
                    ParseMessage = x.ParseMessage,
                    ChunkCount = x.Chunks.Count,
                    UploadedAt = x.UploadedAt,
                    ParsedAt = x.ParsedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<NetworkResource>> ListNetworkResourcesAsync(
            NetworkResourcesListQuery query,
            CancellationToken cancellationToken)
        {
            var resourceQuery = _db.NetworkResources.AsNoTracking().AsQueryable();

            if (query.EnabledOnly)
            {
                resourceQuery = resourceQuery.Where(x => x.IsEnabled);
            }

            if (query.CourseId.HasValue && query.CourseId.Value > 0)
            {
                resourceQuery = resourceQuery.Where(x => x.CourseId == query.CourseId.Value);
            }

            return await resourceQuery
                .OrderBy(x => x.Category)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .ToListAsync(cancellationToken);
        }

        public async Task<NetworkResource?> GetNetworkResourceByIdAsync(
            NetworkResourceByIdQuery query,
            CancellationToken cancellationToken)
        {
            return await _db.NetworkResources
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == query.ResourceId, cancellationToken);
        }

        public async Task<NetworkResource> CreateNetworkResourceAsync(
            CreateNetworkResourceCommand command,
            CancellationToken cancellationToken)
        {
            _db.NetworkResources.Add(command.Resource);
            await _db.SaveChangesAsync(cancellationToken);
            return command.Resource;
        }

        public async Task<NetworkResource?> UpdateNetworkResourceAsync(
            UpdateNetworkResourceCommand command,
            CancellationToken cancellationToken)
        {
            var entity = await _db.NetworkResources
                .FirstOrDefaultAsync(x => x.Id == command.ResourceId, cancellationToken);

            if (entity == null)
            {
                return null;
            }

            if (command.Category != null)
            {
                entity.Category = command.Category.Trim();
            }

            if (command.Title != null)
            {
                entity.Title = command.Title.Trim();
            }

            if (command.Description != null)
            {
                entity.Description = command.Description.Trim();
            }

            if (command.Link != null)
            {
                entity.Link = command.Link.Trim();
            }

            if (command.SortOrder.HasValue)
            {
                entity.SortOrder = command.SortOrder.Value;
            }

            if (command.IsEnabled.HasValue)
            {
                entity.IsEnabled = command.IsEnabled.Value;
            }

            entity.UpdatedAt = command.UpdatedAt;

            await _db.SaveChangesAsync(cancellationToken);
            return entity;
        }

        public async Task<bool> DeleteNetworkResourceAsync(
            DeleteNetworkResourceCommand command,
            CancellationToken cancellationToken)
        {
            var entity = await _db.NetworkResources
                .FirstOrDefaultAsync(x => x.Id == command.ResourceId, cancellationToken);

            if (entity == null)
            {
                return false;
            }

            _db.NetworkResources.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<List<string>> NormalizeKnowledgePointsAsync(
            NormalizeKnowledgePointsQuery query,
            CancellationToken cancellationToken)
        {
            var existingCodes = await _db.KnowledgePoints
                .AsNoTracking()
                .Where(x => x.CourseId == query.CourseId && x.Code != null && x.Code != "")
                .Select(x => x.Code!)
                .Distinct()
                .ToListAsync(cancellationToken);

            string? chapterName = null;
            if (query.ChapterId.HasValue)
            {
                chapterName = await _db.Chapters
                    .AsNoTracking()
                    .Where(x => x.Id == query.ChapterId.Value)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return await KnowledgePointNormalizer.NormalizeAsync(
                query.RawKnowledgePoints,
                new KnowledgePointNormalizationSnapshot
                {
                    ExistingCodes = existingCodes,
                    ChapterName = chapterName
                },
                query.ProblemText,
                query.StudentSolutionText,
                cancellationToken);
        }

        public async Task<AnalysisPersistenceSession?> InitializeAnalysisSessionAsync(
            UAOInputModel request,
            string analysisMode,
            CancellationToken cancellationToken)
        {
            var course = await GetCourseAsync(new CourseByIdQuery(request.CourseId), cancellationToken);
            if (course == null)
            {
                return null;
            }

            Chapter? chapter = null;
            if (request.ChapterId.HasValue)
            {
                chapter = await GetChapterAsync(new ChapterByCourseQuery(request.CourseId, request.ChapterId.Value), cancellationToken);
            }

            var aggregate = await CreateProblemAggregateAsync(
                request,
                course,
                chapter,
                cancellationToken);

            var pendingAnalysisResult = await CreatePendingAnalysisResultAsync(
                aggregate.Problem,
                aggregate.StudentSolution,
                analysisMode,
                course,
                chapter,
                cancellationToken);

            return new AnalysisPersistenceSession
            {
                Course = course,
                Chapter = chapter,
                Problem = aggregate.Problem,
                StudentSolution = aggregate.StudentSolution,
                PendingAnalysisResult = pendingAnalysisResult
            };
        }

        public async Task<(Problem Problem, StudentSolution? StudentSolution)> CreateProblemAggregateAsync(
            UAOInputModel request,
            Course course,
            Chapter? chapter,
            CancellationToken cancellationToken)
        {
            var problem = new Problem
            {
                CourseId = request.CourseId,
                ChapterId = chapter?.Id,
                Title = null,
                ContentMarkdown = request.ProblemText.Trim(),
                ContentLatex = null,
                SourceType = "text",
                SourceFilePath = null,
                PhotoSolutionOcrRecordId = request.OcrRecordId,
                StructuredProblemId = request.StructuredProblemId,
                ProblemType = "mixed",
                Difficulty = null,
                CreatedByUserId = request.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Problems.Add(problem);
            await _db.SaveChangesAsync(cancellationToken);

            StudentSolution? studentSolution = null;
            if (!string.IsNullOrWhiteSpace(request.StudentSolutionText))
            {
                studentSolution = new StudentSolution
                {
                    ProblemId = problem.Id,
                    UserId = request.UserId,
                    SolutionText = request.StudentSolutionText.Trim(),
                    SubmittedAt = DateTime.UtcNow
                };

                _db.StudentSolutions.Add(studentSolution);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return (problem, studentSolution);
        }

        public async Task<AnalysisResult> CreatePendingAnalysisResultAsync(
            Problem problem,
            StudentSolution? studentSolution,
            string analysisMode,
            Course course,
            Chapter? chapter,
            CancellationToken cancellationToken)
        {
            var entity = new AnalysisResult
            {
                ProblemId = problem.Id,
                StudentSolutionId = studentSolution?.Id,
                PhotoSolutionOcrRecordId = problem.PhotoSolutionOcrRecordId,
                StructuredProblemId = problem.StructuredProblemId,
                AnalysisMode = analysisMode,
                CourseName = course.Name,
                ChapterName = chapter?.Name,
                ProblemType = "unknown",
                Difficulty = "unknown",
                KnowledgePointsJson = "[]",
                StandardSolution = null,
                StudentSolutionReview = null,
                MistakeTagsJson = "[]",
                ReviewSuggestionsJson = "[]",
                RawResponseJson = null,
                AnswerReliability = AnswerReliability.Uncertain,
                NeedsReview = true,
                ReliabilityReasonsJson = "[]",
                VerifierWarningsJson = "[]",
                VerifiedAt = null,
                AiJudgedCorrect = null,
                FinalCorrect = null,
                FinalCorrectSource = CorrectnessSource.Ai,
                CreatedAt = DateTime.UtcNow
            };

            _db.AnalysisResults.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return entity;
        }

        public async Task<AnalysisResult> SaveLlmFailedAsync(
            AnalysisResult pending,
            UAOInputModel request,
            Course course,
            Chapter? chapter,
            LLMChatResponseDto llmResponse,
            CancellationToken cancellationToken)
        {
            var errorPayload = JsonSerializer.Serialize(new
            {
                status = "llm_failed",
                error_code = llmResponse.ErrorCode,
                error_message = llmResponse.ErrorMessage,
                latency_ms = llmResponse.LatencyMs
            });

            pending.RawResponseJson = errorPayload;
            pending.ProblemType = "unknown";
            pending.Difficulty = "unknown";
            pending.KnowledgePointsJson = "[]";
            pending.StandardSolution = null;
            pending.StudentSolutionReview = null;
            pending.MistakeTagsJson = "[]";
            pending.ReviewSuggestionsJson = "[]";
            pending.AiJudgedCorrect = null;
            pending.FinalCorrect = null;
            await _db.SaveChangesAsync(cancellationToken);
            return pending;
        }

        public async Task<AnalysisResult> SaveParseFailedAsync(
            AnalysisResult pending,
            UAOInputModel request,
            Course course,
            Chapter? chapter,
            LLMChatResponseDto llmResponse,
            string parseError,
            CancellationToken cancellationToken)
        {
            var payload = JsonSerializer.Serialize(new
            {
                rawContent = llmResponse.Content,
                parseError,
                latencyMs = llmResponse.LatencyMs,
                tokenUsage = new
                {
                    prompt = llmResponse.PromptTokenCount,
                    completion = llmResponse.CompletionTokenCount,
                    total = llmResponse.TotalTokenCount
                }
            });

            pending.RawResponseJson = payload;
            pending.ProblemType = "unknown";
            pending.Difficulty = "unknown";
            pending.KnowledgePointsJson = "[]";
            pending.StandardSolution = null;
            pending.StudentSolutionReview = null;
            pending.MistakeTagsJson = "[]";
            pending.ReviewSuggestionsJson = "[]";
            pending.AiJudgedCorrect = null;
            pending.FinalCorrect = null;
            await _db.SaveChangesAsync(cancellationToken);
            return pending;
        }

        public async Task<AnalysisResult> SaveSchemaInvalidAsync(
            AnalysisResult pending,
            UAOInputModel request,
            Course course,
            Chapter? chapter,
            LLMChatResponseDto llmResponse,
            AnalysisResultModel parsed,
            string validationError,
            CancellationToken cancellationToken)
        {
            return await SaveParseFailedAsync(
                pending,
                request,
                course,
                chapter,
                llmResponse,
                $"llm_schema_invalid: {validationError}",
                cancellationToken);
        }

        public async Task<AnalysisResult> SaveSuccessAsync(
            AnalysisResult pending,
            AnalysisResultModel parsed,
            LLMChatResponseDto llmResponse,
            CancellationToken cancellationToken)
        {
            pending.CourseName = parsed.Course;
            pending.ChapterName = parsed.Chapter;
            pending.ProblemType = parsed.ProblemType;
            pending.Difficulty = parsed.Difficulty;
            pending.KnowledgePointsJson = JsonSerializer.Serialize(parsed.KnowledgePoints);
            pending.StandardSolution = JsonSerializer.Serialize(parsed.StandardSolution.Select(x => new StandardSolutionStep
            {
                Step = x.Step,
                Title = x.Title,
                Content = x.Content
            }));
            pending.StudentSolutionReview = JsonSerializer.Serialize(new StudentSolutionReview
            {
                IsCorrect = parsed.StudentSolutionReview.IsCorrect,
                MainIssue = parsed.StudentSolutionReview.MainIssue,
                LogicGaps = parsed.StudentSolutionReview.LogicGaps,
                Suggestions = parsed.StudentSolutionReview.Suggestions
            });
            pending.MistakeTagsJson = JsonSerializer.Serialize(parsed.MistakeTags);
            pending.ReviewSuggestionsJson = JsonSerializer.Serialize(parsed.ReviewSuggestions);
            pending.RawResponseJson = llmResponse.Content;
            pending.AiJudgedCorrect = parsed.StudentSolutionReview.IsCorrect;
            pending.FinalCorrect = parsed.StudentSolutionReview.IsCorrect;
            pending.FinalCorrectSource = CorrectnessSource.Ai;
            await _db.SaveChangesAsync(cancellationToken);
            return pending;
        }

        public async Task<AnalysisResult> SaveVerificationAsync(
            AnalysisResult analysisResult,
            StructuredProblem? structuredProblem,
            PhotoSolutionOcrRecord? ocrRecord,
            AnswerReliability answerReliability,
            bool needsReview,
            string reliabilityReasonsJson,
            string verifierWarningsJson,
            DateTime verifiedAt,
            CancellationToken cancellationToken)
        {
            analysisResult.AnswerReliability = answerReliability;
            analysisResult.NeedsReview = needsReview;
            analysisResult.ReliabilityReasonsJson = reliabilityReasonsJson;
            analysisResult.VerifierWarningsJson = verifierWarningsJson;
            analysisResult.VerifiedAt = verifiedAt;

            if (structuredProblem != null && analysisResult.StructuredProblemId == null)
            {
                analysisResult.StructuredProblemId = structuredProblem.Id;
            }

            if (ocrRecord != null && analysisResult.PhotoSolutionOcrRecordId == null)
            {
                analysisResult.PhotoSolutionOcrRecordId = ocrRecord.Id;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return analysisResult;
        }

        public async Task<AnalysisVerificationArtifacts> LoadVerificationArtifactsAsync(
            UAOInputModel request,
            Problem problem,
            CancellationToken cancellationToken)
        {
            StructuredProblem? structuredProblem = null;
            if (problem.StructuredProblemId.HasValue)
            {
                structuredProblem = await GetStructuredProblemAsync(
                    new StructuredProblemByIdQuery(problem.StructuredProblemId.Value),
                    cancellationToken);
            }
            else if (request.StructuredProblemId.HasValue)
            {
                structuredProblem = await GetStructuredProblemAsync(
                    new StructuredProblemByIdQuery(request.StructuredProblemId.Value),
                    cancellationToken);
            }

            PhotoSolutionOcrRecord? ocrRecord = null;
            if (problem.PhotoSolutionOcrRecordId.HasValue)
            {
                ocrRecord = await GetPhotoSolutionOcrRecordAsync(
                    new PhotoSolutionOcrRecordByIdQuery(problem.PhotoSolutionOcrRecordId.Value),
                    cancellationToken);
            }
            else if (request.OcrRecordId.HasValue)
            {
                ocrRecord = await GetPhotoSolutionOcrRecordAsync(
                    new PhotoSolutionOcrRecordByIdQuery(request.OcrRecordId.Value),
                    cancellationToken);
            }

            return new AnalysisVerificationArtifacts
            {
                StructuredProblem = structuredProblem,
                OcrRecord = ocrRecord
            };
        }
    }
}
