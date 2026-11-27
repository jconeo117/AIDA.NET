using System.Globalization;
using Aida.Arms.Health;
using Aida.Arms.Health.Setup;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;
using Aida.Platform.People;
using Aida.Platform.Records;

namespace Aida.Arms.Health.Actions;

/// <summary>Las reglas de cada acción de la historia clínica, detrás de la puerta del motor.</summary>
/// <remarks>
/// <para>Tres acciones, con sus rechazos en el orden de la tabla del contrato: la primera regla que falla es la que se explica
/// a quien pidió. No registra ni audita nada: eso es de la puerta y de <c>HealthDesk</c>.</para>
/// <para>El texto clínico no viaja en los argumentos (ADR 0029): <c>guardar_atencion</c> lleva solo la huella de los campos,
/// y el texto llega a la herramienta por su constructor. La herramienta comprueba que la huella es la de lo que recibió; si no
/// lo es, <c>bad_digest</c>. Así la auditoría de la puerta nunca tiene texto de los apartados.</para>
/// <para>El puesto va en el constructor y no en los argumentos: la firma lo guarda, pero no es algo que pida quien firma, sino
/// desde qué equipo se firma.</para>
/// </remarks>
internal sealed class HealthTool : IToolPort
{
    private readonly RecordStore _records;
    private readonly PersonStore _people;
    private readonly EncounterTemplate _template;
    private readonly IReadOnlyDictionary<string, string> _fields;
    private readonly string _workstation;
    private readonly TimeProvider _time;

    internal HealthTool(RecordStore records, PersonStore people, EncounterTemplate template, IReadOnlyDictionary<string, string> fields, string workstation, TimeProvider time)
    {
        _records = records;
        _people = people;
        _template = template;
        _fields = fields;
        _workstation = workstation;
        _time = time;
    }

    /// <inheritdoc/>
    public ToolResult Execute(ActionId action, IReadOnlyDictionary<string, string> arguments) => action.Value switch
    {
        "crear_atencion" => Create(arguments),
        "guardar_atencion" => Save(arguments),
        "firmar_atencion" => Sign(arguments),
        _ => ToolResult.Rejected("unknown_action"),
    };

    private ToolResult Create(IReadOnlyDictionary<string, string> arguments)
    {
        if (!long.TryParse(Text(arguments, "person"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var person)
            || _people.Find(person) is not { } found)
        {
            return ToolResult.Rejected("unknown_person");
        }

        if (found.Details is not { DocumentType: not null, DocumentNumber: not null, BirthDate: not null, Sex: not null })
        {
            return ToolResult.Rejected("incomplete_person");
        }

        var reference = Text(arguments, "reference").Trim();
        var record = _records.CreateDraft(HealthDeclarations.RecordKind, person, Text(arguments, "by"),
            reference.Length == 0 ? null : reference, _time.GetLocalNow());
        return Done(record);
    }

    private ToolResult Save(IReadOnlyDictionary<string, string> arguments)
    {
        if (Draft(arguments, out var draft) is { } rejection)
        {
            return rejection;
        }

        if (_fields.Keys.Any(key => _template.Sections.All(section => section.Id != key)))
        {
            return ToolResult.Rejected("unknown_field");
        }

        if (RecordFields.Digest(_fields) != Text(arguments, "fields_digest"))
        {
            return ToolResult.Rejected("bad_digest");
        }

        // Los campos que llegan sustituyen a los suyos; los que no llegan conservan lo que tenían.
        var merged = new Dictionary<string, string>(_records.Open(draft.Id)!.Fields, StringComparer.Ordinal);
        foreach (var (key, value) in _fields)
        {
            merged[key] = value;
        }

        _records.SaveDraft(draft.Id, merged, _time.GetLocalNow());
        return Done(draft.Id);
    }

    private ToolResult Sign(IReadOnlyDictionary<string, string> arguments)
    {
        if (Draft(arguments, out var draft) is { } rejection)
        {
            return rejection;
        }

        // Un apartado obligatorio vacío se firma con su texto de «No consta.», no se deja en blanco (ADR 0040).
        var fields = new Dictionary<string, string>(_records.Open(draft.Id)!.Fields, StringComparer.Ordinal);
        foreach (var section in _template.Sections.Where(section => section.Required))
        {
            if (!fields.TryGetValue(section.Id, out var text) || text.Trim().Length == 0)
            {
                fields[section.Id] = _template.NotRecorded;
            }
        }

        var now = _time.GetLocalNow();
        _records.SaveDraft(draft.Id, fields, now);
        _records.Sign(draft.Id, _workstation, now);
        return Done(draft.Id);
    }

    /// <summary>Comprueba que la atención existe, es de la clase y es un borrador de quien la pide.</summary>
    /// <param name="arguments">Los argumentos, con <c>record</c> y <c>by</c>.</param>
    /// <param name="draft">El resumen del borrador, si no hay rechazo.</param>
    /// <returns>El rechazo <c>unknown_record</c>, <c>not_draft</c> o <c>not_author</c>, o <see langword="null"/> si es suyo.</returns>
    private ToolResult? Draft(IReadOnlyDictionary<string, string> arguments, out RecordSummary draft)
    {
        draft = null!;
        if (!long.TryParse(Text(arguments, "record"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            || _records.Summary(id) is not { } found
            || found.Kind != HealthDeclarations.RecordKind)
        {
            return ToolResult.Rejected("unknown_record");
        }

        if (found.Status != RecordStatus.Draft)
        {
            return ToolResult.Rejected("not_draft");
        }

        if (found.Author != Text(arguments, "by"))
        {
            return ToolResult.Rejected("not_author");
        }

        draft = found;
        return null;
    }

    private static ToolResult Done(long record) =>
        ToolResult.Done(new Dictionary<string, string> { ["record"] = record.ToString(CultureInfo.InvariantCulture) });

    private static string Text(IReadOnlyDictionary<string, string> arguments, string key) =>
        arguments.TryGetValue(key, out var value) ? value : string.Empty;
}
