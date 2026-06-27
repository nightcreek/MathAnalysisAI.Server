using Architecture.ModuleContracts;

var repoRoot = ResolveRepoRoot();
var registryPath = args.Length > 0 && !string.Equals(args[0], "--strict", StringComparison.OrdinalIgnoreCase)
    ? Path.GetFullPath(args[0], Directory.GetCurrentDirectory())
    : Path.Combine(repoRoot, "Architecture", "ModuleContracts", "module-contracts.json");
var outputPath = args.Length > 1 && !string.Equals(args[1], "--strict", StringComparison.OrdinalIgnoreCase)
    ? Path.GetFullPath(args[1], Directory.GetCurrentDirectory())
    : Path.Combine(repoRoot, "module-contract-violations.json");
var strict = args.Any(arg => string.Equals(arg, "--strict", StringComparison.OrdinalIgnoreCase));

var validator = new ModuleContractValidator(repoRoot);
var result = validator.Validate(registryPath, outputPath, strict);

Console.WriteLine($"Module contract registry: {result.RegistryPath}");
Console.WriteLine($"Module contract violations report: {result.OutputPath}");
Console.WriteLine($"Modules loaded: {result.ModuleCount}");
Console.WriteLine($"Violations found: {result.ViolationCount}");
Console.WriteLine($"Blocking violations: {result.BlockingViolationCount}");

return strict && result.BlockingViolationCount > 0 ? 1 : 0;

static string ResolveRepoRoot()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());

    while (current != null)
    {
        var serverDir = Path.Combine(current.FullName, "MathAnalysisAI.Server");
        if (Directory.Exists(serverDir))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Could not resolve repository root.");
}
