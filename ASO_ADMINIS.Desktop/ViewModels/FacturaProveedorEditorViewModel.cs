using System;
using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Alta/edición de una factura de compra. La validación de fondo (número repetido, monto,
/// vencimiento) la hace <see cref="CuentasPorPagarService"/>: el editor se la pide y muestra
/// el mensaje que devuelva.
/// </summary>
public sealed class FacturaProveedorEditorViewModel : CrudEditorViewModelBase<FacturaProveedor>
{
    private readonly FacturaProveedor _original;
    private readonly CuentasPorPagarService _servicio;

    public FacturaProveedorEditorViewModel(FacturaProveedor original,
                                           IProveedorDataSource proveedores,
                                           CuentasPorPagarService servicio)
    {
        _original = original;
        _servicio = servicio;

        Proveedores = proveedores.GetAll().Where(p => p.Activo).OrderBy(p => p.Nombre).ToList();

        NumeroDocumento = original.NumeroDocumento;
        Descripcion = original.Descripcion;
        FechaEmision = original.FechaEmision == default ? DateTime.Today : original.FechaEmision;
        FechaVencimiento = original.FechaVencimiento ?? DateTime.Today.AddDays(30);
        Monto = original.Monto == 0 ? string.Empty : original.Monto.ToString("0.##");

        ProveedorSeleccionado = Proveedores.FirstOrDefault(p => p.Id == original.ProveedorId);
    }

    public override string Titulo =>
        _original.Id == 0 ? "Registrar factura de proveedor" : $"Editar factura Nº {_original.Id}";

    public override double AnchoEditor => Ancho.Estandar;

    public IReadOnlyList<Proveedor> Proveedores { get; }

    private Proveedor? _proveedorSeleccionado;
    public Proveedor? ProveedorSeleccionado
    {
        get => _proveedorSeleccionado;
        set => SetProperty(ref _proveedorSeleccionado, value);
    }

    private string _numeroDocumento = string.Empty;
    public string NumeroDocumento
    {
        get => _numeroDocumento;
        set => SetProperty(ref _numeroDocumento, value);
    }

    private string _descripcion = string.Empty;
    public string Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    private DateTime _fechaEmision = DateTime.Today;
    public DateTime FechaEmision
    {
        get => _fechaEmision;
        set => SetProperty(ref _fechaEmision, value);
    }

    private DateTime _fechaVencimiento = DateTime.Today.AddDays(30);
    public DateTime FechaVencimiento
    {
        get => _fechaVencimiento;
        set => SetProperty(ref _fechaVencimiento, value);
    }

    private string _monto = string.Empty;
    public string Monto
    {
        get => _monto;
        set => SetProperty(ref _monto, value);
    }

    protected override bool Validar(out string? error)
    {
        if (!decimal.TryParse(Monto, out _))
        {
            error = "El monto debe ser un número.";
            return false;
        }

        return _servicio.Validar(Construir(), out error);
    }

    public override FacturaProveedor ObtenerResultado() => Construir();

    private FacturaProveedor Construir()
    {
        var factura = _original.Clonar();

        factura.NumeroDocumento = NumeroDocumento.Trim();
        factura.ProveedorId = ProveedorSeleccionado?.Id ?? 0;
        factura.ProveedorNombre = ProveedorSeleccionado?.Nombre ?? string.Empty;
        factura.Descripcion = Descripcion.Trim();
        factura.FechaEmision = FechaEmision;
        factura.FechaVencimiento = FechaVencimiento;
        factura.Monto = decimal.TryParse(Monto, out var monto) ? monto : 0m;

        return factura;
    }
}
