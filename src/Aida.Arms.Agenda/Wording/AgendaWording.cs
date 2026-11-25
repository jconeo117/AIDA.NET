using System.Collections.Frozen;
using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Arms.Agenda.Wording;

/// <summary>Los textos de la agenda, de la sección <c>agenda</c> de <c>packs/agenda/ui.yaml</c>.</summary>
/// <remarks>Es <c>AgendaWording</c> de la 0.1 (<c>app.py:52-68</c>). Nombran el oficio del mostrador, así que viven en el pack
/// y no en el código (ADR 0002). Falta un texto que pinta la pantalla y el arranque falla con la ruta: un hueco en blanco
/// se descubriría con alguien delante.</remarks>
public sealed class AgendaWording
{
    /// <summary>Las claves que la pantalla del día usa sí o sí.</summary>
    public static IReadOnlyList<string> RequiredKeys { get; } =
    [
        "title", "new_button", "today", "detail_service", "detail_resource", "detail_phone", "detail_state", "created_line",
        "state_booked", "state_cancelled", "state_attended", "state_no_show", "attended_button", "no_show_button",
        "move_button", "cancel_button", "cancel_confirm", "cancel_back", "cancel_yes", "new_title", "new_lead",
        "field_service", "field_resource", "any_resource", "free_slots", "no_slots", "field_name", "field_phone",
        "book_button", "close_button", "move_title", "reason_failed", "reason_forbidden", "reason_needs_authorization",
        "edit_button", "save_button", "block_button", "block_title", "block_lead", "field_who", "all_resources", "field_from",
        "field_until", "field_reason", "block_save", "blocked_label", "blocked_by", "unblock_button", "conflicts_title",
        "search_placeholder", "search_empty", "card_link", "card_upcoming", "card_attended", "card_no_show", "card_appointments",
        "card_edit",
        "assistant_placeholder", "assistant_button", "assistant_not_understood", "assistant_no_client", "assistant_pick_client",
        "assistant_time_taken", "assistant_past_day", "assistant_no_appointment", "assistant_pick_appointment",
    ];

    private readonly FrozenDictionary<string, string> _texts;

    private AgendaWording(FrozenDictionary<string, string> texts) => _texts = texts;

    /// <summary>Un texto por su clave.</summary>
    /// <param name="key">La clave.</param>
    /// <returns>El texto.</returns>
    /// <exception cref="KeyNotFoundException">Si la clave no está en el archivo.</exception>
    public string this[string key] => _texts[key];

    /// <summary>Lee los textos.</summary>
    /// <param name="path">La ruta de <c>ui.yaml</c>.</param>
    /// <returns>Los textos.</returns>
    /// <exception cref="InvalidDataException">Si falta la sección o alguna de <see cref="RequiredKeys"/>.</exception>
    public static AgendaWording Load(string path)
    {
        if (PlatformYaml.Child(PlatformYaml.ReadRoot(path), "agenda") is not YamlMappingNode section)
        {
            throw new InvalidDataException($"{path}: falta la sección 'agenda'");
        }

        var texts = section.Children
            .Select(pair => KeyValuePair.Create(PlatformYaml.Text(pair.Key) ?? string.Empty, PlatformYaml.Text(pair.Value)))
            .Where(pair => pair.Value is not null)
            .ToFrozenDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.Ordinal);
        var missing = RequiredKeys.Where(key => !texts.ContainsKey(key)).ToList();
        return missing.Count > 0
            ? throw new InvalidDataException($"{path}: faltan textos de la agenda: {string.Join(", ", missing)}")
            : new AgendaWording(texts);
    }

    /// <summary>El texto que explica un motivo de rechazo (<c>reason_&lt;código&gt;</c>).</summary>
    /// <param name="reason">El código del motivo.</param>
    /// <returns>El texto, o el de <c>reason_failed</c> si el motivo no tiene el suyo.</returns>
    public string Reason(string reason) => _texts.GetValueOrDefault($"reason_{reason}") ?? _texts["reason_failed"];
}
