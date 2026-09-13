using System;
using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Alta/edición de un movimiento tecleado a mano: la comisión que cobró el banco, un retiro, un
/// aporte de capital, el anticipo que se le adelantó a alguien en efectivo.
///
/// Solo llega aquí lo que NO viene de un documento. Un cobro o un pago se registran desde su
/// factura y bajan solos al libro; este editor no los ofrece ni los deja corregir.
/// </summary>
public sealed class MovimientoBancoEditorViewModel : CrudEditorViewModelBase<MovimientoBanco>
{
    private readonly MovimientoBanco _original;
    private readonly BancoService _servicio;

    public MovimientoBancoEditorViewModel(MovimientoBanco original,
                                          IReadOnlyList<CuentaBancaria> cuentas,
                                          BancoService servicio)
    {
        _original = original;
        _servicio = servicio;

        Cuentas = cuentas;

        Concepto = original.Concepto;
        Referencia = original.Referencia;
        Monto = original.Monto == 0 ? string.Empty : original.Monto.ToString("0.##");
        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        EsSalida = original.Tipo == TipoMovimientoBanco.Salida;
        CategoriaSeleccionada = original.Categoria;

        _cuentaSeleccionada = BuscarCuenta(original.CuentaId) ?? (cuentas.Count == 1 ? cuentas[0] : null);
    }

    public override string Titulo =>
        _original.Id == 0 ? "Nuevo movimiento" : $"Editar movimiento Nº {_original.Id}";

    public override double AnchoEditor => Ancho.Estandar;

    public IReadOnlyList<CuentaBancaria> Cuentas { get; }

    /// <summary>
    /// Las categorías que tiene sentido teclear. Falta a propósito la que solo produce el
    /// sistema —pago a proveedor—: ofrecerla invitaría a duplicar a mano un asiento que ya baja
    /// solo desde su documento, y el saldo contaría el mismo dinero dos veces. Transferencia
    /// tampoco: nace de <see cref="BancoService.Transferir"/>, no de este editor.
    /// </summary>
    public IReadOnlyList<CategoriaMovimiento> Categorias { get; } =
    [
        CategoriaMovimiento.ComisionBancaria,
        CategoriaMovimiento.Impuesto,
        CategoriaMovimiento.GastoVario,
        CategoriaMovimiento.AporteCapital,
        CategoriaMovimiento.Retiro,
        CategoriaMovimiento.Otro
    ];

    private CategoriaMovimiento _categoriaSeleccionada = CategoriaMovimiento.GastoVario;
    public CategoriaMovimiento CategoriaSeleccionada
    {
        get => _categoriaSeleccionada;
        set => SetProperty(ref _categoriaSeleccionada, value);
    }

    private CuentaBancaria? _cuentaSeleccionada;
    public CuentaBancaria? CuentaSeleccionada
    {
        get => _cuentaSeleccionada;
        set => SetProperty(ref _cuentaSeleccionada, value);
    }

    /// <summary>
    /// Entrada o salida, como un par de opciones y no como un desplegable de dos: es la decisión
    /// que más cambia el significado de la pantalla y conviene que se vea sin abrir nada.
    /// </summary>
    private bool _esSalida = true;
    public bool EsSalida
    {
        get => _esSalida;
        set
        {
            if (SetProperty(ref _esSalida, value))
                OnPropertyChanged(nameof(EsEntrada));
        }
    }

    public bool EsEntrada
    {
        get => !EsSalida;
        set { if (value) EsSalida = false; }
    }

    private string _concepto = string.Empty;
    public string Concepto
    {
        get => _concepto;
        set => SetProperty(ref _concepto, value);
    }

    private string _referencia = string.Empty;
    public string Referencia
    {
        get => _referencia;
        set => SetProperty(ref _referencia, value);
    }

    private string _monto = string.Empty;
    public string Monto
    {
        get => _monto;
        set => SetProperty(ref _monto, value);
    }

    private DateTime _fecha = DateTime.Today;
    public DateTime Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
    }

    protected override bool Validar(out string? error)
    {
        if (!decimal.TryParse(Monto, out var monto) || monto <= 0)
        {
            error = "El monto debe ser un número mayor que cero.";
            return false;
        }

        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override MovimientoBanco ObtenerResultado()
    {
        var movimiento = _original.Clonar();

        movimiento.CuentaId = CuentaSeleccionada?.Id ?? 0;
        movimiento.CuentaNombre = CuentaSeleccionada?.Nombre ?? string.Empty;
        movimiento.Fecha = Fecha.Date;
        movimiento.Tipo = EsSalida ? TipoMovimientoBanco.Salida : TipoMovimientoBanco.Entrada;
        movimiento.Monto = decimal.TryParse(Monto, out var monto) ? monto : 0m;
        movimiento.Concepto = Concepto.Trim();
        movimiento.Referencia = Referencia.Trim();
        movimiento.Categoria = CategoriaSeleccionada;

        // Lo que sale de este editor es manual por definición; un asiento derivado no llega aquí.
        movimiento.Origen = OrigenMovimiento.Manual;
        movimiento.OrigenId = null;

        return movimiento;
    }

    private CuentaBancaria? BuscarCuenta(int id)
    {
        foreach (var cuenta in Cuentas)
        {
            if (cuenta.Id == id)
                return cuenta;
        }

        return null;
    }
}
