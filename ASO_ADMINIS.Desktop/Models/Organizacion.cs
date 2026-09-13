namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// La empresa donde esta instalado el sistema. Una instalacion atiende a una sola organizacion,
/// y dentro de ella todas las referencias apuntan a esa misma organizacion.
///
/// Es tambien el ambito de aislamiento: cada fila de las entidades operativas le pertenece y
/// nadie ve las de otra (ver <see cref="IDeOrganizacion"/>).
/// </summary>
public class Organizacion : IEntidad<int>
{
    public int Id { get; set; }

    /// <summary>Codigo corto de uso interno, para etiquetar la instalacion (p. ej. "ADM").</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;

    public string Etiqueta => $"{Codigo} · {Nombre}";
    public string EstadoTexto => Activa ? "Activa" : "Inactiva";

    public Organizacion Clonar() => (Organizacion)MemberwiseClone();
}
