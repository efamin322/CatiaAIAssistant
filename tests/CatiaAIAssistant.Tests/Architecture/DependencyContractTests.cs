using System.Reflection;
using System.Xml.Linq;

namespace CatiaAIAssistant.Tests.Architecture
{
    /// <summary>
    /// Executable form of ARCHITECTURE_CONTRACT.md §2: enforces the allowed dependency direction
    /// between projects. A failing test here means a change violates the modularity contract.
    /// </summary>
    public class DependencyContractTests
    {
        /// <summary>project -> internal projects it is ALLOWED to reference.</summary>
        private static readonly Dictionary<string, string[]> Allowed = new()
        {
            ["CatiaAIAssistant.Core"] = Array.Empty<string>(),
            ["CatiaAIAssistant.Engineering"] = new[] { "CatiaAIAssistant.Core" },
            ["CatiaAIAssistant.AI"] = new[] { "CatiaAIAssistant.Core" },
            ["CatiaAIAssistant.Infrastructure.Catia"] = new[] { "CatiaAIAssistant.Core" },
            ["CatiaAIAssistant.Application"] = new[] { "CatiaAIAssistant.Core" },
        };

        private static readonly string[] ForbiddenInPortableLayers =
        {
            "System.Windows.Forms", "Microsoft.VisualBasic", "System.Runtime.InteropServices.Marshal"
        };

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CatiaAIAssistant.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }

        private static IEnumerable<string> ProjectReferences(string csprojPath) =>
            XDocument.Load(csprojPath).Descendants("ProjectReference")
                .Select(e => Path.GetFileNameWithoutExtension(((string?)e.Attribute("Include") ?? "").Replace('\\', '/')));

        public static IEnumerable<object[]> LayerProjects()
        {
            string src = Path.Combine(RepoRoot(), "src");
            foreach (var dir in Directory.GetDirectories(src))
            {
                string name = Path.GetFileName(dir);
                if (Allowed.ContainsKey(name)) yield return new object[] { name };
            }
        }

        [Theory]
        [MemberData(nameof(LayerProjects))]
        public void Project_OnlyReferencesAllowedLayers(string project)
        {
            string csproj = Path.Combine(RepoRoot(), "src", project, project + ".csproj");
            var refs = ProjectReferences(csproj).ToList();
            var illegal = refs.Where(r => !Allowed[project].Contains(r)).ToList();
            Assert.True(illegal.Count == 0, $"{project} must not reference: {string.Join(", ", illegal)} (ARCHITECTURE_CONTRACT §2)");
        }

        [Theory]
        [InlineData("CatiaAIAssistant.Core")]
        [InlineData("CatiaAIAssistant.Engineering")]
        public void PortableLayer_TargetsPlainNet_AndHasNoUiOrComUsages(string project)
        {
            string dir = Path.Combine(RepoRoot(), "src", project);
            var tfm = XDocument.Load(Path.Combine(dir, project + ".csproj")).Descendants("TargetFramework").Single().Value;
            Assert.DoesNotContain("windows", tfm);

            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
                         .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            {
                string text = File.ReadAllText(file);
                foreach (var forbidden in ForbiddenInPortableLayers)
                    Assert.False(text.Contains(forbidden), $"{Path.GetFileName(file)} uses {forbidden} (ARCHITECTURE_CONTRACT §2/§5)");
                Assert.False(text.Contains(" dynamic "), $"{Path.GetFileName(file)} uses 'dynamic' (COM late binding belongs to Infrastructure)");
                Assert.False(text.Contains("CatiaAIAssistant.Infrastructure"), $"{Path.GetFileName(file)} references Infrastructure");
            }
        }

        [Fact]
        public void CoreAssembly_HasNoInternalAssemblyReferences()
        {
            var core = typeof(CatiaAIAssistant.Core.Text.TextNormalizer).Assembly;
            var internalRefs = core.GetReferencedAssemblies().Where(a => a.Name!.StartsWith("CatiaAIAssistant")).ToList();
            Assert.Empty(internalRefs);
        }

        [Fact]
        public void EngineeringAssembly_ReferencesOnlyCore()
        {
            var eng = typeof(CatiaAIAssistant.Engineering.EngineeringIntentAndCalculationEngine).Assembly;
            var internalRefs = eng.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("CatiaAIAssistant")).ToList();
            Assert.All(internalRefs, n => Assert.Equal("CatiaAIAssistant.Core", n));
        }
    }
}
