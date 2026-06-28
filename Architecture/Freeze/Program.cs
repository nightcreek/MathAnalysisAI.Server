using System.Text.Json;
using Architecture.Freeze;

var repoRoot = ResolveRepoRoot();
var strict = args.Any(arg => string.Equals(arg, "--strict", StringComparison.OrdinalIgnoreCase));
var refreshBaseline = args.Any(arg => string.Equals(arg, "--refresh-baseline", StringComparison.OrdinalIgnoreCase));
var positional = args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToList();

var driftOutputPath = positional.Count > 0
    ? Path.GetFullPath(positional[0], Directory.GetCurrentDirectory())
    : Path.Combine(repoRoot, "architecture-drift-report.json");
var graphOutputPath = positional.Count > 1
    ? Path.GetFullPath(positional[1], Directory.GetCurrentDirectory())
    : Path.Combine(repoRoot, "dependency-graph.json");
var intelligenceOutputPath = positional.Count > 2
    ? Path.GetFullPath(positional[2], Directory.GetCurrentDirectory())
    : Path.Combine(repoRoot, "intelligence-isolation-report.json");
var baselinePath = positional.Count > 3
    ? Path.GetFullPath(positional[3], Directory.GetCurrentDirectory())
    : Path.Combine(repoRoot, "Architecture", "Freeze", "contract-freeze-baseline.json");

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true
};

var driftDetector = new ArchitectureDriftDetector(repoRoot);
if (refreshBaseline)
{
    driftDetector.WriteContractFreezeBaseline(baselinePath);
    Console.WriteLine($"Contract freeze baseline refreshed: {baselinePath}");
}

var driftResult = driftDetector.Run(baselinePath);
WriteJson(driftOutputPath, driftResult.DriftReport, jsonOptions);
WriteJson(intelligenceOutputPath, driftResult.IntelligenceIsolationReport, jsonOptions);

var graphValidator = new DependencyGraphValidator(repoRoot);
var dependencyGraph = graphValidator.Validate();
WriteJson(graphOutputPath, dependencyGraph, jsonOptions);

Console.WriteLine($"Architecture drift report: {driftOutputPath}");
Console.WriteLine($"Dependency graph report: {graphOutputPath}");
Console.WriteLine($"Intelligence isolation report: {intelligenceOutputPath}");
Console.WriteLine($"Drift violations: {driftResult.DriftReport.Summary.TotalViolations}");
Console.WriteLine($"Drift blocking violations: {driftResult.DriftReport.Summary.BlockingViolations}");
Console.WriteLine($"Dependency graph violations: {dependencyGraph.Summary.TotalViolations}");
Console.WriteLine($"Dependency graph blocking violations: {dependencyGraph.Summary.BlockingViolations}");
Console.WriteLine($"Intelligence isolation violations: {driftResult.IntelligenceIsolationReport.Summary.TotalViolations}");

return strict && (driftResult.DriftReport.Summary.BlockingViolations > 0 || dependencyGraph.Summary.BlockingViolations > 0)
    ? 1
    : 0;

static void WriteJson<T>(string path, T value, JsonSerializerOptions options)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, JsonSerializer.Serialize(value, options));
}

static string ResolveRepoRoot()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current != null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, "MathAnalysisAI.Server"))
            && Directory.Exists(Path.Combine(current.FullName, "Architecture")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Could not resolve repository root.");
}
