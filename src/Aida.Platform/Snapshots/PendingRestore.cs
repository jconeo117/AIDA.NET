namespace Aida.Platform.Snapshots;

/// <summary>Lo que pasó al arrancar con una copia preparada.</summary>
public enum PendingRestore
{
    /// <summary>No había nada preparado.</summary>
    None,

    /// <summary>Se aplicó, y lo anterior quedó apartado.</summary>
    Installed,

    /// <summary>La base ya tenía usuarios: lo preparado se apartó sin aplicarlo.</summary>
    Discarded,
}
