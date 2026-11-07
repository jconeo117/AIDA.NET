using System.Reflection;
using Aida.Guards;
using Aida.Guards.Dependencies;
using Aida.Guards.Reflection;
using Aida.Guards.Shape;
using Aida.Guards.Vocabulary;

namespace Aida.ArchitectureTests;

public sealed class ArchitectureRulesTests
{
    private static readonly string _repoRoot = RepositoryRoot.Find(AppContext.BaseDirectory);
    private static readonly SourceTree _sourceTree = new(_repoRoot);

    [Fact]
    public void Regla_C1_ningun_archivo_supera_las_trescientas_lineas()
    {
        var guard = new FileLengthGuard(300);
        var files = _sourceTree.ListFiles("src", [".cs", ".razor"]);
        var violations = new List<Violation>();

        foreach (var file in files)
        {
            var content = _sourceTree.ReadAllText(file);
            violations.AddRange(guard.Check(file, content));
        }

        Assert.True(violations.Count == 0, "\n" + string.Join("\n", violations.Select(v => v.ToString())));
    }

    [Fact]
    public void Regla_C3_las_dependencias_entre_proyectos_respetan_la_politica()
    {
        var guard = new DependencyGuard(DependencyPolicy.Allowed);
        var csprojFiles = _sourceTree.ListFiles("src", [".csproj"]);
        var violations = new List<Violation>();

        foreach (var csprojFile in csprojFiles)
        {
            var projectName = Path.GetFileNameWithoutExtension(csprojFile);
            var content = _sourceTree.ReadAllText(csprojFile);
            var references = ProjectReferenceReader.Read(content);
            violations.AddRange(guard.Check(projectName, references));
        }

        Assert.True(violations.Count == 0, "\n" + string.Join("\n", violations.Select(v => v.ToString())));
    }

    [Fact]
    public void Regla_C3_toda_carpeta_de_src_tiene_politica_y_toda_politica_tiene_carpeta()
    {
        var srcDirectory = Path.Combine(_repoRoot, "src");
        var existingProjects = Directory.GetDirectories(srcDirectory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToList();

        var policyProjects = DependencyPolicy.Allowed.Keys
            .Order(StringComparer.Ordinal)
            .ToList();

        var missingInPolicy = existingProjects.Except(policyProjects, StringComparer.Ordinal).ToList();
        var missingInFileSystem = policyProjects.Except(existingProjects, StringComparer.Ordinal).ToList();

        var violations = new List<string>();
        foreach (var missing in missingInPolicy)
        {
            violations.Add($"El proyecto «src/{missing}» existe en el disco pero no está registrado en DependencyPolicy.Allowed.");
        }

        foreach (var missing in missingInFileSystem)
        {
            violations.Add($"El proyecto «{missing}» está en DependencyPolicy.Allowed pero no existe su carpeta bajo «src/».");
        }

        Assert.True(violations.Count == 0, "\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Reglas_A1_y_D4_los_paquetes_restringidos_solo_estan_en_proyectos_permitidos()
    {
        var guard = new PackageGuard(PackagePolicy.Rules);
        var srcDirectory = Path.Combine(_repoRoot, "src");
        var projectDirectories = Directory.GetDirectories(srcDirectory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal);

        var violations = new List<Violation>();
        foreach (var projectName in projectDirectories)
        {
            var relativeLockPath = $"src/{projectName}/packages.lock.json";
            var lockFullPath = Path.Combine(_repoRoot, relativeLockPath);
            if (!File.Exists(lockFullPath))
            {
                continue;
            }

            var lockJson = _sourceTree.ReadAllText(relativeLockPath);
            var packages = LockFileReader.ReadPackages(lockJson);
            violations.AddRange(guard.Check(projectName, packages));
        }

        Assert.True(violations.Count == 0, "\n" + string.Join("\n", violations.Select(v => v.ToString())));
    }

    [Fact]
    public void Regla_C4_sin_estado_estatico_mutable_en_los_diez_ensamblados_de_src()
    {
        var guard = new StaticStateGuard();
        var assemblyNames = new[]
        {
            "Aida.Engine", "Aida.Guards", "Aida.Platform", "Aida.Capture",
            "Aida.Cognition", "Aida.Channel", "Aida.Arms.Health", "Aida.Arms.Agenda",
            "Aida.Web", "Aida.Cli",
        };

        var violations = new List<Violation>();
        foreach (var name in assemblyNames)
        {
            var assembly = Assembly.Load(new AssemblyName(name));
            violations.AddRange(guard.Check(assembly));
        }

        Assert.True(violations.Count == 0, "\n" + string.Join("\n", violations.Select(v => v.ToString())));
    }

    [Fact]
    public void Regla_C6_la_superficie_del_motor_es_sincrona()
    {
        var guard = new SynchronousSurfaceGuard();
        var engineAssembly = Assembly.Load(new AssemblyName("Aida.Engine"));
        var violations = guard.Check(engineAssembly);

        Assert.True(violations.Count == 0, "\n" + string.Join("\n", violations.Select(v => v.ToString())));
    }

    [Fact]
    public void Regla_A2_el_nucleo_no_contiene_vocabulario_de_dominio_ni_concatenaciones()
    {
        var vocabularyGuard = new VocabularyGuard(ForbiddenTermCatalog.LoadDefault());
        var concatenationGuard = new ConcatenationGuard();
        var violations = new List<Violation>();

        var coreProjects = new[] { "src/Aida.Engine", "src/Aida.Platform" };
        foreach (var project in coreProjects)
        {
            var csFiles = _sourceTree.ListFiles(project, [".cs"]);
            foreach (var file in csFiles)
            {
                var content = _sourceTree.ReadAllText(file);
                violations.AddRange(vocabularyGuard.Check(file, content));
                violations.AddRange(concatenationGuard.Check(file, content));
            }
        }

        Assert.True(violations.Count == 0, "\n" + string.Join("\n", violations.Select(v => v.ToString())));
    }
}
