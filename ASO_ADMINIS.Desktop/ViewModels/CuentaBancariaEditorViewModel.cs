using System;
using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Alta/edición de una cuenta del centro. La validación de fondo (nombre repetido) la hace
/// <see cref="BancoService.ValidarCuenta"/>: el editor se la pide y muestra lo que devuelva.
/// </summary>
public sealed class CuentaBancariaEditorViewModel : CrudEditorViewModelBase<CuentaBancaria>
{
    private readonly CuentaBancaria _original;
    private readonly BancoService _servicio;

    /// <summary>
    /// El saldo inicial solo se pide al dar de alta la cuenta. Después queda de solo lectura:
    /// cambiarlo movería de un plumazo todos los saldos calculados desde entonces, sin dejar
    /// ningún asiento que explicara la diferencia. Para corregirlo se registra un movimiento de
    /// ajuste, que sí deja rastro.
    /// </summary>
    public bool PuedeEditarSaldoInicial => _original.Id == 0;

    public CuentaBancariaEditorViewModel(CuentaBancaria original, BancoService servicio)
    {
        _original = original;
        _servicio = servicio;

        Nombre = original.Nombre;
        Banco = original.Banco;
        NumeroCuenta = original.NumeroCuenta;
        Moneda = string.IsNullOrWhiteSpace(original.Moneda) ? "Bs" : original.Moneda;
        Notas = original.Notas;
        Activa = original.Id == 0 || original.Activa;
        FechaApertura = original.FechaApertura == default ? DateTime.Today : original.FechaApertura;
        SaldoInicial = original.SaldoInicial == 0 ? string.Empty : original.SaldoInicial.ToString("0.##");

        TipoSeleccionado = original.Tipo;
    }

    public override string Titulo =>
        _original.Id == 0 ? "Nueva cuenta" : $"Editar {_original.Nombre}";

    public override double AnchoEditor => Ancho.Estandar;

    public IReadOnlyList<TipoCuenta> Tipos { get; } =
        [TipoCuenta.Banco, TipoCuenta.Caja, TipoCuenta.Divisas];

    private TipoCuenta _tipoSeleccionado;
    public TipoCuenta TipoSeleccionado
    {
        get => _tipoSeleccionado;
        set => SetProperty(ref _tipoSeleccionado, value);
    }

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private string _banco = string.Empty;
    public string Banco
    {
        get => _banco;
        set => SetProperty(ref _banco, value);
    }

    private string _numeroCuenta = string.Empty;
    public string NumeroCuenta
    {
        get => _numeroCuenta;
        set => SetProperty(ref _numeroCuenta, value);
    }

    private string _moneda = "Bs";
    public string Moneda
    {
        get => _moneda;
        set => SetProperty(ref _moneda, value);
    }

    private string _saldoInicial = string.Empty;
    public string SaldoInicial
    {
        get => _saldoInicial;
        set => SetProperty(ref _saldoInicial, value);
    }

    private DateTime _fechaApertura = DateTime.Today;
    public DateTime FechaApertura
    {
        get => _fechaApertura;
        set => SetProperty(ref _fechaApertura, value);
    }

    private bool _activa = true;
    public bool Activa
    {
        get => _activa;
        set => SetProperty(ref _activa, value);
    }

    private string _notas = string.Empty;
    public string Notas
    {
        get => _notas;
        set => SetProperty(ref _notas, value);
    }

    protected override bool Validar(out string? error)
    {
        if (!string.IsNullOrWhiteSpace(SaldoInicial)
            && (!decimal.TryParse(SaldoInicial, out var saldo) || saldo < 0))
        {
            error = "El saldo inicial debe ser un número mayor o igual a cero.";
            return false;
        }

        return _servicio.ValidarCuenta(ObtenerResultado(), out error);
    }

    public override CuentaBancaria ObtenerResultado()
    {
        var cuenta = _original.Clonar();
        cuenta.Nombre = Nombre.Trim();
        cuenta.Tipo = TipoSeleccionado;
        cuenta.Banco = Banco.Trim();
        cuenta.NumeroCuenta = NumeroCuenta.Trim();
        cuenta.Moneda = Moneda.Trim();
        cuenta.Notas = Notas.Trim();
        cuenta.Activa = Activa;
        cuenta.FechaApertura = FechaApertura.Date;

        if (PuedeEditarSaldoInicial)
            cuenta.SaldoInicial = decimal.TryParse(SaldoInicial, out var saldo) ? saldo : 0m;

        return cuenta;
    }
}
