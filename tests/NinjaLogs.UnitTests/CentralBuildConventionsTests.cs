using System.Text.RegularExpressions;

namespace NinjaLogs.UnitTests;

public sealed class CentralBuildConventionsTests
{
    [Fact]
    public void DirectoryBuildProps_ShouldDefineTargetFramework()
    {
        string root = GetRepoRoot();
        string propsPath = Path.Combine(root, "Directory.Build.props");
        Assert.True(File.Exists(propsPath));
        string content = File.ReadAllText(propsPath);
        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", content);
    }

    [Fact]
    public void CsprojFiles_ShouldNotDeclareTargetFrameworkOrPackageVersions()
    {
        string root = GetRepoRoot();
        foreach (string csproj in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(csproj);
            Assert.DoesNotContain("<TargetFramework>", content);

            MatchCollection inlineVersions = Regex.Matches(content, "<PackageReference[^>]*\\sVersion=\"[^\"]+\"[^>]*/>");
            Assert.True(inlineVersions.Count == 0, $"Inline package versions are not allowed: {csproj}");
        }
    }

    [Fact]
    public void CsprojFiles_ShouldNotReferenceNewtonsoftJsonPackage()
    {
        string root = GetRepoRoot();
        foreach (string csproj in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(csproj);
            Assert.DoesNotContain("Newtonsoft.Json", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string GetRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NinjaLogs.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
