using Aida.Platform.Assistant;
using Xunit;

namespace Aida.Platform.Tests.Assistant;

public sealed class TimeVocabularyTests
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
    public void Carga_vocabulario_real_de_la_plataforma()
    {
        var path = Path.Combine(FindRepoRoot(), "packs", "platform", "time.yaml");
        var vocabulary = TimeVocabulary.Load(path);

        Assert.Contains("pasado", vocabulary.Words);
        Assert.Contains("manana", vocabulary.Words);
        Assert.Contains("miercoles", vocabulary.Words);
        Assert.Contains("mediodia", vocabulary.Words);

        // Marcadores y conjunciones no entran en Words
        Assert.DoesNotContain("la", vocabulary.Words);
        Assert.DoesNotContain("las", vocabulary.Words);
        Assert.DoesNotContain("y", vocabulary.Words);

        // Propiedades nuevas leídas del pack real
        Assert.Equal(DayOfWeek.Wednesday, vocabulary.Weekdays["miercoles"]);
        Assert.Equal(2, vocabulary.RelativeDays["pasado manana"]);
        Assert.Equal(DayPeriod.Afternoon, vocabulary.Periods["tarde"]);
        Assert.Equal(DayPeriod.Morning, vocabulary.Periods["manana"]);
        Assert.Equal(1, vocabulary.Months["enero"]);
        Assert.Equal(12, vocabulary.Months["diciembre"]);

        Assert.Contains("mediodia", vocabulary.Noon);
        Assert.Contains("media", vocabulary.Half);
        Assert.Contains("cuarto", vocabulary.Quarter);
        Assert.Contains("y", vocabulary.And);
        Assert.Contains("las", vocabulary.HourMarkers);
        Assert.Contains("la", vocabulary.PeriodMarkers);
    }

    [Fact]
    public void Falta_seccion_time_lanza_InvalidDataException()
    {
        var temp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(temp, "otra_seccion: {}");
            var ex = Assert.Throws<InvalidDataException>(() => TimeVocabulary.Load(temp));
            Assert.StartsWith(temp, ex.Message);
            Assert.Contains("falta la sección 'time'", ex.Message);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Theory]
    [InlineData("time:\n  relative_days: 'no_es_mapa'", "'time.relative_days' tiene que ser un mapa")]
    [InlineData("time:\n  relative_days: {}\n  weekdays: {}\n  months: {}\n  periods: {}\n  noon: 'no_es_lista'", "'time.noon' tiene que ser una lista")]
    [InlineData("time:\n  relative_days:\n    hoy: 8", "'time.relative_days.hoy' tiene un valor que no es válido")]
    [InlineData("time:\n  relative_days:\n    hoy: -1", "'time.relative_days.hoy' tiene un valor que no es válido")]
    [InlineData("time:\n  relative_days:\n    hoy: abc", "'time.relative_days.hoy' tiene un valor que no es válido")]
    [InlineData("time:\n  relative_days: {}\n  weekdays:\n    lunes: martedi", "'time.weekdays.lunes' tiene un valor que no es válido")]
    [InlineData("time:\n  relative_days: {}\n  weekdays: {}\n  months:\n    enero: 0", "'time.months.enero' tiene un valor que no es válido")]
    [InlineData("time:\n  relative_days: {}\n  weekdays: {}\n  months:\n    enero: 13", "'time.months.enero' tiene un valor que no es válido")]
    [InlineData("time:\n  relative_days: {}\n  weekdays: {}\n  months: {}\n  periods:\n    tarde: noche", "'time.periods.tarde' tiene un valor que no es válido")]
    [InlineData("time:\n  relative_days:\n    manana: 1\n    mañana: 1", "repite la clave «manana» una vez plegada")]
    public void Valida_estructura_y_valores_de_secciones_de_time(string yaml, string fragmento)
    {
        var temp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(temp, yaml);
            var ex = Assert.Throws<InvalidDataException>(() => TimeVocabulary.Load(temp));
            Assert.StartsWith(temp, ex.Message);
            Assert.Contains(fragmento, ex.Message);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
