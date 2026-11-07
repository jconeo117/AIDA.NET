namespace Aida.Guards.Tests;

public sealed class RepositoryRootTests
{
    [Fact]
    public void Encuentra_la_raiz_desde_una_subcarpeta_con_Aida_slnx()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida_repo_test_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "tests", "sub", "deep");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(tempDir, "Aida.slnx"), "<Solution />");

        try
        {
            var root = RepositoryRoot.Find(subDir);
            Assert.Equal(Path.GetFullPath(tempDir), root);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Lanza_DirectoryNotFoundException_si_ninguna_carpeta_superior_contiene_Aida_slnx()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida_no_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var exception = Assert.Throws<DirectoryNotFoundException>(() => RepositoryRoot.Find(tempDir));
            Assert.Contains("Aida.slnx", exception.Message);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
