using System.Globalization;
using Aida.Cognition.Interpretation;
using Aida.Engine.Text;
using Aida.Platform.Assistant;
using Xunit;

namespace Aida.Cognition.Tests.Interpretation;

public sealed class WhenReaderTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    private static WhenReader CreateReader()
    {
        var repoRoot = FindRepoRoot();
        var timePath = Path.Combine(repoRoot, "packs", "platform", "time.yaml");
        var timeVocab = TimeVocabulary.Load(timePath);
        return new WhenReader(timeVocab);
    }

    [Theory]
    [InlineData("mañana", "2026-11-25||Unknown")]
    [InlineData("pasado mañana a las 10", "2026-11-26|10:00|Unknown")]
    [InlineData("el jueves a las 9", "2026-11-26|09:00|Unknown")]
    [InlineData("el martes", "2026-12-01||Unknown")]
    [InlineData("hoy a las 3 de la tarde", "2026-11-24|15:00|Afternoon")]
    [InlineData("a las 10 y media", "|10:30|Unknown")]
    [InlineData("10:30", "|10:30|Unknown")]
    [InlineData("a las 8 y cuarto de la mañana", "|08:15|Morning")]
    [InlineData("mediodía", "|12:00|Unknown")]
    [InlineData("el 5 de diciembre", "2026-12-05||Unknown")]
    [InlineData("25/11", "2026-11-25||Unknown")]
    [InlineData("20/11", "2026-11-20||Unknown")]
    [InlineData("24/10", "2026-10-24||Unknown")]
    [InlineData("23/10", "2027-10-23||Unknown")]
    [InlineData("5/1", "2027-01-05||Unknown")]
    [InlineData("20/11/2026", "2026-11-20||Unknown")]
    [InlineData("el 20", "2026-12-20||Unknown")]
    [InlineData("el 20/11", "2026-11-20||Unknown")]
    [InlineData("el 24", "2026-11-24||Unknown")]
    [InlineData("el 31", "2026-12-31||Unknown")]
    [InlineData("mañana en la mañana", "2026-11-25||Morning")]
    [InlineData("el jueves en la tarde", "2026-11-26||Afternoon")]
    [InlineData("del martes al jueves a las 11", "2026-12-01||Unknown;2026-11-26|11:00|Unknown")]
    [InlineData("a las 10 el jueves", "2026-11-26|10:00|Unknown")]
    [InlineData("31/02", "")]
    [InlineData("29/02", "")]
    [InlineData("llame al 300 555 1234", "")]
    [InlineData("la 25", "")]
    [InlineData("25-11", "2026-11-25||Unknown")]
    [InlineData("20-11-2026", "2026-11-20||Unknown")]
    [InlineData("24/11", "2026-11-24||Unknown")]
    [InlineData("5 de diciembre", "2026-12-05||Unknown")]
    [InlineData("el 1 de enero", "2027-01-01||Unknown")]
    [InlineData("el 1", "2026-12-01||Unknown")]
    [InlineData("el 0", "")]
    [InlineData("el 32", "")]
    [InlineData("el 20-11", "2026-11-20||Unknown")]
    [InlineData("las 10", "|10:00|Unknown")]
    [InlineData("10 y media", "|10:30|Unknown")]
    [InlineData("10 y cuarto", "|10:15|Unknown")]
    [InlineData("10 de la mañana", "|10:00|Morning")]
    [InlineData("10 por la mañana", "|10:00|Morning")]
    [InlineData("10 en la mañana", "|10:00|Morning")]
    [InlineData("12 de la mañana", "|00:00|Morning")]
    [InlineData("12 de la tarde", "|12:00|Afternoon")]
    [InlineData("12 de la noche", "|12:00|Afternoon")]
    [InlineData("de la tarde", "||Afternoon")]
    [InlineData("por la mañana", "||Morning")]
    [InlineData("5 de papas", "")]
    [InlineData("35 de diciembre", "")]
    public void Read_resuelve_momentos_segun_tabla_de_especificacion(string frase, string esperados)
    {
        var reader = CreateReader();
        var spans = TextTokens.Spans(frase);
        var result = reader.Read(frase, spans, Today);

        var obtenido = FormatMentions(result.Mentions);
        Assert.Equal(esperados, obtenido);
    }

    [Theory]
    [InlineData("25/11", new[] { 0, 1 })]
    [InlineData("20/11/2026", new[] { 0, 1, 2 })]
    [InlineData("el martes", new[] { 0, 1 })]
    [InlineData("el 5 de diciembre", new[] { 0, 1, 2, 3 })]
    [InlineData("a las 10", new[] { 0, 1, 2 })]
    [InlineData("10 y media", new[] { 0, 1, 2 })]
    [InlineData("10 y cuarto", new[] { 0, 1, 2 })]
    [InlineData("en la mañana", new[] { 0, 1, 2 })]
    [InlineData("10 de la mañana", new[] { 0, 1, 2, 3 })]
    public void Read_registra_posiciones_consumidas_correctamente(string frase, int[] esperados)
    {
        var reader = CreateReader();
        var spans = TextTokens.Spans(frase);
        var result = reader.Read(frase, spans, Today);

        Assert.Equal(esperados.ToHashSet(), result.Consumed);
    }

    private static string FormatMentions(IReadOnlyList<WhenMention> mentions)
    {
        if (mentions.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(";", mentions.Select(m =>
            $"{(m.Day.HasValue ? m.Day.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty)}|" +
            $"{(m.Time.HasValue ? m.Time.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : string.Empty)}|" +
            $"{m.Period}"));
    }
}
