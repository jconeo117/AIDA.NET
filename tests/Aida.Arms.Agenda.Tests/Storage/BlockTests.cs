using Aida.Arms.Agenda.Storage;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Storage;

public sealed class BlockTests
{
    private static readonly DateTimeOffset BaseTime = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");

    [Fact]
    public void AppliesTo_Vacio_aplica_a_todos()
    {
        var block = new Block(1, string.Empty, BaseTime, BaseTime.AddHours(1), "Cierre general", "admin");

        Assert.True(block.AppliesTo("consultorio_1"));
        Assert.True(block.AppliesTo("consultorio_2"));
        Assert.True(block.AppliesTo(string.Empty));
    }

    [Fact]
    public void AppliesTo_Con_recurso_solo_aplica_a_ese_recurso()
    {
        var block = new Block(1, "consultorio_1", BaseTime, BaseTime.AddHours(1), "Ausencia", "admin");

        Assert.True(block.AppliesTo("consultorio_1"));
        Assert.False(block.AppliesTo("consultorio_2"));
        Assert.False(block.AppliesTo(string.Empty));
    }

    [Theory]
    [InlineData("10:15", "10:45", true)]   // Dentro
    [InlineData("09:30", "11:30", true)]   // Envuelve el bloqueo
    [InlineData("09:30", "10:30", true)]   // Solape por la izquierda
    [InlineData("10:30", "11:30", true)]   // Solape por la derecha
    [InlineData("09:00", "10:00", false)]  // Toca borde izquierdo (fin = inicio bloqueo)
    [InlineData("11:00", "12:00", false)]  // Toca borde derecho (inicio = fin bloqueo)
    [InlineData("08:00", "09:00", false)]  // Totalmente antes
    [InlineData("12:00", "13:00", false)]  // Totalmente después
    public void Crosses_Evalua_cruce_de_intervalos(string startHm, string endHm, bool expected)
    {
        var blockStart = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");
        var blockEnd = DateTimeOffset.Parse("2026-11-16T11:00:00-05:00");
        var block = new Block(1, "r1", blockStart, blockEnd, "Motivo", "u1");

        var testStart = DateTimeOffset.Parse($"2026-11-16T{startHm}:00-05:00");
        var testEnd = DateTimeOffset.Parse($"2026-11-16T{endHm}:00-05:00");

        Assert.Equal(expected, block.Crosses(testStart, testEnd));
    }
}
