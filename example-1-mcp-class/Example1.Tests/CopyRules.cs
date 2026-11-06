namespace Example1.Tests;

internal static class CopyRules
{
    // Split so this file does not itself contain the wording it rejects.
    private static readonly string[] Banned =
    [
        "every " + "method",
        "any " + "public method",
        "all " + "public methods",
        "70" + "%",
        "fast" + "er",
        "bench" + "mark",
        "lower " + "overhead",
        "lower " + "latency",
        "speed" + "up",
        "performance " + "win",
        "filter is " + "authorization",
        "filter is " + "authentication",
        "filters " + "authorize",
        "filter " + "authorizes",
    ];

    public static void AssertRepository(string repoRoot)
    {
        foreach (var file in Enumerate(repoRoot))
        {
            var text = File.ReadAllText(file);
            foreach (var phrase in Banned)
            {
                var index = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
                Assert.True(
                    index < 0,
                    $"Banned phrase \"{phrase}\" in {Path.GetRelativePath(repoRoot, file)}.");
            }
        }
    }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GraftcodeSamples.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find GraftcodeSamples.sln.");
    }

    private static IEnumerable<string> Enumerate(string root)
    {
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", "TestResults",
        };

        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Any(part => skip.Contains(part)))
            {
                continue;
            }

            var name = Path.GetFileName(path);
            var extension = Path.GetExtension(path);
            if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
                || extension is ".cs" or ".csproj" or ".sln" or ".md" or ".json" or ".yml"
                    or ".yaml" or ".proto" or ".sh" or ".props" or ".targets" or ".config")
            {
                yield return path;
            }
        }
    }
}
