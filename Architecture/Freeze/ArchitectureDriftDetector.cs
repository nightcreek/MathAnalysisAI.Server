using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Architecture.Freeze;

public sealed class ArchitectureDriftDetector
{
    private const string BlockingCategory = "blocking";
    private const string ObservationCategory = "observation";

    private static readonly Regex UsingRegex = new(@"^[ \t]*using\s+(?<ns>[A-Za-z0-9_\.]+)\s*;", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ApiRouteRegex = new(@"/api/[A-Za-z0-9_\-/{}/]+", RegexOptions.Compiled);
    private static readonly string[] AllowedIdentityFiles =
    [
        "MathAnalysisAI.Server/Services/Auth/AuthService.cs",
        "MathAnalysisAI.Server/Services/Auth/CurrentUserService.cs",
        "MathAnalysisAI.Server/Services/Auth/IdentityKernel.cs",
        "MathAnalysisAI.Server/Services/Auth/AuthPersistenceService.cs",
        "MathAnalysisAI.Server/Data/Auth/AuthPersistenceService.cs",
        "MathAnalysisAI.Server/Program.cs"
    ];

    private static readonly string[] DataAccessConcreteFiles =
    [
        "MathAnalysisAI.Server/Services/Analysis/Persistence/AnalysisPersistenceService.cs",
        "MathAnalysisAI.Server/Services/Analysis/Structuring/ProblemStructuringService.cs",
        "MathAnalysisAI.Server/Services/LLM/LLMGateway.cs",
        "MathAnalysisAI.Server/Services/ExceptionHandling/DbHealthCheck.cs",
        "MathAnalysisAI.Server/Services/ExceptionHandling/ApiExceptionClassifier.cs"
    ];

    private readonly string _repoRoot;

    public ArchitectureDriftDetector(string repoRoot)
    {
        _repoRoot = repoRoot;
    }

    public void WriteContractFreezeBaseline(string baselinePath)
    {
        var snapshot = BuildContractFreezeSnapshot();
        Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
        File.WriteAllText(baselinePath, System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    public ArchitectureDriftDetectionResult Run(string baselinePath)
    {
        var violations = new List<FreezeViolation>();
        var intelligenceViolations = new List<FreezeViolation>();
        var sourceFiles = EnumerateServerSourceFiles().ToList();

        foreach (var file in sourceFiles)
        {
            var relativePath = NormalizePath(Path.GetRelativePath(_repoRoot, file));
            var content = File.ReadAllText(file);
            var lines = File.ReadAllLines(file);

            CheckControllerIntelligenceLeak(relativePath, lines, violations);
            CheckServiceDataBypass(relativePath, lines, violations);
            CheckEfCoreOutsideDataLayer(relativePath, lines, violations);
            CheckIntelligenceIsolation(relativePath, lines, violations, intelligenceViolations);
            CheckIdentityKernelDrift(relativePath, lines, violations);
        }

        CheckFrontendRuntimeFreeze(violations);
        CheckContractFreezeBaseline(baselinePath, violations);

        var driftReport = new ArchitectureDriftReport
        {
            RepoRoot = _repoRoot.Replace('\\', '/'),
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = BuildSummary(violations),
            Violations = violations
                .OrderBy(v => v.File, StringComparer.Ordinal)
                .ThenBy(v => v.Line)
                .ThenBy(v => v.RuleId, StringComparer.Ordinal)
                .ToList()
        };

        var intelligenceReport = new IntelligenceIsolationReport
        {
            RepoRoot = _repoRoot.Replace('\\', '/'),
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = BuildSummary(intelligenceViolations),
            Violations = intelligenceViolations
                .OrderBy(v => v.File, StringComparer.Ordinal)
                .ThenBy(v => v.Line)
                .ThenBy(v => v.RuleId, StringComparer.Ordinal)
                .ToList()
        };

        return new ArchitectureDriftDetectionResult(driftReport, intelligenceReport);
    }

    private void CheckControllerIntelligenceLeak(string relativePath, IReadOnlyList<string> lines, List<FreezeViolation> violations)
    {
        if (!relativePath.StartsWith("MathAnalysisAI.Server/Controllers/", StringComparison.Ordinal))
        {
            return;
        }

        AddMatches(
            violations,
            relativePath,
            lines,
            "controller",
            "CONTROLLER_INTELLIGENCE_IMPLEMENTATION_REFERENCE",
            "Controllers must not reference Intelligence implementation namespaces directly.",
            @"using\s+MathAnalysisAI\.Server\.Intelligence\.(?!Interfaces\b)",
            BlockingCategory,
            "Controllers should depend on application services only, never Intelligence implementation classes.");
    }

    private void CheckServiceDataBypass(string relativePath, IReadOnlyList<string> lines, List<FreezeViolation> violations)
    {
        if (!relativePath.StartsWith("MathAnalysisAI.Server/Services/", StringComparison.Ordinal) || IsDataAccessFile(relativePath))
        {
            return;
        }

        AddMatches(
            violations,
            relativePath,
            lines,
            "backend-application",
            "SERVICE_DATA_LAYER_BYPASS",
            "Application services must not depend on concrete data access namespaces.",
            @"using\s+MathAnalysisAI\.Server\.Data\.",
            BlockingCategory,
            "Inject a persistence-facing interface instead of referencing Data layer implementations.");
    }

    private void CheckEfCoreOutsideDataLayer(string relativePath, IReadOnlyList<string> lines, List<FreezeViolation> violations)
    {
        if (IsDataAccessFile(relativePath) || string.Equals(relativePath, "MathAnalysisAI.Server/Program.cs", StringComparison.Ordinal))
        {
            return;
        }

        AddMatches(
            violations,
            relativePath,
            lines,
            InferLayer(relativePath),
            "EF_CORE_OUTSIDE_DATA_LAYER",
            "EF Core usage must remain inside the Data Access layer.",
            @"\b(ApplicationDbContext|DbContext|DbSet<|Microsoft\.EntityFrameworkCore)\b",
            BlockingCategory,
            "Move EF Core usage behind a Data Access adapter or persistence-facing interface.");
    }

    private void CheckIntelligenceIsolation(
        string relativePath,
        IReadOnlyList<string> lines,
        List<FreezeViolation> driftViolations,
        List<FreezeViolation> intelligenceViolations)
    {
        if (!relativePath.StartsWith("MathAnalysisAI.Server/Intelligence/", StringComparison.Ordinal))
        {
            return;
        }

        AddMatches(
            intelligenceViolations,
            relativePath,
            lines,
            "intelligence",
            "INTELLIGENCE_EF_CORE_REFERENCE",
            "Intelligence layer must not reference EF Core or DbContext.",
            @"\b(ApplicationDbContext|DbContext|DbSet<|Microsoft\.EntityFrameworkCore)\b",
            BlockingCategory,
            "Keep Intelligence pure and move all persistence concerns behind interfaces.");

        AddMatches(
            intelligenceViolations,
            relativePath,
            lines,
            "intelligence",
            "INTELLIGENCE_CONTROLLER_REFERENCE",
            "Intelligence layer must not reference controllers.",
            @"using\s+MathAnalysisAI\.Server\.Controllers\b",
            BlockingCategory,
            "Pass primitive values or DTOs in from the application layer instead of referencing controllers.");

        AddMatches(
            intelligenceViolations,
            relativePath,
            lines,
            "intelligence",
            "INTELLIGENCE_CONCRETE_DATA_REFERENCE",
            "Intelligence layer must not reference concrete persistence implementations.",
            @"using\s+MathAnalysisAI\.Server\.Data\.|using\s+MathAnalysisAI\.Server\.Services\.(?!Analysis\.Domain\b|Analysis\.UAO\b|Analysis\.Application\b)",
            BlockingCategory,
            "Depend on interfaces or DTOs only from Intelligence code.");

        driftViolations.AddRange(intelligenceViolations.Where(v => v.File == relativePath));
    }

    private void CheckIdentityKernelDrift(string relativePath, IReadOnlyList<string> lines, List<FreezeViolation> violations)
    {
        if (AllowedIdentityFiles.Contains(relativePath, StringComparer.Ordinal))
        {
            return;
        }

        AddMatches(
            violations,
            relativePath,
            lines,
            InferLayer(relativePath),
            "IDENTITY_KERNEL_DUPLICATE_PASSWORD_VERIFICATION",
            "Password verification logic must remain in IdentityKernel.",
            @"BCrypt\.Net\.BCrypt\.Verify\s*\(",
            BlockingCategory,
            "Route credential verification through IIdentityKernel instead of duplicating it elsewhere.");

        AddMatches(
            violations,
            relativePath,
            lines,
            InferLayer(relativePath),
            "IDENTITY_KERNEL_DUPLICATE_CLAIM_RESOLUTION",
            "Current-user claim resolution must remain in IdentityKernel.",
            @"FindFirstValue\s*\(\s*""app_user_id""|FindFirstValue\s*\(\s*ClaimTypes\.Name|FindFirstValue\s*\(\s*JwtRegisteredClaimNames\.UniqueName",
            BlockingCategory,
            "Resolve current user through IIdentityKernel or IUserContext instead of parsing claims directly.");
    }

    private void CheckFrontendRuntimeFreeze(List<FreezeViolation> violations)
    {
        var analysisHtmlPath = Path.Combine(_repoRoot, "MathAnalysisAI.Server", "wwwroot", "analysis.html");
        var analysisJsPath = Path.Combine(_repoRoot, "MathAnalysisAI.Server", "wwwroot", "js", "analysis.js");
        var jsRoot = Path.Combine(_repoRoot, "MathAnalysisAI.Server", "wwwroot", "js");

        if (File.Exists(analysisHtmlPath))
        {
            var content = File.ReadAllText(analysisHtmlPath);
            if (!content.Contains("analysisBootstrap.js", StringComparison.Ordinal)
                || !content.Contains("AnalysisBootstrap.start()", StringComparison.Ordinal))
            {
                violations.Add(new FreezeViolation(
                    "FRONTEND_ANALYSIS_BOOTSTRAP_MISSING",
                    "MathAnalysisAI.Server/wwwroot/analysis.html",
                    0,
                    "frontend",
                    BlockingCategory,
                    "analysis.html must be initialized through analysisBootstrap.js only.",
                    "Keep AnalysisBootstrap as the single runtime entry point for the analysis page."));
            }
        }

        if (File.Exists(analysisJsPath))
        {
            var lines = File.ReadAllLines(analysisJsPath);
            AddMatches(
                violations,
                "MathAnalysisAI.Server/wwwroot/js/analysis.js",
                lines,
                "frontend",
                "FRONTEND_ANALYSIS_SUBMIT_LISTENER_FORBIDDEN",
                "analysis.js must not register submit listeners directly.",
                @"addEventListener\s*\(\s*[""']submit[""']",
                BlockingCategory,
                "Keep submit orchestration in analysisBootstrap.js and let analysis.js remain a compatibility shim.");
        }

        foreach (var file in Directory.GetFiles(jsRoot, "*.js", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
        {
            var relativePath = NormalizePath(Path.GetRelativePath(_repoRoot, file));
            if (relativePath.StartsWith("MathAnalysisAI.Server/wwwroot/js/api/", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (!line.Contains("/api/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (relativePath.EndsWith("/api.js", StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add(new FreezeViolation(
                    "FRONTEND_DIRECT_API_ROUTE_REFERENCE",
                    relativePath,
                    index + 1,
                    "frontend",
                    BlockingCategory,
                    "Frontend files outside the centralized api layer must not reference /api/* routes directly.",
                    "Route backend calls through wwwroot/js/api/* modules and endpoint contracts only."));
            }
        }
    }

    private void CheckContractFreezeBaseline(string baselinePath, List<FreezeViolation> violations)
    {
        var current = BuildContractFreezeSnapshot();
        if (!File.Exists(baselinePath))
        {
            violations.Add(new FreezeViolation(
                "CONTRACT_FREEZE_BASELINE_MISSING",
                NormalizePath(Path.GetRelativePath(_repoRoot, baselinePath)),
                0,
                "tooling",
                BlockingCategory,
                "Contract freeze baseline file is missing.",
                "Generate or restore Architecture/Freeze/contract-freeze-baseline.json before running strict freeze checks."));
            return;
        }

        var baseline = System.Text.Json.JsonSerializer.Deserialize<ContractFreezeSnapshot>(File.ReadAllText(baselinePath))
            ?? new ContractFreezeSnapshot();

        foreach (var currentEntry in current.Files)
        {
            if (!baseline.Files.TryGetValue(currentEntry.Key, out var baselineHash))
            {
                violations.Add(new FreezeViolation(
                    "CONTRACT_FREEZE_NEW_FILE",
                    currentEntry.Key,
                    0,
                    "tooling",
                    BlockingCategory,
                    "A frozen DTO or contract file was added after the baseline was captured.",
                    "Review the new file intentionally and refresh the contract freeze baseline if the change is approved."));
                continue;
            }

            if (!string.Equals(currentEntry.Value, baselineHash, StringComparison.Ordinal))
            {
                violations.Add(new FreezeViolation(
                    "CONTRACT_FREEZE_HASH_CHANGED",
                    currentEntry.Key,
                    0,
                    "tooling",
                    BlockingCategory,
                    "A frozen DTO or contract file changed compared with the architecture freeze baseline.",
                    "Changes to DTOs or contract JSON require an intentional baseline refresh after review."));
            }
        }

        foreach (var baselineEntry in baseline.Files.Keys.Except(current.Files.Keys, StringComparer.Ordinal))
        {
            violations.Add(new FreezeViolation(
                "CONTRACT_FREEZE_FILE_REMOVED",
                baselineEntry,
                0,
                "tooling",
                BlockingCategory,
                "A frozen DTO or contract file from the baseline is no longer present.",
                "Restore the file or intentionally refresh the baseline after confirming the removal."));
        }
    }

    private ContractFreezeSnapshot BuildContractFreezeSnapshot()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in Directory.GetFiles(Path.Combine(_repoRoot, "MathAnalysisAI.Server", "DTOs"), "*.cs", SearchOption.AllDirectories)
                     .OrderBy(x => x, StringComparer.Ordinal))
        {
            files[NormalizePath(Path.GetRelativePath(_repoRoot, file))] = ComputeSha256(file);
        }

        foreach (var file in Directory.GetFiles(Path.Combine(_repoRoot, "Contracts"), "*.json", SearchOption.AllDirectories)
                     .OrderBy(x => x, StringComparer.Ordinal))
        {
            files[NormalizePath(Path.GetRelativePath(_repoRoot, file))] = ComputeSha256(file);
        }

        return new ContractFreezeSnapshot
        {
            SchemaVersion = 1,
            Files = files
        };
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private IEnumerable<string> EnumerateServerSourceFiles()
    {
        var serverRoot = Path.Combine(_repoRoot, "MathAnalysisAI.Server");
        return Directory.GetFiles(serverRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private bool IsDataAccessFile(string relativePath)
    {
        if (relativePath.StartsWith("MathAnalysisAI.Server/Data/", StringComparison.Ordinal))
        {
            return true;
        }

        if (relativePath.StartsWith("MathAnalysisAI.Server/Migrations/", StringComparison.Ordinal))
        {
            return true;
        }

        if (relativePath.StartsWith("MathAnalysisAI.Server/Models/", StringComparison.Ordinal))
        {
            return true;
        }

        if (relativePath.Contains("Persistence", StringComparison.Ordinal) && relativePath.StartsWith("MathAnalysisAI.Server/Services/", StringComparison.Ordinal))
        {
            return true;
        }

        if (relativePath.EndsWith("DbHealthCheck.cs", StringComparison.Ordinal) || relativePath.EndsWith("ApiExceptionClassifier.cs", StringComparison.Ordinal))
        {
            return true;
        }

        return DataAccessConcreteFiles.Contains(relativePath, StringComparer.Ordinal);
    }

    private string InferLayer(string relativePath)
    {
        if (relativePath.StartsWith("MathAnalysisAI.Server/Controllers/", StringComparison.Ordinal))
        {
            return "backend-api";
        }

        if (relativePath == "MathAnalysisAI.Server/Program.cs")
        {
            return "composition-root";
        }

        if (relativePath.StartsWith("MathAnalysisAI.Server/Intelligence/", StringComparison.Ordinal))
        {
            return "intelligence";
        }

        if (IsDataAccessFile(relativePath))
        {
            return "data-access";
        }

        if (relativePath.StartsWith("MathAnalysisAI.Server/wwwroot/", StringComparison.Ordinal))
        {
            return "frontend";
        }

        return "backend-application";
    }

    private static ArchitectureViolationSummary BuildSummary(IReadOnlyCollection<FreezeViolation> violations)
    {
        return new ArchitectureViolationSummary
        {
            TotalViolations = violations.Count,
            BlockingViolations = violations.Count(v => string.Equals(v.Category, BlockingCategory, StringComparison.Ordinal)),
            ObservationViolations = violations.Count(v => string.Equals(v.Category, ObservationCategory, StringComparison.Ordinal)),
            ViolationsByRule = violations
                .GroupBy(v => v.RuleId, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal)
        };
    }

    private static void AddMatches(
        List<FreezeViolation> target,
        string relativePath,
        IReadOnlyList<string> lines,
        string layer,
        string ruleId,
        string message,
        string pattern,
        string category,
        string recommendation)
    {
        var regex = new Regex(pattern, RegexOptions.Compiled);
        for (var index = 0; index < lines.Count; index++)
        {
            if (!regex.IsMatch(lines[index]))
            {
                continue;
            }

            target.Add(new FreezeViolation(
                ruleId,
                relativePath,
                index + 1,
                layer,
                category,
                message,
                recommendation));
        }
    }

    private static string NormalizePath(string value)
        => value.Replace('\\', '/');
}

public sealed record ArchitectureDriftDetectionResult(
    ArchitectureDriftReport DriftReport,
    IntelligenceIsolationReport IntelligenceIsolationReport);

public sealed class ArchitectureDriftReport
{
    public string RepoRoot { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; }
    public ArchitectureViolationSummary Summary { get; init; } = new();
    public List<FreezeViolation> Violations { get; init; } = new();
}

public sealed class IntelligenceIsolationReport
{
    public string RepoRoot { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; }
    public ArchitectureViolationSummary Summary { get; init; } = new();
    public List<FreezeViolation> Violations { get; init; } = new();
}

public sealed class ArchitectureViolationSummary
{
    public int TotalViolations { get; init; }
    public int BlockingViolations { get; init; }
    public int ObservationViolations { get; init; }
    public Dictionary<string, int> ViolationsByRule { get; init; } = new(StringComparer.Ordinal);
}

public sealed record FreezeViolation(
    string RuleId,
    string File,
    int Line,
    string Layer,
    string Category,
    string Message,
    string Recommendation);

public sealed class ContractFreezeSnapshot
{
    public int SchemaVersion { get; init; } = 1;
    public Dictionary<string, string> Files { get; init; } = new(StringComparer.Ordinal);
}
