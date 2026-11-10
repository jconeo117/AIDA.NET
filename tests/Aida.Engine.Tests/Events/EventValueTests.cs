using System.Text;
using Aida.Engine.Events;

namespace Aida.Engine.Tests.Events;

public sealed class EventValueTests
{
    [Fact]
    public void EventValue_Of_rechaza_un_texto_null()
    {
        Assert.Throws<ArgumentNullException>(() => EventValue.Of((string)null!));
    }

    [Fact]
    public void EventValue_escribe_correctamente_cada_tipo()
    {
        var text = EventValue.Of("hola");
        var integer = EventValue.Of(42L);
        var number = EventValue.Of(3.14);
        var booleanTrue = EventValue.Of(true);
        var booleanFalse = EventValue.Of(false);

        var sb = new StringBuilder();
        text.WriteJson(sb);
        Assert.Equal("\"hola\"", sb.ToString());

        sb.Clear();
        integer.WriteJson(sb);
        Assert.Equal("42", sb.ToString());

        sb.Clear();
        number.WriteJson(sb);
        Assert.Equal("3.14", sb.ToString());

        sb.Clear();
        booleanTrue.WriteJson(sb);
        Assert.Equal("true", sb.ToString());

        sb.Clear();
        booleanFalse.WriteJson(sb);
        Assert.Equal("false", sb.ToString());
    }

    [Fact]
    public void EventValue_por_defecto_falla_al_escribir()
    {
        var defaultValue = default(EventValue);
        var sb = new StringBuilder();
        Assert.Throws<InvalidOperationException>(() => defaultValue.WriteJson(sb));
    }
}
