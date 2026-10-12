using System.Text.RegularExpressions;

namespace Blueverse.MarineSafety.Tests.Integration;

/// <summary>
/// Meta-tests (M2-DOC-*): repository hygiene rules that keep the requirement
/// traceability contract honest. CaseId traits must be unique across the whole
/// marine test suite so CI evidence and the G00 mapping stay unambiguous.
/// </summary>
public sealed class DocumentationMetaTests
{
    [Fact]
    [Trait("CaseId", "M2-DOC-001")]
    public void M2_DOC_001_every_case_id_in_the_marine_suite_is_unique()
    {
        var testDirectory = AppContext.BaseDirectory;
        var sourceRoot = FindRepoRoot(testDirectory);
        Assert.NotNull(sourceRoot);

        var integrationDirectory = Path.Combine(sourceRoot!, "services", "marine-safety", "tests", "Blueverse.MarineSafety.Tests");
        Assert.True(Directory.Exists(integrationDirectory), $"Test source directory not found: {integrationDirectory}");

        var caseIds = new Dictionary<string, string>();
        foreach (var file in Directory.EnumerateFiles(integrationDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(content, @"Trait\(""CaseId"",\s*""(?<id>[^""]+)""\)"))
            {
                var caseId = match.Groups["id"].Value;
                Assert.True(
                    caseIds.TryAdd(caseId, Path.GetFileName(file)),
                    $"Duplicate CaseId '{caseId}' (first seen in {caseIds[caseId]}, also in {Path.GetFileName(file)})");
            }
        }

        // Sanity bound: the suite must contain a healthy number of traced
        // cases — guards against the scan silently matching nothing.
        Assert.True(caseIds.Count >= 60, $"Expected at least 60 traced case IDs, found {caseIds.Count}");
    }

    private static string? FindRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "compose.yaml")))
        {
            directory = directory.Parent!;
        }

        return directory?.FullName;
    }
}
