using Aida.Arms.Agenda.Setup;
using Aida.Platform.Packs;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Setup;

public sealed class AgendaSetupLoaderTests
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
    public void Copia_la_plantilla_si_no_existe()
    {
        var repoRoot = FindRepoRoot();
        var armPackDir = Path.Combine(repoRoot, "packs", "agenda");
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-setup-" + Guid.NewGuid().ToString("n"));
        var configFile = Path.Combine(tempDir, "config", "agenda.yaml");

        try
        {
            Assert.False(File.Exists(configFile));
            var setup = AgendaSetupLoader.Load(configFile, armPackDir, "consultorio");

            Assert.True(File.Exists(configFile));
            Assert.Equal("consultorio", setup.Sector);
            Assert.True(setup.SlotMinutes > 0);
            Assert.NotEmpty(setup.Services);
            Assert.NotEmpty(setup.Resources);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Falla_si_no_es_mapeo()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "- lista\n- en vez\n- de dict\n");
            var ex = Assert.Throws<InvalidDataException>(() =>
                AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(tempFile), tempFile));
            Assert.Contains("tiene que ser un único mapa", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("\"30\"")]
    [InlineData("texto")]
    public void Falla_slot_minutes_invalido(string slotMinutesValue)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, $"""
                sector: test
                client_label: Paciente
                slot_minutes: {slotMinutesValue}
                services: []
                resources: []
                """);
            var ex = Assert.Throws<InvalidDataException>(() =>
                AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(tempFile), tempFile));
            Assert.Contains("'slot_minutes' debe ser un entero positivo", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Falla_minutos_de_servicio_invalidos()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                sector: test
                client_label: Paciente
                slot_minutes: 15
                services:
                  - id: s1
                    label: S1
                    minutes: 0
                resources: []
                """);
            var ex = Assert.Throws<InvalidDataException>(() =>
                AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(tempFile), tempFile));
            Assert.Contains("el servicio «s1» necesita minutos positivos", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Falla_recurso_con_servicio_desconocido()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                sector: test
                client_label: Paciente
                slot_minutes: 15
                services:
                  - id: s1
                    label: S1
                    minutes: 15
                resources:
                  - id: r1
                    label: R1
                    place: P1
                    services:
                      - fantasma
                """);
            var ex = Assert.Throws<InvalidDataException>(() =>
                AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(tempFile), tempFile));
            Assert.Contains("ofrece servicios que no existen: fantasma", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Falla_dia_desconocido()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                sector: test
                client_label: Paciente
                slot_minutes: 15
                services:
                  - id: s1
                    label: S1
                    minutes: 15
                resources:
                  - id: r1
                    label: R1
                    place: P1
                    services:
                      - s1
                    hours:
                      funday:
                        - ["08:00", "12:00"]
                """);
            var ex = Assert.Throws<InvalidDataException>(() =>
                AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(tempFile), tempFile));
            Assert.Contains("día desconocido «funday» en el recurso «r1»", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Falla_hora_mal_formateada()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                sector: test
                client_label: Paciente
                slot_minutes: 15
                services:
                  - id: s1
                    label: S1
                    minutes: 15
                resources:
                  - id: r1
                    label: R1
                    place: P1
                    services:
                      - s1
                    hours:
                      mon:
                        - ["ocho", "12:00"]
                """);
            var ex = Assert.Throws<InvalidDataException>(() =>
                AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(tempFile), tempFile));
            Assert.Contains("hora inválida «ocho»", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Falla_tramo_al_reves()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                sector: test
                client_label: Paciente
                slot_minutes: 15
                services:
                  - id: s1
                    label: S1
                    minutes: 15
                resources:
                  - id: r1
                    label: R1
                    place: P1
                    services:
                      - s1
                    hours:
                      mon:
                        - ["12:00", "08:00"]
                """);
            var ex = Assert.Throws<InvalidDataException>(() =>
                AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(tempFile), tempFile));
            Assert.Contains("un tramo está al revés en «r1»", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Falla_tramo_no_multiplo_del_hueco()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                sector: test
                client_label: Paciente
                slot_minutes: 30
                services:
                  - id: s1
                    label: S1
                    minutes: 30
                resources:
                  - id: r1
                    label: R1
                    place: P1
                    services:
                      - s1
                    hours:
                      mon:
                        - ["08:00", "08:45"]
                """);
            var ex = Assert.Throws<InvalidDataException>(() =>
                AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(tempFile), tempFile));
            Assert.Contains("un tramo no es múltiplo de 30 minutos en «r1»", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Lee_las_dos_plantillas_del_repositorio()
    {
        var repoRoot = FindRepoRoot();
        var armPackDir = Path.Combine(repoRoot, "packs", "agenda");

        var consultorioPath = Path.Combine(armPackDir, "consultorio", "agenda.yaml");
        var setup1 = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(consultorioPath), consultorioPath);
        Assert.Equal("consultorio", setup1.Sector);
        Assert.Equal(30, setup1.SlotMinutes);
        Assert.Equal("Paciente", setup1.ClientLabel);
        Assert.Equal(3, setup1.Services.Count);
        Assert.Equal(2, setup1.Resources.Count);

        var peluqueriaPath = Path.Combine(armPackDir, "peluqueria", "agenda.yaml");
        var setup2 = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(peluqueriaPath), peluqueriaPath);
        Assert.Equal("peluqueria", setup2.Sector);
        Assert.Equal(15, setup2.SlotMinutes);
        Assert.Equal("Cliente", setup2.ClientLabel);
        Assert.Equal(4, setup2.Services.Count);
        Assert.Equal(3, setup2.Resources.Count);
    }
}
