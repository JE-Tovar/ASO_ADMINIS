namespace ASO_ADMINIS.Desktop.Models;

public enum EstadoTurno
{
    Abierto,
    Cerrado
}

/// <summary>
/// El turno de un cajero en su caja: se abre con un fondo en efectivo y se cierra con el arqueo
/// (lo que se contó contra lo que debía haber). Toda factura emitida queda ligada al turno
/// abierto en ese momento (<see cref="Venta.TurnoId"/>).
///
/// Una caja tiene a lo sumo UN turno abierto. Lo hace cumplir <c>TurnosService.Abrir</c> y, por
/// si dos PCs lo intentaran a la vez, un índice único filtrado en la base (ver
/// <c>AsoAdminisDbContext</c>).
///
/// Lo esperado se calcula y se guarda al cerrar (snapshot): una nota de crédito posterior no
/// debe cambiar un arqueo ya firmado.
/// </summary>
public class Turno : IEntidad<int>, IDeOrganizacion, IConVersion
{
    /// <summary>Concurrencia optimista (rowversion): la pone SQL Server en cada escritura.</summary>
    public byte[]? Version { get; set; }

    public int Id { get; set; }
    public int OrganizacionId { get; set; }

    public int CajaId { get; set; }
    public string CajaCodigo { get; set; } = string.Empty;   // snapshot

    /// <summary>El cajero dueño del turno.</summary>
    public int UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;   // snapshot

    public EstadoTurno Estado { get; set; } = EstadoTurno.Abierto;

    // ---- Apertura ----
    public DateTime Apertura { get; set; }
    public decimal FondoBs { get; set; }
    public decimal FondoDivisa { get; set; }

    // ---- Cierre (arqueo) ----
    public DateTime? Cierre { get; set; }
    public decimal EsperadoBs { get; set; }
    public decimal EsperadoDivisa { get; set; }
    public decimal ContadoBs { get; set; }
    public decimal ContadoDivisa { get; set; }

    /// <summary>Quién lo cerró: el propio cajero o, si se fue sin cerrar, un supervisor.</summary>
    public int? CerradoPorId { get; set; }
    public string CerradoPorNombre { get; set; } = string.Empty;

    public string Observaciones { get; set; } = string.Empty;

    // ---- Derivados ----

    public bool EstaAbierto => Estado == EstadoTurno.Abierto;

    /// <summary>Contado − esperado. Positivo: sobra; negativo: falta.</summary>
    public decimal DiferenciaBs => ContadoBs - EsperadoBs;
    public decimal DiferenciaDivisa => ContadoDivisa - EsperadoDivisa;

    public string EstadoTexto => EstaAbierto ? "Abierto" : "Cerrado";
    public string AperturaTexto => Apertura.ToString("dd/MM/yyyy HH:mm");
    public string CierreTexto => Cierre?.ToString("dd/MM/yyyy HH:mm") ?? "—";
    public string FondoTexto => FondoDivisa > 0 ? $"{FondoBs:N2}  (US$ {FondoDivisa:N2})" : FondoBs.ToString("N2");

    public string DiferenciaTexto => EstaAbierto
        ? "—"
        : DiferenciaDivisa != 0
            ? $"{DiferenciaBs:N2}  (US$ {DiferenciaDivisa:N2})"
            : DiferenciaBs.ToString("N2");

    public bool Cuadra => !EstaAbierto && DiferenciaBs == 0 && DiferenciaDivisa == 0;

    public Turno Clonar() => (Turno)MemberwiseClone();
}
