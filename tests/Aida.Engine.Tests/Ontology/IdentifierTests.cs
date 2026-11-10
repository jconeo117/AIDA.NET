using Aida.Engine.Ontology;

namespace Aida.Engine.Tests.Ontology;

public sealed class IdentifierTests
{
    [Fact]
    public void AgentId_y_ActionId_rechazan_null()
    {
        Assert.Throws<ArgumentNullException>(() => new AgentId(null!));
        Assert.Throws<ArgumentNullException>(() => new ActionId(null!));

        var agent = new AgentId("agente-1");
        var action = new ActionId("accion-1");
        Assert.Equal("agente-1", agent.Value);
        Assert.Equal("accion-1", action.Value);
    }
}
