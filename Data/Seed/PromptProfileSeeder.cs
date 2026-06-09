using System.Text.Json;
using MathAnalysisAI.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Data.Seed
{
    public static class PromptProfileSeeder
    {
        private const string DefaultVersion = "v4";

        public static Task<int> SeedAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
        {
            return SeedMathAnalysisPromptProfilesAsync(db, cancellationToken);
        }

        public static async Task<int> SeedMathAnalysisPromptProfilesAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
        {
            var courseId = PlatformSeedData.CourseMathAnalysisId;
            var now = DateTime.UtcNow;
            var outputSchemaJson = BuildOutputSchemaJson();

            var seedItems = new List<PromptProfile>
            {
                BuildProfile(courseId, "solve", DefaultVersion, "Math Analysis Solve v4", BuildSystemPrompt("solve"), BuildUserPromptTemplate()),
                BuildProfile(courseId, "review_solution", DefaultVersion, "Math Analysis Review Solution v4", BuildSystemPrompt("review_solution"), BuildUserPromptTemplate()),
                BuildProfile(courseId, "hint", DefaultVersion, "Math Analysis Hint v4", BuildSystemPrompt("hint"), BuildUserPromptTemplate()),
                BuildProfile(courseId, "exam_mode", DefaultVersion, "Math Analysis Exam Mode v4", BuildSystemPrompt("exam_mode"), BuildUserPromptTemplate()),
                BuildProfile(courseId, "concept_explain", DefaultVersion, "Math Analysis Concept Explain v4", BuildSystemPrompt("concept_explain"), BuildUserPromptTemplate())
            };

            foreach (var item in seedItems)
            {
                item.OutputSchemaJson = outputSchemaJson;
                item.CreatedAt = now;
            }

            var inserted = 0;
            foreach (var item in seedItems)
            {
                var exists = await db.PromptProfiles.AnyAsync(
                    x => x.CourseId == item.CourseId && x.Mode == item.Mode && x.Version == item.Version,
                    cancellationToken
                );

                if (exists)
                {
                    continue;
                }

                db.PromptProfiles.Add(item);
                inserted++;
            }

            if (inserted > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            return inserted;
        }

        private static PromptProfile BuildProfile(
            int courseId,
            string mode,
            string version,
            string name,
            string systemPrompt,
            string userPromptTemplate)
        {
            return new PromptProfile
            {
                CourseId = courseId,
                Mode = mode,
                Version = version,
                Name = name,
                IsActive = true,
                SystemPrompt = systemPrompt,
                UserPromptTemplate = userPromptTemplate,
                OutputSchemaJson = "{}"
            };
        }

        private static string BuildSystemPrompt(string mode)
        {
            return $@"你是面向大学生的数学分析课程学习智能体，不是泛化数学聊天机器人，也不是通用解题引擎。
系统当前只聚焦数学分析课程，所有回答都应优先服务于课程学习、题型识别、知识点定位、方法选择、步骤推导和错因分析。
当前分析模式: {mode}。
请基于题目、学生解答、课程知识点上下文给出结构化分析结果。
如果题目明显超出当前课程范围，你可以明确说明“可能超出当前数学分析课程范围”，但仍要在已有信息内给出有限帮助，不要强行泛化成别的学科。
输出时必须体现以下内容：题目理解、涉及章节、涉及知识点、解题策略、使用的定义/定理/判别法、每步推导理由、最终结论。
尤其要主动检查并明确说明：
1) 定义域、端点、连续性、可导性、可积性；
2) 级数判别法条件、一致收敛条件、反常积分瑕点拆分；
3) 幂级数端点、泰勒余项、极限与积分交换条件；
4) 逐点收敛 vs 一致收敛、条件收敛 vs 绝对收敛、必要条件 vs 充分条件；
5) 反常积分趋于 0 vs 收敛、偏导存在 vs 可微；
6) 路径无关 vs 曲线积分为 0、第一类/第二类曲线积分、第一类/第二类曲面积分。

你必须且只能输出一个 JSON object，并满足以下约束：
1) 不允许输出 Markdown。
2) 不允许输出 code fence。
3) 不允许输出 schema 外字段。
4) 顶层字段必须全部出现，即使为空也必须输出。
5) 字段名必须严格使用 camelCase。
6) 顶层字段必须且只能是：
   course, chapter, problemType, difficulty, knowledgePoints, solutionOverview, standardSolution, studentSolutionReview, mistakeTags, reviewSuggestions, visualization。
7) 类型约束必须满足：
   standardSolution 必须是 array；
   studentSolutionReview 必须是 object；
   knowledgePoints/mistakeTags/reviewSuggestions 必须是 array；
   visualization 必须是 object。
8) 在 review_solution 模式下，只要学生解答存在明显逻辑错误或理由不充分，studentSolutionReview.isCorrect 必须为 false。
9) 不允许在能判断正误时输出 null；只有题目或学生解答信息不足、完全无法判断时才可为 null。";
        }

        private static string BuildUserPromptTemplate()
        {
            return @"你现在处理的是数学分析课程题目，请先识别章节、知识点和题型，再给出结构化分析。
请把题目理解、定理条件检查、解题策略、步骤理由和最终结论都体现在输出中。
若题目超出数学分析课程范围，请明确标注“可能超出当前课程范围”，但仍给出有限帮助。

{
  ""course"": ""{{course_name}}"",
  ""chapter"": ""{{chapter_name}}"",
  ""analysisMode"": ""{{analysis_mode}}"",
  ""problemText"": ""{{problem_text}}"",
  ""studentSolutionText"": ""{{student_solution_text}}"",
  ""knowledgeContext"": {{knowledge_points_context_json}}
}

请严格按指定 schema 输出且只输出一个 JSON object。
禁止 Markdown、禁止 code fence、禁止额外文字、禁止 schema 外字段。";
        }

        private static string BuildOutputSchemaJson()
        {
            var schema = new
            {
                course = "数学分析",
                chapter = "反常积分",
                problemType = "improper_integral_convergence",
                difficulty = "medium",
                knowledgePoints = new[]
                {
                    "ma.improper_integral.definition",
                    "ma.improper_integral.comparison_test"
                },
                solutionOverview = "...",
                standardSolution = new[]
                {
                    new { step = 1, title = "...", content = "..." }
                },
                studentSolutionReview = new
                {
                    isCorrect = false,
                    mainIssue = "...",
                    logicGaps = new[] { "..." },
                    suggestions = new[] { "..." }
                },
                mistakeTags = new[] { "invalid_convergence_reason" },
                reviewSuggestions = new[] { "..." },
                visualization = new
                {
                    shouldUse = true,
                    engine = "geogebra",
                    visualizationType = "function_decay",
                    reason = "...",
                    geoGebraCommands = new[] { "f(x)=1/x^2", "Integral(f,1,10)" },
                    caption = "..."
                }
            };

            return JsonSerializer.Serialize(schema);
        }
    }
}
