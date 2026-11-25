using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Text;
using Aida.Platform.Assistant;

namespace Aida.Arms.Agenda.Assistant;

/// <summary>Convierte lo entendido en una propuesta para la pantalla de la agenda.</summary>
/// <remarks>
/// Solo lee: la configuración, la búsqueda y la ficha del mostrador. El cambio lo hace el operador con el botón de siempre,
/// que pasa por la puerta (ADR 0007, ADR 0037). Si encaja más de una persona, las devuelve para que elija el operador:
/// nunca elige él.
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
        var day = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var words = understanding.Words;
        var mentionsTime = words.Any(word => word.Role == WordRole.Time);
        if (understanding.Intent is null)
        {
            return new AgendaProposal(ProposalKind.NotUnderstood, day, null, null, [], mentionsTime);
        }

        var used = new HashSet<int>();
        var service = MatchService(words, used);
        var rest = Enumerable.Range(0, words.Count)
            .Where(index => words[index].Role == WordRole.Free && !used.Contains(index))
            .ToList();
        var restText = string.Join(' ', rest.Select(index => words[index].Text));
        var clients = Lookup(understanding.Phone, restText, rest.Count > 0);
        return understanding.Intent switch
        {
            "agendar" => ProposeNew(day, service, clients, understanding.Phone, restText, rest.Count > 0, mentionsTime),
            "ver_huecos" => new AgendaProposal(ProposalKind.FreeSlots, day, service, null, [], mentionsTime),
            "cancelar" or "reprogramar" => ProposeCard(day, clients, mentionsTime),
            _ => new AgendaProposal(ProposalKind.NotUnderstood, day, null, null, [], mentionsTime),
        };
    }

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

    private static AgendaProposal ProposeNew(DateOnly day, string? service, IReadOnlyList<Client> clients, string? phone, string restText, bool hasRest, bool mentionsTime)
    {
        if (clients.Count == 1)
        {
            return new AgendaProposal(ProposalKind.NewAppointment, day, service, clients[0], clients, mentionsTime);
        }

        if (clients.Count > 1)
        {
            return new AgendaProposal(ProposalKind.PickClientForNew, day, service, null, clients, mentionsTime);
        }

        var prefill = hasRest || phone is not null ? new Client(0, restText, phone ?? string.Empty) : null;
        return new AgendaProposal(ProposalKind.NewAppointment, day, service, prefill, [], mentionsTime);
    }

    private static AgendaProposal ProposeCard(DateOnly day, IReadOnlyList<Client> clients, bool mentionsTime) =>
        clients.Count switch
        {
            0 => new AgendaProposal(ProposalKind.NoClient, day, null, null, [], mentionsTime),
            1 => new AgendaProposal(ProposalKind.OpenCard, day, null, null, clients, mentionsTime),
            _ => new AgendaProposal(ProposalKind.PickClientForCard, day, null, null, clients, mentionsTime),
        };
}
