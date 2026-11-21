using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Desk;

public sealed class ClientCardTests
{
    private static AppointmentView CreateView(long id, DateTimeOffset start, AppointmentState state)
    {
        var app = new Appointment(id, 1, "control", "consultorio_1", start, 30, state, "u1", "u1");
        return new AppointmentView(app, "Ana Pérez", "3005550142", "Control", "Usuario 1", "Usuario 1");
    }

    [Fact]
    public void Cifras_cuentan_proximas_atendidas_y_no_asistio_con_borde_de_now()
    {
        var client = new Client(1, "Ana Pérez", "3005550142");
        var now = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");

        var appPasada = CreateView(1, now.AddHours(-2), AppointmentState.Booked);
        var appBorde = CreateView(2, now, AppointmentState.Booked); // Start == now
        var appFutura = CreateView(3, now.AddHours(1), AppointmentState.Booked);
        var appAtendida = CreateView(4, now.AddHours(-3), AppointmentState.Attended);
        var appNoShow = CreateView(5, now.AddHours(-4), AppointmentState.NoShow);

        var views = new List<AppointmentView> { appNoShow, appAtendida, appPasada, appBorde, appFutura };
        var card = ClientCard.Build(client, views, now);

        // Upcoming cuenta las Booked con Start >= now (appBorde y appFutura = 2)
        Assert.Equal(2, card.Upcoming);
        Assert.Equal(1, card.Attended);
        Assert.Equal(1, card.NoShow);
    }

    [Fact]
    public void Citas_canceladas_no_suman_en_ninguna_cifra()
    {
        var client = new Client(1, "Ana Pérez", "3005550142");
        var now = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");

        var canceladaPasada = CreateView(1, now.AddHours(-1), AppointmentState.Cancelled);
        var canceladaBorde = CreateView(2, now, AppointmentState.Cancelled);
        var canceladaFutura = CreateView(3, now.AddHours(2), AppointmentState.Cancelled);

        var views = new List<AppointmentView> { canceladaPasada, canceladaBorde, canceladaFutura };
        var card = ClientCard.Build(client, views, now);

        Assert.Equal(0, card.Upcoming);
        Assert.Equal(0, card.Attended);
        Assert.Equal(0, card.NoShow);
        Assert.Equal(3, card.Appointments.Count);
    }

    [Fact]
    public void Citas_se_ordenan_de_la_mas_reciente_a_la_mas_antigua()
    {
        var client = new Client(1, "Ana Pérez", "3005550142");
        var now = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");

        var v1 = CreateView(1, now.AddHours(-2), AppointmentState.Booked);
        var v2 = CreateView(2, now.AddHours(-1), AppointmentState.Booked);
        var v3 = CreateView(3, now.AddHours(1), AppointmentState.Booked);

        // Entrada en orden cronológico (de más antigua a más reciente)
        var views = new List<AppointmentView> { v1, v2, v3 };
        var card = ClientCard.Build(client, views, now);

        // Salida debe invertirse (de más reciente a más antigua)
        Assert.Equal(3, card.Appointments.Count);
        Assert.Equal(3, card.Appointments[0].Appointment.Id);
        Assert.Equal(2, card.Appointments[1].Appointment.Id);
        Assert.Equal(1, card.Appointments[2].Appointment.Id);
    }
}
