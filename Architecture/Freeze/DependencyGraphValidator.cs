using System.Text.RegularExpressions;

namespace Architecture.Freeze;

public sealed class DependencyGraphValidator
{
    private const string BlockingCategory = "blocking";

    private static readonly Regex UsingRegex = new(@"^[ \t]*using\s+(?<ns>[A-Za-z0-9_\.]+)\s*;", RegexOptions.Multiline | RegexOptions.Compiled);

    private readonly string _repoRoot;

    public DependencyGraphValidator(string repoRoot)
    {
        _repoRoot = repoRoot;
    }

    public DependencyGraphReport Validate()
    {
        var nodes = DiscoverNodes().ToList();
        var edges = BuildEdges(nodes).ToList();
        var violations = ValidateEdges(edges).ToList();

        return new DependencyGraphReport
        {
            RepoRoot = _repoRoot.Replace('\\', '/'),
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = new DependencyGraphSummary
            {
                TotalNodes = nodes.Count,
                TotalEdges = edges.Count,
                TotalViolations = violations.Count,
                BlockingViolations = violations.Count(v => string.Equals(v.Category, BlockingCategory, StringComparison.Ordinal))
            },
            Nodes = nodes,
            Edges = edges,
            Violations = violations
                .OrderBy(v => v.Source, StringComparer.Ordinal)
                .ThenBy(v => v.Target, StringComparer.Ordinal)
                .ThenBy(v => v.RuleId, StringComparer.Ordinal)
                .ToList()
        };
    }

    private IEnumerable<DependencyNode> DiscoverNodes()
    {
        var serverRoot = Path.Combine(_repoRoot, "MathAnalysisAI.Server");
        foreach (var file in Directory.GetFiles(serverRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            var relativePath = NormalizePath(Path.GetRelativePath(_repoRoot, file));
            var content = File.ReadAllText(file);
            yield return new DependencyNode(
                relativePath,
                InferLayer(relativePath),
                DiscoverUsings(content));
        }
    }

    private static IEnumerable<string> DiscoverUsings(string content)
    {
        return UsingRegex.Matches(content)
            .Select(match => match.Groups["ns"].Value)
            .Where(ns => !string.IsNullOrWhiteSpace(ns))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ns => ns, StringComparer.Ordinal);
    }

    private IEnumerable<DependencyEdge> BuildEdges(IReadOnlyCollection<DependencyNode> nodes)
    {
        foreach (var node in nodes)
        {
            foreach (var import in node.Usings)
            {
                var targetLayer = MapNamespaceToLayer(import);
                if (targetLayer == null)
                {
                    continue;
                }

                yield return new DependencyEdge(node.Path, node.Layer, import, targetLayer);
            }
        }
    }

    private IEnumerable<DependencyViolation> ValidateEdges(IReadOnlyCollection<DependencyEdge> edges)
    {
        foreach (var edge in edges)
        {
            if (edge.SourceLayer == "backend-api" && edge.TargetLayer == "intelligence-implementation")
            {
                yield return new DependencyViolation(
                    "BACKEND_API_TO_INTELLIGENCE_IMPLEMENTATION",
                    edge.Source,
                    edge.Import,
                    BlockingCategory,
                    "Controllers and composition root code must not reference Intelligence implementations directly.",
                    "Depend on backend application services or Intelligence interfaces instead.");
            }

            if (edge.SourceLayer == "backend-api" && edge.TargetLayer == "data-access")
            {
                yield return new DependencyViolation(
                    "BACKEND_API_TO_DATA_ACCESS",
                    edge.Source,
                    edge.Import,
                    BlockingCategory,
                    "Backend API layer must not reference Data Access layer directly.",
                    "Move persistence access behind an application service.");
            }

            if (edge.SourceLayer == "backend-application" && edge.TargetLayer == "data-access-concrete")
            {
                yield return new DependencyViolation(
                    "BACKEND_APPLICATION_TO_DATA_ACCESS_CONCRETE",
                    edge.Source,
                    edge.Import,
                    BlockingCategory,
                    "Application services must not depend on concrete Data Access implementations.",
                    "Depend on persistence-facing interfaces instead of Data layer concrete types.");
            }

            if (edge.SourceLayer == "backend-application" && edge.TargetLayer == "intelligence-implementation")
            {
                yield return new DependencyViolation(
                    "BACKEND_APPLICATION_TO_INTELLIGENCE_IMPLEMENTATION",
                    edge.Source,
                    edge.Import,
                    BlockingCategory,
                    "Application services must depend on Intelligence interfaces, not concrete implementations.",
                    "Inject Intelligence through interfaces defined under MathAnalysisAI.Server.Intelligence.Interfaces.");
            }

            if (edge.SourceLayer == "intelligence" && edge.TargetLayer is "backend-api" or "data-access" or "data-access-concrete")
            {
                yield return new DependencyViolation(
                    "INTELLIGENCE_LAYER_ISOLATION_BREACH",
                    edge.Source,
                    edge.Import,
                    BlockingCategory,
                    "Intelligence layer must remain isolated from Controllers and Data Access implementations.",
                    "Keep Intelligence dependencies limited to DTOs, primitives, and interface contracts.");
            }
        }
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

        if (relativePath.StartsWith("MathAnalysisAI.Server/Data/", StringComparison.Ordinal)
            || relativePath.StartsWith("MathAnalysisAI.Server/Migrations/", StringComparison.Ordinal)
            || relativePath.StartsWith("MathAnalysisAI.Server/Models/", StringComparison.Ordinal)
            || relativePath.Contains("Persistence", StringComparison.Ordinal)
            || relativePath.EndsWith("DbHealthCheck.cs", StringComparison.Ordinal)
            || relativePath.EndsWith("ApiExceptionClassifier.cs", StringComparison.Ordinal)
            || relativePath.EndsWith("LLMGateway.cs", StringComparison.Ordinal)
            || relativePath.EndsWith("ProblemStructuringService.cs", StringComparison.Ordinal))
        {
            return "data-access";
        }

        return "backend-application";
    }

    private static string? MapNamespaceToLayer(string importNamespace)
    {
        if (importNamespace.StartsWith("MathAnalysisAI.Server.Controllers", StringComparison.Ordinal))
        {
            return "backend-api";
        }

        if (importNamespace.StartsWith("MathAnalysisAI.Server.Intelligence.Interfaces", StringComparison.Ordinal))
        {
            return "intelligence-interface";
        }

        if (importNamespace.StartsWith("MathAnalysisAI.Server.Intelligence.", StringComparison.Ordinal))
        {
            return "intelligence-implementation";
        }

        if (importNamespace.StartsWith("MathAnalysisAI.Server.Data.", StringComparison.Ordinal))
        {
            return "data-access-concrete";
        }

        if (string.Equals(importNamespace, "Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
        {
            return "data-access-concrete";
        }

        return null;
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');
}

public sealed class DependencyGraphReport
{
    public string RepoRoot { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; }
    public DependencyGraphSummary Summary { get; init; } = new();
    public List<DependencyNode> Nodes { get; init; } = new();
    public List<DependencyEdge> Edges { get; init; } = new();
    public List<DependencyViolation> Violations { get; init; } = new();
}

public sealed class DependencyGraphSummary
{
    public int TotalNodes { get; init; }
    public int TotalEdges { get; init; }
    public int TotalViolations { get; init; }
    public int BlockingViolations { get; init; }
}

public sealed class DependencyNode
{
    public DependencyNode(string path, string layer, IEnumerable<string> usings)
    {
        Path = path;
        Layer = layer;
        Usings = usings.ToList();
    }

    public string Path { get; }
    public string Layer { get; }
    public List<string> Usings { get; }
}

public sealed record DependencyEdge(
    string Source,
    string SourceLayer,
    string Import,
    string TargetLayer);

public sealed record DependencyViolation(
    string RuleId,
    string Source,
    string Target,
    string Category,
    string Message,
    string Recommendation);
