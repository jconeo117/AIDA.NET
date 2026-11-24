using Aida.Arms.Agenda;
using Xunit;

namespace Aida.Arms.Agenda.Tests;

public sealed class AgendaDeclarationsTests
{
    [Fact]
    public void Version_del_esquema_es_dos()
    {
        Assert.Equal(2, AgendaDeclarations.SchemaVersion);
    }

    [Fact]
    public void ArmId_y_permisos_y_hechos_declarados()
    {
        Assert.Equal("agenda", AgendaDeclarations.ArmId);
        Assert.Contains("agenda.manage", AgendaDeclarations.Permissions);
        Assert.Contains("agenda.block", AgendaDeclarations.Permissions);
        Assert.NotEmpty(AgendaDeclarations.AuditKinds);
    }
}
