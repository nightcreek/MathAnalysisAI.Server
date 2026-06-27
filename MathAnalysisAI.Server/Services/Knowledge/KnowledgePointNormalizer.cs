namespace MathAnalysisAI.Server.Services.Knowledge
{
    public static class KnowledgePointNormalizer
    {
        private static readonly Dictionary<string, string[]> LabelToCodeCandidates = new(StringComparer.OrdinalIgnoreCase)
        {
            ["反常积分"] = new[] { "ma.improper_integral.convergence_criteria", "ma.improper_integral.comparison_test" },
            ["反常积分收敛性"] = new[] { "ma.improper_integral.convergence_criteria", "ma.improper_integral.comparison_test" },
            ["反常积分的收敛性判定"] = new[] { "ma.improper_integral.convergence_criteria", "ma.improper_integral.comparison_test" },
            ["无穷限反常积分"] = new[] { "ma.improper_integral.infinite_interval" },
            ["无穷区间反常积分"] = new[] { "ma.improper_integral.infinite_interval" },
            ["p-积分"] = new[] { "ma.improper_integral.convergence_criteria" },
            ["p积分"] = new[] { "ma.improper_integral.convergence_criteria" },
            ["比较判别法"] = new[] { "ma.improper_integral.comparison_test" },
            ["积分判别"] = new[] { "ma.improper_integral.comparison_test", "ma.improper_integral.convergence_criteria" },
            ["ma.improper_integral.convergence_criteria"] = new[] { "ma.improper_integral.convergence_criteria" },

            ["重积分"] = new[] { "ma.multiple_integral.concept", "ma.multiple_integral.double_integral", "ma.multiple_integral.triple_integral" },
            ["multipleintegral"] = new[] { "ma.multiple_integral.concept" },
            ["doubleintegral"] = new[] { "ma.multiple_integral.double_integral" },
            ["tripleintegral"] = new[] { "ma.multiple_integral.triple_integral" },
            ["二重积分"] = new[] { "ma.multiple_integral.double_integral" },
            ["三重积分"] = new[] { "ma.multiple_integral.triple_integral" },
            ["积分次序"] = new[] { "ma.multiple_integral.region_order" },
            ["变量替换"] = new[] { "ma.multiple_integral.change_of_variables" },
            ["极坐标"] = new[] { "ma.multiple_integral.coordinate_systems" },
            ["柱坐标"] = new[] { "ma.multiple_integral.coordinate_systems" },
            ["球坐标"] = new[] { "ma.multiple_integral.coordinate_systems" },

            ["曲线积分"] = new[] { "ma.line_integral.concept", "ma.line_integral.scalar", "ma.line_integral.vector" },
            ["lineintegral"] = new[] { "ma.line_integral.concept" },
            ["scalarlineintegral"] = new[] { "ma.line_integral.scalar" },
            ["vectorlineintegral"] = new[] { "ma.line_integral.vector" },
            ["第一类曲线积分"] = new[] { "ma.line_integral.scalar" },
            ["第二类曲线积分"] = new[] { "ma.line_integral.vector" },
            ["路径无关"] = new[] { "ma.line_integral.path_independence" },
            ["路径无关性"] = new[] { "ma.line_integral.path_independence" },
            ["保守场"] = new[] { "ma.line_integral.path_independence" },
            ["green公式"] = new[] { "ma.line_integral.green_formula" },
            ["green公式定理"] = new[] { "ma.line_integral.green_formula" },

            ["曲面积分"] = new[] { "ma.surface_integral.concept", "ma.surface_integral.scalar", "ma.surface_integral.flux" },
            ["surfaceintegral"] = new[] { "ma.surface_integral.concept" },
            ["scalarsurfaceintegral"] = new[] { "ma.surface_integral.scalar" },
            ["fluxintegral"] = new[] { "ma.surface_integral.flux" },
            ["第一类曲面积分"] = new[] { "ma.surface_integral.scalar" },
            ["第二类曲面积分"] = new[] { "ma.surface_integral.flux" },
            ["gauss公式"] = new[] { "ma.surface_integral.gauss_formula" },
            ["stokes公式"] = new[] { "ma.surface_integral.stokes_formula" },

            ["一致收敛"] = new[] { "ma.function_series.uniform_convergence" },
            ["uniformconvergence"] = new[] { "ma.function_series.uniform_convergence" },
            ["函数项级数一致收敛"] = new[] { "ma.function_series.uniform_convergence.criteria" },
            ["逐点收敛"] = new[] { "ma.function_series.pointwise_convergence" },
            ["pointwiseconvergence"] = new[] { "ma.function_series.pointwise_convergence" },
            ["逐点收敛与一致收敛区分"] = new[] { "ma.function_series.pointwise_vs_uniform" },
            ["幂级数端点"] = new[] { "ma.power_series.endpoint_convergence" },
            ["endpointconvergence"] = new[] { "ma.power_series.endpoint_convergence" },
            ["泰勒余项"] = new[] { "ma.power_series.taylor_remainder" },
            ["remainderterm"] = new[] { "ma.power_series.taylor_remainder" },
            ["瑕点"] = new[] { "ma.improper_integral.singularity_split" },
            ["奇点"] = new[] { "ma.improper_integral.singularity_split" },
            ["反常积分瑕点拆分"] = new[] { "ma.improper_integral.singularity_split" },
            ["improperintegralsingularity"] = new[] { "ma.improper_integral.singularity_split" },
            ["中值定理条件"] = new[] { "ma.mean_value_theorem.conditions_check" },
            ["中值定理条件检查"] = new[] { "ma.mean_value_theorem.conditions_check" },
            ["meanvaluetheoremconditions"] = new[] { "ma.mean_value_theorem.conditions_check" },
            ["极限与积分交换"] = new[] { "ma.function_series.limit_exchange_conditions" },
            ["极限与积分交换条件"] = new[] { "ma.function_series.limit_exchange_conditions" },
            ["interchangeoflimitandintegral"] = new[] { "ma.function_series.limit_exchange_conditions" }
        };

        public static Task<List<string>> NormalizeAsync(
            IEnumerable<string>? rawKnowledgePoints,
            KnowledgePointNormalizationSnapshot snapshot,
            string problemText,
            string? studentSolutionText,
            CancellationToken cancellationToken = default)
        {
            var existingSet = new HashSet<string>(snapshot.ExistingCodes, StringComparer.OrdinalIgnoreCase);
            var output = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in rawKnowledgePoints ?? Enumerable.Empty<string>())
            {
                var label = NormalizeLabel(raw);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                // Already a valid code.
                if (existingSet.Contains(label))
                {
                    AddCode(label, seen, output);
                    continue;
                }

                if (!LabelToCodeCandidates.TryGetValue(label, out var candidates))
                {
                    continue;
                }

                foreach (var candidate in candidates)
                {
                    if (existingSet.Contains(candidate))
                    {
                        AddCode(candidate, seen, output);
                        break;
                    }
                }
            }

            if (output.Count == 0 && IsImproperIntegralContext(snapshot.ChapterName, problemText, studentSolutionText))
            {
                foreach (var fallback in ResolveImproperIntegralFallbackCodes(existingSet))
                {
                    AddCode(fallback, seen, output);
                }
            }

            return Task.FromResult(output);
        }

        private static IEnumerable<string> ResolveImproperIntegralFallbackCodes(HashSet<string> existingSet)
        {
            // Prefer requested code when available; fallback to actually seeded equivalents.
            var preferred = new[]
            {
                "ma.improper_integral.definition",
                "ma.improper_integral.convergence_criteria"
            };
            var first = preferred.FirstOrDefault(existingSet.Contains);
            if (!string.IsNullOrWhiteSpace(first))
            {
                yield return first;
            }

            if (existingSet.Contains("ma.improper_integral.comparison_test"))
            {
                yield return "ma.improper_integral.comparison_test";
            }
        }

        private static bool IsImproperIntegralContext(
            string? chapterName,
            string problemText,
            string? studentSolutionText)
        {
            if (!string.IsNullOrWhiteSpace(chapterName)
                && chapterName.Contains("反常积分", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var combined = $"{problemText} {studentSolutionText}".ToLowerInvariant();
            return combined.Contains("∞")
                || combined.Contains("无穷")
                || combined.Contains("反常积分")
                || combined.Contains("∫_1^∞")
                || combined.Contains("∫1^∞");
        }

        private static string NormalizeLabel(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return input.Trim()
                .Replace("（", "(")
                .Replace("）", ")")
                .Replace(" ", string.Empty);
        }

        private static void AddCode(string code, HashSet<string> seen, List<string> output)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            if (seen.Add(code))
            {
                output.Add(code);
            }
        }
    }

    public sealed class KnowledgePointNormalizationSnapshot
    {
        public IReadOnlyCollection<string> ExistingCodes { get; init; } = Array.Empty<string>();
        public string? ChapterName { get; init; }
    }
}
