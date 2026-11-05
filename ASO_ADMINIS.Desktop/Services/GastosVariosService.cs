using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Reglas de Finanzas · Gastos varios: lo que la empresa paga y no es una factura de proveedor
/// (luz, alquiler, transporte, limpieza…).
///
/// <b>Orden de un registro:</b> validar → guardar el gasto → asentar en Banco si salió de una
/// cuenta. El asiento va después porque necesita el <c>Id</c> del gasto como origen; si el
/// asiento fallara, se retira el gasto recién creado — mismo criterio que Entradas con su
/// cuenta por pagar, porque no hay transacción que abarque las dos tablas.
///
/// <b>No se edita:</b> un gasto mal cargado se anula (con motivo) y se registra de nuevo; anular
/// anula también su asiento de Banco, así el saldo vuelve solo.
/// </summary>
public sealed class GastosVariosService
{
    private const string Prefijo = "GAS-";

    /// <summary>Las de siempre en un comercio; el combo acepta además cualquier otra.</summary>
    public static readonly IReadOnlyList<string> CategoriasBase =
    [
        "Servicios públicos",
        "Alquiler",
        "Nómina y honorarios",
        "Transporte y fletes",
        "Mantenimiento y reparaciones",
        "Impuestos y tasas",
        "Papelería y oficina",
        "Limpieza",
        "Publicidad",
        "Comisiones bancarias",
        "Otros"
    ];

    private readonly IGastoVarioDataSource _gastos;
    private readonly BancoService _banco;
    private readonly ITasaCambioDataSource _tasas;
    private readonly ISesionActual _sesion;

    public GastosVariosService(IGastoVarioDataSource gastos,
                               BancoService banco,
                               ITasaCambioDataSource tasas,
                               ISesionActual sesion)
    {
        _gastos = gastos;
        _banco = banco;
        _tasas = tasas;
        _sesion = sesion;
    }

    public bool PuedeAnular(GastoVario gasto) => gasto.Estado == EstadoGastoVario.Registrado;

    public IReadOnlyList<CuentaBancaria> CuentasActivas() => _banco.CuentasActivas();

    /// <summary>Las de base más las que ya se usaron, sin repetir ni distinguir mayúsculas.</summary>
    public IReadOnlyList<string> CategoriasSugeridas() =>
        [.. CategoriasBase
            .Concat(_gastos.GetAll().Select(g => g.Categoria.Trim()).Where(c => c.Length > 0))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// La tasa BCV vigente en una fecha: la última registrada ese día o antes. Si el gasto es
    /// anterior a la primera tasa guardada, se usa la más antigua que haya.
    /// </summary>
    public decimal TasaEn(DateTime fecha) => TasaEn(_tasas.GetAll(), fecha);

    public static decimal TasaEn(IEnumerable<TasaCambio> tasas, DateTime fecha)
    {
        var lista = tasas.Where(t => t.Valor > 0).OrderBy(t => t.Fecha).ToList();
        return lista.LastOrDefault(t => t.Fecha.Date <= fecha.Date)?.Valor
               ?? lista.FirstOrDefault()?.Valor
               ?? 0m;
    }

    /// <summary>Equivalente en Bs con la tasa que tocaría en la fecha del gasto (vista previa).</summary>
    public (decimal Tasa, decimal MontoBs) Convertir(MonedaGasto moneda, decimal monto, DateTime fecha)
    {
        if (moneda == MonedaGasto.Bs)
            return (1m, CalculoFiscal.Redondear(monto));

        var tasa = TasaEn(fecha);
        return (tasa, CalculoFiscal.Redondear(monto * tasa));
    }

    public bool Validar(GastoVario gasto, out string? error)
    {
        if (gasto.Fecha.Date > DateTime.Today)
        {
            error = "La fecha del gasto no puede ser futura.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(gasto.Categoria))
        {
            error = "Elija o escriba una categoría.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(gasto.Concepto))
        {
            error = "Indique el concepto del gasto (en qué consistió).";
            return false;
        }

        if (InventarioService.DescripcionGenerica(gasto.Concepto))
        {
            error = $"\"{gasto.Concepto.Trim()}\" es muy genérico: diga en qué consistió el gasto.";
            return false;
        }

        if (gasto.Monto <= 0)
        {
            error = "El monto debe ser mayor que cero.";
            return false;
        }

        if (gasto.Moneda == MonedaGasto.Usd && TasaEn(gasto.Fecha) <= 0)
        {
            error = "No hay tasa de cambio BCV registrada para pasar el gasto a bolívares: " +
                    "actualícela en Administración · Organización.";
            return false;
        }

        if (gasto.CuentaId is { } cuentaId)
        {
            var cuenta = _banco.CuentasActivas().FirstOrDefault(c => c.Id == cuentaId);
            if (cuenta is null)
            {
                error = "La cuenta elegida ya no está activa.";
                return false;
            }

            if (!GastoVario.MonedaCoincide(gasto.Moneda, cuenta.Moneda))
            {
                error = $"La cuenta {cuenta.NombreConMoneda} no es en {GastoVario.TextoDe(gasto.Moneda)}: " +
                        "elija una cuenta de la misma moneda o pague en efectivo.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>Numera, congela la tasa y el monto en Bs, guarda y asienta en Banco si hace falta.</summary>
    public GastoVario Registrar(GastoVario borrador, int usuarioId)
    {
        if (!_sesion.Puede(Permisos.GastosVarios.Crear))
            throw new InvalidOperationException("No tienes permiso para registrar gastos varios.");

        var gasto = borrador.Clonar();
        gasto.Id = 0;
        gasto.Fecha = gasto.Fecha.Date;
        gasto.Categoria = gasto.Categoria.Trim();
        gasto.Concepto = gasto.Concepto.Trim();
        gasto.Beneficiario = gasto.Beneficiario.Trim();
        gasto.Referencia = gasto.Referencia.Trim();
        gasto.Monto = CalculoFiscal.Redondear(gasto.Monto);

        if (!Validar(gasto, out var error))
            throw new InvalidOperationException(error);

        (gasto.TasaCambio, gasto.MontoBs) = Convertir(gasto.Moneda, gasto.Monto, gasto.Fecha);

        gasto.CuentaNombre = gasto.CuentaId is { } id
            ? _banco.CuentasActivas().First(c => c.Id == id).NombreConMoneda
            : string.Empty;
        gasto.Estado = EstadoGastoVario.Registrado;
        gasto.Numero = SiguienteNumero();
        gasto.CreadoPorId = usuarioId;
        gasto.CreadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty;
        gasto.FechaCreacion = DateTime.Now;

        var guardado = _gastos.Add(gasto);

        if (guardado.CuentaId is { } cuentaId)
        {
            try
            {
                _banco.RegistrarGastoVario(guardado, new AsientoBanco(cuentaId, guardado.Fecha, guardado.Referencia), usuarioId);
            }
            catch (Exception)
            {
                // Sin asiento el gasto mentiría sobre el saldo de la cuenta: se retira.
                _gastos.Delete(guardado.Id);
                throw;
            }
        }

        return guardado;
    }

    public GastoVario Anular(GastoVario gasto, string motivo)
    {
        if (!_sesion.Puede(Permisos.Finanzas.Anular))
            throw new InvalidOperationException("No tienes permiso para anular gastos.");

        if (!PuedeAnular(gasto))
            throw new InvalidOperationException("Este gasto ya está anulado.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        var copia = gasto.Clonar();
        copia.Estado = EstadoGastoVario.Anulado;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _gastos.Update(copia);

        if (copia.CuentaId is not null)
            _banco.AnularDerivados(OrigenMovimiento.GastoVario, copia.Id, $"Gasto {copia.Numero} anulado: {copia.MotivoAnulacion}");

        return copia;
    }

    private string SiguienteNumero()
    {
        var ultimo = _gastos.GetAll()
            .Select(g => g.Numero)
            .Where(n => n.StartsWith(Prefijo, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[Prefijo.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{Prefijo}{ultimo + 1:D6}";
    }
}
