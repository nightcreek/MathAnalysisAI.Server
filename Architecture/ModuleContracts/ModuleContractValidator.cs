using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Architecture.ModuleContracts;

public sealed class ModuleContractValidator
{
    private const string BlockingCategory = "blocking";
    private const string ObservationCategory = "observation";
    private const string AcceptedLegacyCategory = "acceptedLegacy";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] StepClassNames =
    [
        "UAOBuilderStep",
        "OCRStep",
        "LLMStep",
        "EvaluationStep",
        "PersistenceStep"
    ];

    private static readonly HashSet<string> AcceptedLegacyEfCoreFiles = new(StringComparer.Ordinal);

    private readonly string _repoRoot;
    private readonly string _sourceRoot;

    public ModuleContractValidator(string repoRoot)
    {
        _repoRoot = repoRoot;
        _sourceRoot = Path.Combine(_repoRoot, "MathAnalysisAI.Server");
    }

    public ModuleContractValidationResult Validate(string registryPath, string outputPath, bool strict)
    {
        if (!File.Exists(registryPath))
        {
            throw new FileNotFoundException("Module contracts registry not found.", registryPath);
        }

        var registry = JsonSerializer.Deserialize<ModuleContractRegistry>(
            File.ReadAllText(registryPath),
            JsonOptions) ?? new ModuleContractRegistry();

        var files = EnumerateSourceFiles().ToList();
        var violations = new List<ModuleContractViolation>();

        foreach (var file in files)
        {
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(_repoRoot, file));
            var lines = File.ReadAllLines(file);
            var content = string.Join('\n', lines);
            var moduleName = InferModuleName(relativePath, registry.Modules);

            CheckControllerDbContextLeak(relativePath, lines, moduleName, violations);
            CheckControllerStepImports(relativePath, lines, moduleName, violations);
            CheckEfCoreImports(relativePath, lines, content, moduleName, violations);
            CheckAnalysisConcreteProviderUsage(relativePath, lines, moduleName, violations);
            CheckUaoDtoImports(relativePath, lines, moduleName, violations);
            CheckDomainImports(relativePath, lines, moduleName, violations);
            CheckStepReferencesOutsidePipeline(relativePath, content, lines, moduleName, violations);
        }

        var report = new ModuleContractViolationReport
        {
            RegistryPath = Path.GetFullPath(registryPath),
            OutputMode = strict ? "strict" : "observation",
            SourceRoot = _sourceRoot.Replace('\\', '/'),
            Summary = new ModuleContractViolationSummary
            {
                ModuleCount = registry.Modules.Count,
                FilesScanned = files.Count,
                ViolationCount = violations.Count,
                BlockingViolationCount = violations.Count(v => string.Equals(v.Category, BlockingCategory, StringComparison.Ordinal)),
                ObservationViolationCount = violations.Count(v => string.Equals(v.Category, ObservationCategory, StringComparison.Ordinal)),
                AcceptedLegacyViolationCount = violations.Count(v => string.Equals(v.Category, AcceptedLegacyCategory, StringComparison.Ordinal)),
                ViolationsByRule = violations
                    .GroupBy(v => v.Rule, StringComparer.Ordinal)
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
                ViolationsByCategory = violations
                    .GroupBy(v => v.Category, StringComparer.Ordinal)
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal)
            },
            Violations = violations
                .OrderBy(v => v.File, StringComparer.Ordinal)
                .ThenBy(v => v.Line)
                .ThenBy(v => v.Rule, StringComparer.Ordinal)
                .ToList()
        };

        EnsureDirectory(outputPath);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, JsonOptions));

        return new ModuleContractValidationResult
        {
            RegistryPath = Path.GetFullPath(registryPath),
            OutputPath = Path.GetFullPath(outputPath),
            ModuleCount = registry.Modules.Count,
            ViolationCount = violations.Count,
            BlockingViolationCount = violations.Count(v => string.Equals(v.Category, BlockingCategory, StringComparison.Ordinal))
        };
    }

    private void CheckControllerDbContextLeak(
        string relativePath,
        IReadOnlyList<string> lines,
        string moduleName,
        List<ModuleContractViolation> violations)
    {
        if (!relativePath.StartsWith("MathAnalysisAI.Server/Controllers/", StringComparison.Ordinal))
        {
            return;
        }

        AddMatches(
            violations,
            relativePath,
            lines,
            moduleName,
            "CONTROLLER_DB_CONTEXT_REFERENCE",
            "Controllers must not reference ApplicationDbContext directly.",
            @"\b(ApplicationDbContext|DbContext)\b",
            BlockingCategory,
            "Inject a public service or persistence-facing interface instead of DbContext into the controller.");

        AddMatches(
            violations,
            relativePath,
            lines,
            moduleName,
            "CONTROLLER_DB_CONTEXT_REFERENCE",
            "Controllers must not import EntityFrameworkCore.",
            @"^[ \t]*using\s+Microsoft\.EntityFrameworkCore\b",
            BlockingCategory,
            "Remove the EF Core import and move persistence access behind an application service.");
    }

    private void CheckControllerStepImports(
        string relativePath,
        IReadOnlyList<string> lines,
        string moduleName,
        List<ModuleContractViolation> violations)
    {
        if (!relativePath.StartsWith("MathAnalysisAI.Server/Controllers/", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var stepClass in StepClassNames)
        {
            AddMatches(
                violations,
                relativePath,
                lines,
                moduleName,
                "CONTROLLER_PIPELINE_STEP_REFERENCE",
                $"Controllers must not reference internal pipeline step {stepClass}.",
                $@"\b{Regex.Escape(stepClass)}\b",
                BlockingCategory,
                "Controllers should depend on IAnalysisService only, never internal pipeline steps.");
        }
    }

    private void CheckEfCoreImports(
        string relativePath,
        IReadOnlyList<string> lines,
        string content,
        string moduleName,
        List<ModuleContractViolation> violations)
    {
        if (IsAllowedEfCoreFile(relativePath, content))
        {
            return;
        }

        var category = AcceptedLegacyEfCoreFiles.Contains(relativePath)
            ? AcceptedLegacyCategory
            : ObservationCategory;
        var recommendation = AcceptedLegacyEfCoreFiles.Contains(relativePath)
            ? "Keep this finding visible until the service is migrated behind a narrower persistence interface."
            : "Move EF Core usage behind PersistenceModule or remove the import if it is unused.";

        AddMatches(
            violations,
            relativePath,
            lines,
            moduleName,
            "NON_PERSISTENCE_EF_CORE_IMPORT",
            "Microsoft.EntityFrameworkCore should not be imported outside persistence/data/migrations/startup composition.",
            @"^[ \t]*using\s+Microsoft\.EntityFrameworkCore\b",
            category,
            recommendation);
    }

    private void CheckAnalysisConcreteProviderUsage(
        string relativePath,
        IReadOnlyList<string> lines,
        string moduleName,
        List<ModuleContractViolation> violations)
    {
        if (!relativePath.StartsWith("MathAnalysisAI.Server/Services/Analysis/", StringComparison.Ordinal) &&
            !relativePath.StartsWith("MathAnalysisAI.Server/Services/Orchestration/", StringComparison.Ordinal))
        {
            return;
        }

        AddMatches(
            violations,
            relativePath,
            lines,
            moduleName,
            "ANALYSIS_CONCRETE_PROVIDER_REFERENCE",
            "Analysis module must not depend on concrete LLM provider implementations.",
            @"\bLLMGateway\b",
            BlockingCategory,
            "Depend on ILLMModule or ILLMService instead of a concrete provider implementation.");
        AddMatches(
            violations,
            relativePath,
            lines,
            moduleName,
            "ANALYSIS_CONCRETE_PROVIDER_REFERENCE",
            "Analysis module must not depend on concrete OCR provider implementations.",
            @"\bLiteLLMPhotoSolutionOcrProvider\b",
            BlockingCategory,
            "Depend on IOCRService instead of a concrete OCR provider implementation.");
    }

    private void CheckUaoDtoImports(
        string relativePath,
        IReadOnlyList<string> lines,
        string moduleName,
        List<ModuleContractViolation> violations)
    {
        if (!relativePath.StartsWith("MathAnalysisAI.Server/Services/Analysis/UAO/", StringComparison.Ordinal))
        {
            return;
        }

        AddMatches(
            violations,
            relativePath,
            lines,
            moduleName,
            "UAO_DTO_IMPORT",
            "UAO files must not import DTO namespaces.",
            @"^[ \t]*using\s+MathAnalysisAI\.Server\.DTOs\.",
            BlockingCategory,
            "Move DTO-to-UAO mapping to an application-layer mapper and keep UAO semantic-only.");
    }

    private void CheckDomainImports(
        string relativePath,
        IReadOnlyList<string> lines,
        string moduleName,
        List<ModuleContractViolation> violations)
    {
        if (!relativePath.StartsWith("MathAnalysisAI.Server/Services/Analysis/Domain/", StringComparison.Ordinal))
        {
            return;
        }

        AddMatches(
            violations,
            relativePath,
            lines,
            moduleName,
            "DOMAIN_HTTP_OR_DTO_IMPORT",
            "Analysis domain files must not import DTO namespaces.",
            @"^[ \t]*using\s+MathAnalysisAI\.Server\.DTOs\.",
            BlockingCategory,
            "Remove DTO imports from domain code and keep translation at module boundaries.");
        AddMatches(
            violations,
            relativePath,
            lines,
            moduleName,
            "DOMAIN_HTTP_OR_DTO_IMPORT",
            "Analysis domain files must not import ASP.NET namespaces.",
            @"^[ \t]*using\s+Microsoft\.AspNetCore\.",
            BlockingCategory,
            "Remove ASP.NET dependencies from domain code and keep HTTP concerns in controllers/application services.");
    }

    private void CheckStepReferencesOutsidePipeline(
        string relativePath,
        string content,
        IReadOnlyList<string> lines,
        string moduleName,
        List<ModuleContractViolation> violations)
    {
        if (relativePath.StartsWith("MathAnalysisAI.Server/Services/Orchestration/", StringComparison.Ordinal) ||
            string.Equals(relativePath, "MathAnalysisAI.Server/Program.cs", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var stepClass in StepClassNames)
        {
            if (!Regex.IsMatch(content, $@"\b{Regex.Escape(stepClass)}\b"))
            {
                continue;
            }

            AddMatches(
                violations,
                relativePath,
                lines,
                moduleName,
                "ANALYSIS_STEP_EXTERNAL_REFERENCE",
                $"Files outside the analysis pipeline must not reference internal step class {stepClass}.",
                $@"\b{Regex.Escape(stepClass)}\b",
                BlockingCategory,
                "Route analysis execution through IAnalysisPipeline rather than referencing step classes directly.");
        }
    }

    private void AddMatches(
        List<ModuleContractViolation> violations,
        string relativePath,
        IReadOnlyList<string> lines,
        string moduleName,
        string rule,
        string message,
        string pattern,
        string category,
        string recommendation)
    {
        var regex = new Regex(pattern, RegexOptions.Compiled);
        for (var i = 0; i < lines.Count; i++)
        {
            if (!regex.IsMatch(lines[i]))
            {
                continue;
            }

            violations.Add(new ModuleContractViolation
            {
                Rule = rule,
                RuleId = rule,
                Severity = category,
                Category = category,
                ModuleName = moduleName,
                File = relativePath,
                Line = i + 1,
                Message = message,
                Snippet = lines[i].Trim(),
                Recommendation = recommendation
            });
        }
    }

    private IEnumerable<string> EnumerateSourceFiles()
    {
        return Directory.EnumerateFiles(_sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal);
    }

    private static bool IsAllowedEfCoreFile(string relativePath, string content)
    {
        return relativePath.StartsWith("MathAnalysisAI.Server/Data/", StringComparison.Ordinal) ||
               relativePath.StartsWith("MathAnalysisAI.Server/Migrations/", StringComparison.Ordinal) ||
               relativePath.StartsWith("MathAnalysisAI.Server/Models/", StringComparison.Ordinal) &&
               content.Contains("[Index(", StringComparison.Ordinal) ||
               relativePath.StartsWith("MathAnalysisAI.Server/Services/Analysis/Persistence/", StringComparison.Ordinal) ||
               string.Equals(relativePath, "MathAnalysisAI.Server/Services/ExceptionHandling/ApiExceptionClassifier.cs", StringComparison.Ordinal) ||
               string.Equals(relativePath, "MathAnalysisAI.Server/Services/ExceptionHandling/DbHealthCheck.cs", StringComparison.Ordinal) ||
               string.Equals(relativePath, "MathAnalysisAI.Server/Program.cs", StringComparison.Ordinal);
    }

    private static string InferModuleName(string relativePath, IReadOnlyList<ModuleContract> modules)
    {
        foreach (var module in modules)
        {
            foreach (var layer in module.AllowedLayers.OrderByDescending(x => x.Length))
            {
                var normalizedLayer = layer.Replace('\\', '/').TrimStart('/');
                var prefix = normalizedLayer.StartsWith("MathAnalysisAI.Server/", StringComparison.Ordinal)
                    ? normalizedLayer
                    : $"MathAnalysisAI.Server/{normalizedLayer}";

                if (relativePath.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return module.ModuleName;
                }
            }
        }

        if (relativePath.StartsWith("MathAnalysisAI.Server/Controllers/", StringComparison.Ordinal))
        {
            return "Controllers";
        }

        return "Unmapped";
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}

public sealed class ModuleContractValidationResult
{
    public required string RegistryPath { get; init; }
    public required string OutputPath { get; init; }
    public int ModuleCount { get; init; }
    public int ViolationCount { get; init; }
    public int BlockingViolationCount { get; init; }
}

public sealed class ModuleContractRegistry
{
    public List<ModuleContract> Modules { get; init; } = new();
}

public sealed class ModuleContract
{
    public string ModuleName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<string> PublicInterfaces { get; init; } = new();
    public List<string> AllowedDependencies { get; init; } = new();
    public List<string> ForbiddenDependencies { get; init; } = new();
    public List<string> AllowedLayers { get; init; } = new();
    public List<string> Notes { get; init; } = new();
}

public sealed class ModuleContractViolationReport
{
    public string RegistryPath { get; init; } = string.Empty;
    public string SourceRoot { get; init; } = string.Empty;
    public string OutputMode { get; init; } = string.Empty;
    public ModuleContractViolationSummary Summary { get; init; } = new();
    public List<ModuleContractViolation> Violations { get; init; } = new();
}

public sealed class ModuleContractViolationSummary
{
    public int ModuleCount { get; init; }
    public int FilesScanned { get; init; }
    public int ViolationCount { get; init; }
    public int BlockingViolationCount { get; init; }
    public int ObservationViolationCount { get; init; }
    public int AcceptedLegacyViolationCount { get; init; }
    public Dictionary<string, int> ViolationsByRule { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> ViolationsByCategory { get; init; } = new(StringComparer.Ordinal);
}

public sealed class ModuleContractViolation
{
    public string Rule { get; init; } = string.Empty;
    public string RuleId { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public string File { get; init; } = string.Empty;
    public int Line { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
}
