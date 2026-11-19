namespace Aida.Arms.Agenda.Setup;

/// <summary>Un servicio que se agenda: su identificador, cómo se llama y cuánto dura.</summary>
/// <remarks>Es <c>ServiceSpec</c> de la 0.1 (<c>setup.py:26-30</c>). La duración la pone el servicio y no quien agenda: así
/// dos citas del mismo servicio ocupan lo mismo.</remarks>
/// <param name="Id">El identificador, como lo escribe el archivo.</param>
/// <param name="Label">El nombre que ve el personal.</param>
/// <param name="Minutes">La duración, en minutos; mayor que cero.</param>
public sealed record ServiceSpec(string Id, string Label, int Minutes);
