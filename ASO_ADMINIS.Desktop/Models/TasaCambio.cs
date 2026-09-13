namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Tasa de cambio oficial (BCV) de un día: bolívares por cada dólar. Una fila por día — ver
/// <see cref="Services.TasaCambioService"/> para cómo se trae y se guarda.
///
/// Se guarda con fecha (no solo "la tasa actual") pensando en que el futuro módulo de Ventas
/// necesitará la tasa exacta del día de cada venta, no la de hoy recalculada después.
/// </summary>
public class TasaCambio : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Solo la parte de fecha; la hora no importa para esto.</summary>
    public DateTime Fecha { get; set; }

    /// <summary>Bolívares por cada dólar.</summary>
    public decimal Valor { get; set; }

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string ValorTexto => Valor.ToString("N4");

    /// <summary>Copia superficial (solo hay tipos de valor) para no mutar el original en la lista.</summary>
    public TasaCambio Clonar() => (TasaCambio)MemberwiseClone();
}
