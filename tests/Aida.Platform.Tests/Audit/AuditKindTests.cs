using Aida.Platform.Audit;
using Xunit;

namespace Aida.Platform.Tests.Audit;

public sealed class AuditKindTests
{
    [Fact]
    public void Las_siete_de_la_plataforma_con_su_nombre()
    {
        Assert.Equal(7, AuditKind.Platform.Count);
        Assert.Equal("session_opened", AuditKind.SessionOpened.Name);
        Assert.Equal("session_closed", AuditKind.SessionClosed.Name);
        Assert.Equal("locked", AuditKind.Locked.Name);
        Assert.Equal("unlocked", AuditKind.Unlocked.Name);
        Assert.Equal("user_created", AuditKind.UserCreated.Name);
        Assert.Equal("user_disabled", AuditKind.UserDisabled.Name);
        Assert.Equal("password_reset", AuditKind.PasswordReset.Name);

        Assert.Contains(AuditKind.SessionOpened, AuditKind.Platform);
        Assert.Contains(AuditKind.SessionClosed, AuditKind.Platform);
        Assert.Contains(AuditKind.Locked, AuditKind.Platform);
        Assert.Contains(AuditKind.Unlocked, AuditKind.Platform);
        Assert.Contains(AuditKind.UserCreated, AuditKind.Platform);
        Assert.Contains(AuditKind.UserDisabled, AuditKind.Platform);
        Assert.Contains(AuditKind.PasswordReset, AuditKind.Platform);
    }

    [Theory]
    [InlineData("agenda_changed", true)]
    [InlineData("custom_action", true)]
    [InlineData("single", true)]
    [InlineData("", false)]
    [InlineData("1invalid", false)]
    [InlineData("Invalid_Upper", false)]
    [InlineData("with-dash", false)]
    public void Declare_valida_el_nombre(string name, bool valid)
    {
        if (valid)
        {
            var kind = AuditKind.Declare(name);
            Assert.Equal(name, kind.Name);
            Assert.Equal(name, kind.ToString());
        }
        else
        {
            Assert.Throws<ArgumentException>(() => AuditKind.Declare(name));
        }
    }

    [Fact]
    public void Igualdad_por_nombre()
    {
        var k1 = AuditKind.Declare("test_event");
        var k2 = AuditKind.Declare("test_event");
        var k3 = AuditKind.Declare("other_event");

        Assert.Equal(k1, k2);
        Assert.Equal(k1.GetHashCode(), k2.GetHashCode());
        Assert.NotEqual(k1, k3);
    }
}
