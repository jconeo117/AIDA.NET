using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Text;
using Aida.Platform.Assistant;

namespace Aida.Arms.Agenda.Assistant;

/// <summary>Convierte lo entendido en una propuesta para la pantalla de la agenda.</summary>
/// <remarks>
/// Solo lee: la configuración, la búsqueda, la ficha y el día del mostrador. El cambio lo hace el operador con el botón de
/// siempre, que pasa por la puerta (ADR 0007, ADR 0037). Si encaja más de una persona, las devuelve para que elija el
/// operador: nunca elige él. Las fechas y las horas solo se leen para agendar y para ver los huecos: cancelar y reprogramar
/// abren la ficha de hoy (N014 lee su fecha).
/// </remarks>
public sealed class AgendaAssistant
{
    private readonly AgendaDesk _desk;
    private readonly TimeProvider _time;

    /// <summary>Crea el asistente.</summary>
    /// <param name="desk">El mostrador.</param>
    /// <param name="time">El reloj, para el día de hoy.</param>
    /// <exception cref="ArgumentNullException">Si alguno es <see langword="null"/>.</exception>
    public AgendaAssistant(AgendaDesk desk, TimeProvider time)
    {
        _desk = desk ?? throw new ArgumentNullException(nameof(desk));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>Propone.</summary>
    /// <param name="understanding">Lo entendido.</param>
    /// <returns>La propuesta.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="understanding"/> es <see langword="null"/>.</exception>
    public AgendaProposal Propose(Understanding understanding)
    {
        ArgumentNullException.ThrowIfNull(understanding);
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        if (understanding.Intent is null)
        {
            return new AgendaProposal(ProposalKind.NotUnderstood, today, null, null, false, null, []);
        }

        var reads = understanding.Intent is "agendar" or "ver_huecos";
        var mention = understanding.When.FirstOrDefault();
        var day = reads && mention?.Day is { } given ? given : today;
        if (day < today)
        {
            return new AgendaProposal(ProposalKind.PastDay, day, null, null, false, null, []);
        }

        var words = understanding.Words;
        var used = new HashSet<int>();
        var service = MatchService(words, used) ?? _desk.Setup.Services.FirstOrDefault()?.Id;
        var rest = Enumerable.Range(0, words.Count)
            .Where(index => words[index].Role == WordRole.Free && !used.Contains(index))
            .ToList();
        var restText = string.Join(' ', rest.Select(index => words[index].Text));
        var clients = Lookup(understanding.Phone, restText, rest.Count > 0);
        if (understanding.Intent is "cancelar" or "reprogramar")
        {
            return ProposeCard(today, clients);
        }

        var (time, taken) = ResolveTime(day, service, mention);
        return understanding.Intent switch
        {
            "agendar" => ProposeNew(day, service, time, taken, clients, understanding.Phone, restText, rest.Count > 0),
            _ => new AgendaProposal(ProposalKind.FreeSlots, day, service, time, taken, null, []),
        };
    }

    // La hora que se marca. Con una hora dicha, D12 (una hora menor de 12 que no cae en el horario, pero su +12 sí, es de la
    // tarde) y si ningún hueco libre empieza a esa hora, se marca como tomada. Con solo una parte del día, el primer hueco
    // libre de esa parte (D18); si no hay ninguno, tomada y sin hora.
    private (TimeOnly? Time, bool Taken) ResolveTime(DateOnly day, string? service, WhenMention? mention)
    {
        if (mention is null || service is null)
        {
            return (null, false);
        }

        var free = FreeStarts(day, service);
        if (mention.Time is { } said)
        {
            var resolved = mention.Period == DayPeriod.Unknown && said.Hour < 12 && !OpenAt(day, said) && OpenAt(day, said.AddHours(12))
                ? said.AddHours(12)
                : said;
            return (resolved, !free.Contains(resolved));
        }

        if (mention.Period == DayPeriod.Unknown)
        {
            return (null, false);
        }

        var first = free.Where(start => InPeriod(start, mention.Period)).Cast<TimeOnly?>().FirstOrDefault();
        return first is { } start ? (start, false) : (null, true);
    }

    // Los inicios de los huecos libres del servicio ese día, en el orden del mostrador.
    private IReadOnlyList<TimeOnly> FreeStarts(DateOnly day, string service) =>
        _desk.Day(day).Free.TryGetValue(service, out var slots)
            ? slots.Select(slot => TimeOnly.FromDateTime(slot.Start.DateTime)).ToList()
            : [];

    // Si algún recurso atiende a esa hora ese día: un tramo contiene la hora si empieza en ella o antes, y termina después.
    private bool OpenAt(DateOnly day, TimeOnly hour) =>
        _desk.Setup.Resources.Any(resource => resource.HoursOn(day.DayOfWeek).Any(range => range.Start <= hour && hour < range.End));

    private static bool InPeriod(TimeOnly start, DayPeriod period) =>
        period == DayPeriod.Morning ? start < new TimeOnly(12, 0) : start >= new TimeOnly(12, 0);

    // El primer servicio, en el orden de la configuración, cuyo id o cuyas palabras de nombre están entre las palabras libres
    // o de intención. Esas palabras quedan usadas y no entran en el nombre de la persona.
    private string? MatchService(IReadOnlyList<Word> words, HashSet<int> used)
    {
        foreach (var service in _desk.Setup.Services)
        {
            if (FindWord(words, used, null, TextFold.Fold(service.Id)) is { } byId)
            {
                used.Add(byId);
                return service.Id;
            }

            if (MatchLabel(words, used, service.Label) is { } matched)
            {
                used.UnionWith(matched);
                return service.Id;
            }
        }

        return null;
    }

    private static List<int>? MatchLabel(IReadOnlyList<Word> words, HashSet<int> used, string label)
    {
        var tokens = TextTokens.Tokenize(label).Select(TextFold.Fold).ToList();
        if (tokens.Count == 0)
        {
            return null;
        }

        var matched = new List<int>();
        foreach (var token in tokens)
        {
            if (FindWord(words, used, matched, token) is not { } index)
            {
                return null;
            }

            matched.Add(index);
        }

        return matched;
    }

    private static int? FindWord(IReadOnlyList<Word> words, HashSet<int> used, IReadOnlyCollection<int>? taken, string folded)
    {
        for (var index = 0; index < words.Count; index++)
        {
            var available = !used.Contains(index) && taken?.Contains(index) != true;
            if (available && (words[index].Role is WordRole.Free or WordRole.Intent) && words[index].Folded == folded)
            {
                return index;
            }
        }

        return null;
    }

    private IReadOnlyList<Client> Lookup(string? phone, string restText, bool hasRest)
    {
        if (phone is not null)
        {
            return _desk.Search(phone);
        }

        return hasRest ? _desk.Search(restText) : [];
    }

    private static AgendaProposal ProposeNew(DateOnly day, string? service, TimeOnly? time, bool taken, IReadOnlyList<Client> clients, string? phone, string restText, bool hasRest)
    {
        if (clients.Count == 1)
        {
            return new AgendaProposal(ProposalKind.NewAppointment, day, service, time, taken, clients[0], clients);
        }

        if (clients.Count > 1)
        {
            return new AgendaProposal(ProposalKind.PickClientForNew, day, service, time, taken, null, clients);
        }

        var prefill = hasRest || phone is not null ? new Client(0, restText, phone ?? string.Empty) : null;
        return new AgendaProposal(ProposalKind.NewAppointment, day, service, time, taken, prefill, []);
    }

    private static AgendaProposal ProposeCard(DateOnly day, IReadOnlyList<Client> clients) =>
        clients.Count switch
        {
            0 => new AgendaProposal(ProposalKind.NoClient, day, null, null, false, null, []),
            1 => new AgendaProposal(ProposalKind.OpenCard, day, null, null, false, null, clients),
            _ => new AgendaProposal(ProposalKind.PickClientForCard, day, null, null, false, null, clients),
        };
}
