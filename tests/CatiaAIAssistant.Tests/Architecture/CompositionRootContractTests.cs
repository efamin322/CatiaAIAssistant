namespace CatiaAIAssistant.Tests.Architecture
{
    /// <summary>
    /// ARCHITECTURE_CONTRACT §3/§5 — only the Composition Root may name Infrastructure implementations;
    /// UI code must work exclusively through Core interfaces and never touch COM late binding.
    /// </summary>
    public class CompositionRootContractTests
    {
        private static string AppDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CatiaAIAssistant.sln"))) dir = dir.Parent;
            return Path.Combine(dir!.FullName, "src", "CatiaAIAssistant");
        }

        private static IEnumerable<string> AppSources() =>
            Directory.EnumerateFiles(AppDir(), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        [Fact]
        public void OnlyCompositionRoot_ReferencesInfrastructure()
        {
            var offenders = AppSources()
                .Where(f => Path.GetFileName(f) != "AppComposition.cs")
                .Where(f => File.ReadAllText(f).Contains("CatiaAIAssistant.Infrastructure"))
                .Select(Path.GetFileName).ToList();
            Assert.True(offenders.Count == 0, "Infrastructure referenced outside Composition Root: " + string.Join(", ", offenders));
        }

        [Fact]
        public void AppCode_DoesNotUseComLateBinding()
        {
            var offenders = AppSources().Where(f => File.ReadAllText(f).Contains("dynamic ")).Select(Path.GetFileName).ToList();
            Assert.True(offenders.Count == 0, "UI must not use 'dynamic' COM objects: " + string.Join(", ", offenders));
        }

        [Fact]
        public void AppCode_DoesNotConstructServicesOutsideCompositionRoot()
        {
            string[] concrete = { "new OperationLogger(", "new CatiaConnectionService(", "new CatiaDocumentManager(",
                                  "new CatiaBasicPartService(", "new CatiaMechanicalEngine(", "new EngineeringIntentAndCalculationEngine(" };
            var offenders = AppSources().Where(f => Path.GetFileName(f) != "AppComposition.cs")
                .Where(f => { var t = File.ReadAllText(f); return concrete.Any(t.Contains); })
                .Select(Path.GetFileName).ToList();
            Assert.True(offenders.Count == 0, "Services constructed outside Composition Root: " + string.Join(", ", offenders));
        }
    }
}
