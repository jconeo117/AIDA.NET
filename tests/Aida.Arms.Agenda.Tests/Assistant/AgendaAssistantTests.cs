using Aida.Arms.Agenda.Assistant;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Assistant;

public sealed class AgendaAssistantTests
{
    [Fact]
    public void Valida_las_trece_frases_de_cancelar_y_reprogramar_sobre_la_cita_concreta()
    {
        using var ctx = new AgendaAssistantContext();
        var today = DateOnly.FromDateTime(ctx.Time.Now.DateTime);

        // 1. cancela la cita de Juan del martes -> Cancel, la de hoy a las 11:00 (Weekday, 08:00 ya pasó)
        var p1 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Juan del martes", today));
        Assert.Equal(ProposalKind.Cancel, p1.Kind);
        Assert.Single(p1.Appointments);
        Assert.Equal(new TimeOnly(11, 0), TimeOnly.FromDateTime(p1.Appointments[0].Appointment.Start.DateTime));

        // 2. cancela la cita de Juan -> PickToCancel: la de hoy a las 11:00 y la del jueves
        var p2 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Juan", today));
        Assert.Equal(ProposalKind.PickToCancel, p2.Kind);
        Assert.Equal(2, p2.Appointments.Count);
        Assert.Equal(new TimeOnly(11, 0), TimeOnly.FromDateTime(p2.Appointments[0].Appointment.Start.DateTime));
        Assert.Equal(new DateOnly(2026, 11, 26), DateOnly.FromDateTime(p2.Appointments[1].Appointment.Start.DateTime));

        // 3. cancela la cita de Juan de las 11 -> Cancel, la de las 11:00
        var p3 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Juan de las 11", today));
        Assert.Equal(ProposalKind.Cancel, p3.Kind);
        Assert.Single(p3.Appointments);
        Assert.Equal(new TimeOnly(11, 0), TimeOnly.FromDateTime(p3.Appointments[0].Appointment.Start.DateTime));

        // 4. cancela la cita de Ana -> PickToCancel: la de Ana Pérez (11-25) y la de Ana Gómez (11-27) (D24)
        var p4 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Ana", today));
        Assert.Equal(ProposalKind.PickToCancel, p4.Kind);
        Assert.Equal(2, p4.Appointments.Count);
        Assert.Equal("Ana Pérez", p4.Appointments[0].ClientName);
        Assert.Equal("Ana Gómez", p4.Appointments[1].ClientName);

        // 5. cancela la cita de Ana de las 3 -> Cancel, la de Ana Gómez a las 15:00 (D26)
        var p5 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Ana de las 3", today));
        Assert.Equal(ProposalKind.Cancel, p5.Kind);
        Assert.Single(p5.Appointments);
        Assert.Equal("Ana Gómez", p5.Appointments[0].ClientName);
        Assert.Equal(new TimeOnly(15, 0), TimeOnly.FromDateTime(p5.Appointments[0].Appointment.Start.DateTime));

        // 6. cancela la cita de Ana en la tarde -> Cancel, la de Ana Gómez
        var p6 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Ana en la tarde", today));
        Assert.Equal(ProposalKind.Cancel, p6.Kind);
        Assert.Single(p6.Appointments);
        Assert.Equal("Ana Gómez", p6.Appointments[0].ClientName);

        // 7. cancela la cita de Pedro -> OpenCard con Pedro (D23: su única cita está cancelada)
        var p7 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Pedro", today));
        Assert.Equal(ProposalKind.OpenCard, p7.Kind);
        Assert.Single(p7.Clients);
        Assert.Equal("Pedro Díaz", p7.Clients[0].Name);

        // 8. cancela la cita de Carlos -> NoClient
        var p8 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Carlos", today));
        Assert.Equal(ProposalKind.NoClient, p8.Kind);

        // 9. mueve la cita de Ana Pérez al jueves a las 11 -> Move, la del 11-25, MoveTo = 11-26, 11:00
        var p9 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("mueve la cita de Ana Pérez al jueves a las 11", today));
        Assert.Equal(ProposalKind.Move, p9.Kind);
        Assert.Single(p9.Appointments);
        Assert.Equal("Ana Pérez", p9.Appointments[0].ClientName);
        Assert.Equal(new DateOnly(2026, 11, 26), p9.MoveTo);
        Assert.Equal(new TimeOnly(11, 0), p9.Time);

        // 10. mueve la cita de Juan del martes al jueves a las 11 -> Move, la de hoy, MoveTo = 11-26, 11:00
        var p10 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("mueve la cita de Juan del martes al jueves a las 11", today));
        Assert.Equal(ProposalKind.Move, p10.Kind);
        Assert.Single(p10.Appointments);
        Assert.Equal(new TimeOnly(11, 0), TimeOnly.FromDateTime(p10.Appointments[0].Appointment.Start.DateTime));
        Assert.Equal(new DateOnly(2026, 11, 26), p10.MoveTo);
        Assert.Equal(new TimeOnly(11, 0), p10.Time);

        // 11. mueve la cita de Ana Pérez para las 9 -> Move, MoveTo = null, 09:00 sobre su propio día
        var p11 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("mueve la cita de Ana Pérez para las 9", today));
        Assert.Equal(ProposalKind.Move, p11.Kind);
        Assert.Single(p11.Appointments);
        Assert.Equal("Ana Pérez", p11.Appointments[0].ClientName);
        Assert.Null(p11.MoveTo);
        Assert.Equal(new TimeOnly(9, 0), p11.Time);

        // 12. mueve la cita de Ana Pérez al 20/11 -> PastDay
        var p12 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("mueve la cita de Ana Pérez al 20/11", today));
        Assert.Equal(ProposalKind.PastDay, p12.Kind);

        // 13. mueve la cita de Juan al viernes -> PickToMove, sus dos citas futuras, MoveTo = 11-27
        var p13 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("mueve la cita de Juan al viernes", today));
        Assert.Equal(ProposalKind.PickToMove, p13.Kind);
        Assert.Equal(2, p13.Appointments.Count);
        Assert.Equal(new DateOnly(2026, 11, 27), p13.MoveTo);
    }

    [Fact]
    public void Valida_las_nueve_frases_del_contrato_segun_especificacion()
    {
        using var ctx = new AgendaAssistantContext();
        var today = DateOnly.FromDateTime(ctx.Time.Now.DateTime);

        // 1. cita para Juan Ruiz mañana a las 10, control -> NewAppointment, 11-25, control, 10:00, TimeTaken = false
        var p1 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan Ruiz mañana a las 10, control", today));
        Assert.Equal(ProposalKind.NewAppointment, p1.Kind);
        Assert.Equal(new DateOnly(2026, 11, 25), p1.Day);
        Assert.Equal("control", p1.Service);
        Assert.Equal(new TimeOnly(10, 0), p1.Time);
        Assert.False(p1.TimeTaken);
        Assert.Equal("Juan Ruiz", p1.Prefill?.Name);

        // 2. cita para Juan hoy a las 3 -> 15:00 (D12: las 03:00 caen fuera y las 15:00 dentro)
        var p2 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan hoy a las 3", today));
        Assert.Equal(ProposalKind.NewAppointment, p2.Kind);
        Assert.Equal(today, p2.Day);
        Assert.Equal(new TimeOnly(15, 0), p2.Time);
        Assert.False(p2.TimeTaken);

        // 3. cita para Juan hoy a las 10 -> 10:00 (cae dentro por la mañana)
        var p3 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan hoy a las 10", today));
        Assert.Equal(ProposalKind.NewAppointment, p3.Kind);
        Assert.Equal(today, p3.Day);
        Assert.Equal(new TimeOnly(10, 0), p3.Time);
        Assert.False(p3.TimeTaken);

        // 4. cita para Juan hoy a las 10 con la de las 10:00 ocupada -> 10:00, TimeTaken = true
        ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Paciente Ocupado",
            ["phone"] = "3009998877",
            ["service"] = "primera_vez",
            ["start"] = "2026-11-24T10:00:00-05:00",
        });
        var p4 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan hoy a las 10", today));
        Assert.Equal(ProposalKind.NewAppointment, p4.Kind);
        Assert.Equal(today, p4.Day);
        Assert.Equal(new TimeOnly(10, 0), p4.Time);
        Assert.True(p4.TimeTaken);

        // 5. qué hay libre el jueves en la tarde -> FreeSlots, 11-26, primer servicio, 14:00 (D18)
        ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "bloquear", new Dictionary<string, string>
        {
            ["start"] = "2026-11-26T12:00:00-05:00",
            ["end"] = "2026-11-26T13:00:00-05:00",
            ["resource"] = "consultorio_1",
            ["reason"] = "almuerzo",
        });
        var p5 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("qué hay libre el jueves en la tarde", today));
        Assert.Equal(ProposalKind.FreeSlots, p5.Kind);
        Assert.Equal(new DateOnly(2026, 11, 26), p5.Day);
        Assert.Equal("primera_vez", p5.Service);
        Assert.Equal(new TimeOnly(14, 0), p5.Time);
        Assert.False(p5.TimeTaken);

        // 6. qué hay libre el jueves en la tarde, con la tarde llena -> TimeTaken = true, Time = null
        ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "bloquear", new Dictionary<string, string>
        {
            ["start"] = "2026-11-26T14:00:00-05:00",
            ["end"] = "2026-11-26T18:00:00-05:00",
            ["resource"] = "tarde",
            ["reason"] = "cerrado",
        });
        var p6 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("qué hay libre el jueves en la tarde", today));
        Assert.Equal(ProposalKind.FreeSlots, p6.Kind);
        Assert.Equal(new DateOnly(2026, 11, 26), p6.Day);
        Assert.Null(p6.Time);
        Assert.True(p6.TimeTaken);

        // 7. cita para Juan el 20/11 -> PastDay, 11-20
        var p7 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan el 20/11", today));
        Assert.Equal(ProposalKind.PastDay, p7.Kind);
        Assert.Equal(new DateOnly(2026, 11, 20), p7.Day);
        Assert.Null(p7.Time);
        Assert.False(p7.TimeTaken);

        // 8. cita para Juan -> hoy, el primer servicio, Time = null
        var p8 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan", today));
        Assert.Equal(ProposalKind.NewAppointment, p8.Kind);
        Assert.Equal(today, p8.Day);
        Assert.Equal("primera_vez", p8.Service);
        Assert.Null(p8.Time);
        Assert.False(p8.TimeTaken);

        // 9. cancela la cita de Juan mañana -> OpenCard (D23: Juan no tiene citas mañana)
        var p9 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Juan mañana", today));
        Assert.Equal(ProposalKind.OpenCard, p9.Kind);
        Assert.Single(p9.Clients);
        Assert.Equal("Juan Ruiz", p9.Clients[0].Name);
    }

    [Fact]
    public void Valida_intenciones_y_busqueda_de_contactos_del_asistente()
    {
        using var ctx = new AgendaAssistantContext();
        var today = DateOnly.FromDateTime(ctx.Time.Now.DateTime);

        // cita para Carlos Díaz -> NewAppointment, Prefill = (0, Carlos Díaz, "")
        var p1 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Carlos Díaz", today));
        Assert.Equal(ProposalKind.NewAppointment, p1.Kind);
        Assert.NotNull(p1.Prefill);
        Assert.Equal(0, p1.Prefill.Id);
        Assert.Equal("Carlos Díaz", p1.Prefill.Name);
        Assert.Equal(string.Empty, p1.Prefill.Phone);

        // agéndame al 3005551234 -> NewAppointment con Juan
        var p2 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("agéndame al 3005551234", today));
        Assert.Equal(ProposalKind.NewAppointment, p2.Kind);
        Assert.NotNull(p2.Prefill);
        Assert.Equal("Juan Ruiz", p2.Prefill.Name);

        // cita para Ana -> PickClientForNew: Ana Gómez y Ana Pérez
        var p3 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Ana", today));
        Assert.Equal(ProposalKind.PickClientForNew, p3.Kind);
        Assert.Equal(2, p3.Clients.Count);
        Assert.Equal("Ana Gómez", p3.Clients[0].Name);
        Assert.Equal("Ana Pérez", p3.Clients[1].Name);

        // mueve la cita de Ana -> PickToMove, las dos citas futuras (D24)
        var p4 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("mueve la cita de Ana", today));
        Assert.Equal(ProposalKind.PickToMove, p4.Kind);
        Assert.Equal(2, p4.Appointments.Count);

        // cancela la cita de Carlos -> NoClient
        var p5 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Carlos", today));
        Assert.Equal(ProposalKind.NoClient, p5.Kind);
        Assert.Empty(p5.Clients);

        // buenos días -> NotUnderstood
        var p6 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("buenos días", today));
        Assert.Equal(ProposalKind.NotUnderstood, p6.Kind);
    }
}
