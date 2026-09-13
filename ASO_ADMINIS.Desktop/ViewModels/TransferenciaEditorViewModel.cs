using System;
using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Mover dinero de una cuenta a otra: del banco a la caja chica, o al revés.
///
/// No es un movimiento con un formulario distinto, es DOS movimientos —una salida y una
/// entrada— que tienen que nacer juntos, y por eso tiene editor propio en vez de resolverse
/// tecleando dos veces en <see cref="MovimientoBancoEditorViewModel"/>: dos altas sueltas
/// admiten que alguien haga la primera y se olvide de la segunda, y el disponible total del
/// centro cambiaría sin que nadie hubiera gastado nada.
///
/// El par lo escribe <see cref="Services.BancoService.Transferir"/>, que también los enlaza.
/// </summary>
public sealed class TransferenciaEditorViewModel : CrudEditorViewModelBase
{
    public TransferenciaEditorViewModel(IReadOnlyList<CuentaBancaria> cuentas,
                                        CuentaBancaria? origenSugerido = null)
    {
        Cuentas = cuentas;
        _cuentaOrigen = origenSugerido;
    }

    public override string Titulo => "Transferir entre cuentas";

    public override string TextoAccion => "Transferir";

    public override double AnchoEditor => Ancho.Estandar;

    public IReadOnlyList<CuentaBancaria> Cuentas { get; }

    /// <summary>Con una sola cuenta no hay a dónde transferir; la vista lo explica.</summary>
    public bool HayDondeTransferir => Cuentas.Count >= 2;

    /// <summary>El negado, para el aviso de la vista: no hay converter de bool invertido.</summary>
    public bool NoHayDondeTransferir => !HayDondeTransferir;

    public string AvisoCuentasInsuficientes =>
        "Hace falta al menos otra cuenta activa para poder transferir.";

    private CuentaBancaria? _cuentaOrigen;
    public CuentaBancaria? CuentaOrigen
    {
        get => _cuentaOrigen;
        set => SetProperty(ref _cuentaOrigen, value);
    }

    private CuentaBancaria? _cuentaDestino;
    public CuentaBancaria? CuentaDestino
    {
        get => _cuentaDestino;
        set => SetProperty(ref _cuentaDestino, value);
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

    public decimal MontoValor => decimal.TryParse(Monto, out var monto) ? monto : 0m;

    protected override bool Validar(out string? error)
    {
        if (!HayDondeTransferir)
        {
            error = AvisoCuentasInsuficientes;
            return false;
        }

        if (CuentaOrigen is null)
        {
            error = "Seleccione la cuenta de origen.";
            return false;
        }

        if (CuentaDestino is null)
        {
            error = "Seleccione la cuenta de destino.";
            return false;
        }

        if (CuentaOrigen.Id == CuentaDestino.Id)
        {
            error = "La cuenta de origen y la de destino deben ser distintas.";
            return false;
        }

        if (!decimal.TryParse(Monto, out var monto) || monto <= 0)
        {
            error = "El monto debe ser un número mayor que cero.";
            return false;
        }

        // Se comprueba aquí y otra vez en el servicio: sin tasa de cambio, mover bolívares a una
        // cuenta en dólares daría un saldo que no significa nada.
        if (!string.Equals(CuentaOrigen.Moneda.Trim(), CuentaDestino.Moneda.Trim(),
                           StringComparison.OrdinalIgnoreCase))
        {
            error = $"No se puede transferir entre cuentas de distinta moneda " +
                    $"({CuentaOrigen.Moneda} y {CuentaDestino.Moneda}).";
            return false;
        }

        var apertura = CuentaOrigen.FechaApertura > CuentaDestino.FechaApertura
            ? CuentaOrigen.FechaApertura
            : CuentaDestino.FechaApertura;

        if (Fecha.Date < apertura.Date)
        {
            error = $"La fecha no puede ser anterior a la apertura de las cuentas " +
                    $"({apertura:dd/MM/yyyy}).";
            return false;
        }

        error = null;
        return true;
    }
}
