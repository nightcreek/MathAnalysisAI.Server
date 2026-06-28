using System.Text.Json;
using System.Text.RegularExpressions;

public sealed class ContractValidator
{
    private static readonly Regex RouteRegex = new(@"\[Route\(""(?<value>[^""]+)""\)\]", RegexOptions.Compiled);
    private static readonly Regex HttpMethodRegex = new(@"\[(?<verb>HttpGet|HttpPost|HttpPut|HttpDelete)(?:\(""(?<value>[^""]*)""\))?\]", RegexOptions.Compiled);
    private static readonly Regex ClassRegex = new(@"class\s+(?<name>[A-Za-z0-9_]+Controller)\b", RegexOptions.Compiled);
    private static readonly Regex ParameterRegex = new(@"\{[^}]+\}", RegexOptions.Compiled);

    public ContractValidationResult Validate(string repoRoot, string contractsPath)
    {
        var controllerEndpoints = DiscoverControllerEndpoints(Path.Combine(repoRoot, "MathAnalysisAI.Server", "Controllers"));
        var contractEndpoints = DiscoverContractEndpoints(contractsPath);

        var controllerSet = controllerEndpoints
            .Select(x => EndpointSignature(x.Method, x.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var contractSet = contractEndpoints
            .Select(x => EndpointSignature(x.Method, x.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var mismatches = new List<ContractMismatch>();

        foreach (var controllerEndpoint in controllerEndpoints)
        {
            var signature = EndpointSignature(controllerEndpoint.Method, controllerEndpoint.Path);
            if (!contractSet.Contains(signature))
            {
                mismatches.Add(new ContractMismatch(
                    "controller_missing_in_contract",
                    controllerEndpoint.Method,
                    controllerEndpoint.Path,
                    $"Controller endpoint '{controllerEndpoint.Method} {controllerEndpoint.Path}' is not described in Contracts/api.contract.json.",
                    controllerEndpoint.Source));
            }
        }

        foreach (var contractEndpoint in contractEndpoints)
        {
          var signature = EndpointSignature(contractEndpoint.Method, contractEndpoint.Path);
          if (!controllerSet.Contains(signature))
          {
              mismatches.Add(new ContractMismatch(
                  "contract_missing_in_controller",
                  contractEndpoint.Method,
                  contractEndpoint.Path,
                  $"Contract endpoint '{contractEndpoint.Method} {contractEndpoint.Path}' was not found in controllers.",
                  contractEndpoint.Source));
          }
        }

        return new ContractValidationResult(
            ContractsPath: contractsPath,
            GeneratedAtUtc: DateTime.UtcNow,
            ControllerEndpoints: controllerEndpoints,
            ContractEndpoints: contractEndpoints,
            Mismatches: mismatches);
    }

    private static List<DiscoveredEndpoint> DiscoverControllerEndpoints(string controllersDirectory)
    {
        var result = new List<DiscoveredEndpoint>();
        if (!Directory.Exists(controllersDirectory))
        {
            return result;
        }

        foreach (var file in Directory.GetFiles(controllersDirectory, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(x => x))
        {
            var text = File.ReadAllText(file);
            var className = ClassRegex.Match(text).Groups["name"].Value;
            var controllerToken = className.EndsWith("Controller", StringComparison.Ordinal)
                ? className[..^"Controller".Length].ToLowerInvariant()
                : className.ToLowerInvariant();

            var classRouteMatch = RouteRegex.Match(text);
            var classRoute = classRouteMatch.Success ? classRouteMatch.Groups["value"].Value : string.Empty;
            classRoute = NormalizeRouteTemplate(classRoute.Replace("[controller]", controllerToken, StringComparison.OrdinalIgnoreCase));

            foreach (Match match in HttpMethodRegex.Matches(text))
            {
                var verb = match.Groups["verb"].Value.Replace("Http", string.Empty).ToUpperInvariant();
                var methodRoute = NormalizeRouteTemplate(match.Groups["value"].Success ? match.Groups["value"].Value : string.Empty);
                var fullPath = NormalizeFullPath(classRoute, methodRoute);
                if (string.IsNullOrWhiteSpace(fullPath))
                {
                    continue;
                }

                result.Add(new DiscoveredEndpoint(verb, fullPath, Path.GetRelativePath(controllersDirectory, file)));
            }
        }

        return result;
    }

    private static List<DiscoveredEndpoint> DiscoverContractEndpoints(string contractsPath)
    {
        var result = new List<DiscoveredEndpoint>();
        using var document = JsonDocument.Parse(File.ReadAllText(contractsPath));
        if (!document.RootElement.TryGetProperty("modules", out var modules))
        {
            return result;
        }

        foreach (var module in modules.EnumerateObject())
        {
            if (!module.Value.TryGetProperty("endpoints", out var endpoints))
            {
                continue;
            }

            foreach (var endpoint in endpoints.EnumerateObject())
            {
                var endpointElement = endpoint.Value;
                var method = endpointElement.TryGetProperty("method", out var methodProp)
                    ? methodProp.GetString() ?? string.Empty
                    : string.Empty;

                string? path = null;
                if (endpointElement.TryGetProperty("endpoint", out var endpointProp))
                {
                    path = endpointProp.GetString();
                }
                else if (endpointElement.TryGetProperty("endpointTemplate", out var templateProp))
                {
                    path = templateProp.GetString();
                }

                if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                result.Add(new DiscoveredEndpoint(
                    method.ToUpperInvariant(),
                    NormalizeRouteTemplate(path),
                    $"{module.Name}.{endpoint.Name}"));
            }
        }

        return result;
    }

    private static string NormalizeFullPath(string classRoute, string methodRoute)
    {
        if (string.IsNullOrWhiteSpace(classRoute))
        {
            return NormalizeRouteTemplate(methodRoute);
        }

        if (string.IsNullOrWhiteSpace(methodRoute))
        {
            return NormalizeRouteTemplate(classRoute);
        }

        return NormalizeRouteTemplate($"{classRoute.TrimEnd('/')}/{methodRoute.TrimStart('/')}");
    }

    private static string NormalizeRouteTemplate(string route)
    {
        var normalized = (route ?? string.Empty).Trim();
        normalized = normalized.Trim('/');
        normalized = normalized.Replace("[controller]", string.Empty, StringComparison.OrdinalIgnoreCase);
        normalized = ParameterRegex.Replace(normalized, "{param}");
        normalized = Regex.Replace(normalized, @":[^}/]+", string.Empty);
        normalized = Regex.Replace(normalized, @"\{param\}", "{param}");
        normalized = Regex.Replace(normalized, @"/{2,}", "/");
        return "/" + normalized.ToLowerInvariant().Trim('/');
    }

    private static string EndpointSignature(string method, string path)
        => $"{method.ToUpperInvariant()} {NormalizeRouteTemplate(path)}";
}

public sealed record DiscoveredEndpoint(string Method, string Path, string Source);

public sealed record ContractMismatch(string Type, string Method, string Path, string Message, string Source);

public sealed record ContractValidationResult(
    string ContractsPath,
    DateTime GeneratedAtUtc,
    IReadOnlyList<DiscoveredEndpoint> ControllerEndpoints,
    IReadOnlyList<DiscoveredEndpoint> ContractEndpoints,
    IReadOnlyList<ContractMismatch> Mismatches);
