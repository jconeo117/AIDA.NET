using Aida.Platform.Wording;
using Xunit;

namespace Aida.Platform.Tests.Wording;

public sealed class ShellWordingTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    [Fact]
    public void Lee_el_ui_yaml_del_repositorio()
    {
        var path = Path.Combine(FindRepoRoot(), "packs", "platform", "ui.yaml");
        var wording = ShellWording.Load(path);

        Assert.Equal(TimeSpan.FromMinutes(10), wording.IdleLock);
        foreach (var key in ShellWording.RequiredKeys)
        {
            Assert.False(string.IsNullOrWhiteSpace(wording[key]), $"La clave «{key}» está vacía en ui.yaml");
        }
    }

    [Fact]
    public void Falta_una_clave_y_lo_dice()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                shell:
                  idle_lock_minutes: 10
                  brand: "AIDA"
                """);

            var ex = Assert.Throws<InvalidDataException>(() => ShellWording.Load(tempFile));
            Assert.Contains("faltan claves en la sección 'shell'", ex.Message);
            Assert.Contains("exit_label", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("\"10\"")]
    public void El_tiempo_sin_uso_tiene_que_ser_positivo(string idleValue)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var content = "shell:\n  idle_lock_minutes: " + idleValue + "\n";
            foreach (var key in ShellWording.RequiredKeys)
            {
                content += $"  {key}: \"texto\"\n";
            }

            File.WriteAllText(tempFile, content);

            var ex = Assert.Throws<InvalidDataException>(() => ShellWording.Load(tempFile));
            Assert.Contains("'shell.idle_lock_minutes' debe ser un número positivo", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Sin_seccion_shell_revienta()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "otra_seccion:\n  clave: valor\n");

            var ex = Assert.Throws<InvalidDataException>(() => ShellWording.Load(tempFile));
            Assert.Contains("falta la sección 'shell'", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
