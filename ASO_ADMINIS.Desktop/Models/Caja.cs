namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Una caja o terminal de venta de la organización ("CAJA-01"). Se da de alta en
/// Administración · Cajas y se asigna a los usuarios: la caja de una sesión sale del usuario,
/// no de la PC (ver <c>SesionActual.CajaActual</c>).
///
/// No se borra, se desactiva: las ventas, los turnos y los cierres Z la referencian. Por la
/// misma razón el <see cref="Codigo"/> solo se fija al crearla — los documentos guardan una
/// copia del código, y cambiarlo después los dejaría apuntando a un nombre que ya no existe.
/// </summary>
public class Caja : IEntidad<int>, IDeOrganizacion, IConVersion
{
    /// <summary>Concurrencia optimista (rowversion): la pone SQL Server en cada escritura.</summary>
    public byte[]? Version { get; set; }

    public int Id { get; set; }
    public int OrganizacionId { get; set; }

    /// <summary>Código corto que figura en cada documento fiscal ("CAJA-01"). Único por organización.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Cómo la llaman en la tienda ("Caja de la entrada").</summary>
    public string Nombre { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;

    public string Etiqueta => string.IsNullOrWhiteSpace(Nombre) ? Codigo : $"{Codigo} · {Nombre}";
    public string EstadoTexto => Activa ? "Activa" : "Inactiva";

    public Caja Clonar() => (Caja)MemberwiseClone();
}
