namespace Aida.Arms.Agenda.Setup;

/// <summary>La configuración de la agenda de una clínica: el hueco, los servicios y los recursos.</summary>
/// <remarks>Es <c>AgendaSetup</c> de la 0.1 (<c>setup.py:43-54</c>). Es un archivo y no código, porque cada clínica tiene sus
/// horarios; la lee <see cref="AgendaSetupLoader"/>, que garantiza que cuadra.</remarks>
/// <param name="Sector">El sector de la plantilla de la que salió (<c>consultorio</c>…).</param>
/// <param name="SlotMinutes">El largo del hueco, en minutos.</param>
/// <param name="ClientLabel">Cómo se llama a quien viene: «Paciente», «Cliente».</param>
/// <param name="Services">Los servicios, en el orden del archivo.</param>
/// <param name="Resources">Los recursos, en el orden del archivo: es el orden en que se prueban al agendar.</param>
public sealed record AgendaSetup(string Sector, int SlotMinutes, string ClientLabel, IReadOnlyList<ServiceSpec> Services, IReadOnlyList<ResourceSpec> Resources)
{
    /// <summary>Un servicio por su identificador.</summary>
    /// <param name="id">El identificador.</param>
    /// <returns>El servicio, o <see langword="null"/>.</returns>
    public ServiceSpec? Service(string id) => Services.FirstOrDefault(service => service.Id == id);

    /// <summary>Un recurso por su identificador.</summary>
    /// <param name="id">El identificador.</param>
    /// <returns>El recurso, o <see langword="null"/>.</returns>
    public ResourceSpec? Resource(string id) => Resources.FirstOrDefault(resource => resource.Id == id);
}
