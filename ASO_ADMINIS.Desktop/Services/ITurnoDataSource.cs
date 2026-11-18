using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>Turnos de caja de la organización activa.</summary>
public interface ITurnoDataSource : ICrudDataSource<Turno, int>
{
    /// <summary>El turno abierto de la caja, si lo hay (a lo sumo uno).</summary>
    Turno? AbiertoDe(int cajaId);
}
