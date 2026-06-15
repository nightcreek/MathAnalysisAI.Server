using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.DTOs.LLM;
using MathAnalysisAI.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Services.Analysis.LLM
{
    public sealed class LlmRequestFactory : ILlmRequestFactory
    {
        private readonly ApplicationDbContext _db;

        public LlmRequestFactory(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<LLMChatRequestDto> BuildAsync(
            AnalysisRequestDto request,
            Course course,
            Chapter? chapter,
            Problem problem,
            StudentSolution? studentSolution,
            AnalysisContextDto? context,
            int analysisResultId,
            CancellationToken cancellationToken)
        {
            var mode = string.IsNullOrWhiteSpace(request.AnalysisMode)
                ? "review_solution"
                : request.AnalysisMode.Trim();

            var promptProfile = await ResolvePromptProfileAsync(request.CourseId, mode, cancellationToken);

            var systemPrompt = promptProfile?.SystemPrompt ??
                @"你是面向大学生的数学分析课程学习智能体，不是泛化数学聊天机器人，也不是通用解题引擎。
系统当前只聚焦数学分析课程，优先识别章节、知识点、题型，再给出结构化分析。
如果题目明显超出当前课程范围，请说明“可能超出当前数学分析课程范围”，但仍要在已有信息内给出有限帮助。
请按 JSON 返回结构化分析，并主动检查定义域、端点、连续性、可导性、可积性、级数判别法条件、一致收敛条件、反常积分瑕点拆分、幂级数端点、泰勒余项、极限与积分交换条件。";

            var template = promptProfile?.UserPromptTemplate ??
                @"你现在处理的是数学分析课程题目，请先识别章节、知识点和题型，再给出结构化分析。
请把题目理解、定理条件检查、解题策略、步骤理由和最终结论都体现在输出中。
若题目超出数学分析课程范围，请明确标注“可能超出当前课程范围”，但仍给出有限帮助。

{""course"":""{{course_name}}"",""chapter"":""{{chapter_name}}"",""analysis_mode"":""{{analysis_mode}}"",""problemText"":""{{problem_text}}"",""studentSolutionText"":""{{student_solution_text}}"",""knowledgeContext"":{{knowledge_points_context_json}}}

请严格按指定 schema 输出且只输出一个 JSON object。";

            var userPrompt = template
                .Replace("{{course_name}}", EscapeForTemplate(course.Name))
                .Replace("{{chapter_name}}", EscapeForTemplate(chapter?.Name ?? string.Empty))
                .Replace("{{analysis_mode}}", EscapeForTemplate(mode))
                .Replace("{{problem_text}}", EscapeForTemplate(problem.ContentMarkdown))
                .Replace("{{student_solution_text}}", EscapeForTemplate(studentSolution?.SolutionText ?? string.Empty))
                .Replace("{{knowledge_points_context_json}}", "[]");

            var contextBlock = RenderContextBlock(context);
            if (!string.IsNullOrWhiteSpace(contextBlock))
            {
                userPrompt = $"{userPrompt}\n\n{contextBlock}";
            }

            return new LLMChatRequestDto
            {
                Provider = "deepseek",
                ModelName = "deepseek-chat",
                RequestType = string.IsNullOrWhiteSpace(request.AnalysisMode) ? "review_solution" : request.AnalysisMode,
                UserId = request.UserId,
                AnalysisResultId = analysisResultId,
                Messages = new List<LLMChatMessageDto>
                {
                    new() { Role = "system", Content = systemPrompt },
                    new() { Role = "user", Content = userPrompt }
                }
            };
        }

        private async Task<PromptProfile?> ResolvePromptProfileAsync(int courseId, string mode, CancellationToken cancellationToken)
        {
            return await _db.PromptProfiles
                .AsNoTracking()
                .Where(x => x.CourseId == courseId && x.Mode == mode && x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static string EscapeForTemplate(string input)
        {
            return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string RenderContextBlock(AnalysisContextDto? context)
        {
            return string.IsNullOrWhiteSpace(context?.PromptContextBlock)
                ? string.Empty
                : context!.PromptContextBlock.Trim();
        }
    }
}
