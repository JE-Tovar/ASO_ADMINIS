using System;
using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Registro de una venta: un solo modelo con su cantidad, no un carrito. La validación de fondo
/// (existencia suficiente, precio) la hace <see cref="VentasService"/>: el editor se la pide y
/// muestra el mensaje que devuelva.
/// </summary>
public sealed class VentaEditorViewModel : CrudEditorViewModelBase<Venta>
{
    private readonly Venta _original;
    private readonly VentasService _servicio;
    private readonly IReadOnlyDictionary<int, Articulo> _articulosPorModelo;
    private readonly decimal? _tasaCambio;

    public VentaEditorViewModel(Venta original,
                                IReadOnlyList<Modelo> modelos,
                                IReadOnlyDictionary<int, Articulo> articulosPorModelo,
                                VentasService servicio,
                                decimal? tasaCambio)
    {
        _original = original;
        _servicio = servicio;
        _articulosPorModelo = articulosPorModelo;
        _tasaCambio = tasaCambio;

        Modelos = modelos;

        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        Cantidad = original.Cantidad == 0 ? string.Empty : original.Cantidad.ToString("0.##");
        PrecioUnitario = original.PrecioUnitarioUsd == 0 ? string.Empty : original.PrecioUnitarioUsd.ToString("0.##");
        ClienteNombre = original.ClienteNombre;

        ModeloSeleccionado = Modelos.FirstOrDefault(m => m.Id == original.ModeloId);
    }

    public override string Titulo => "Registrar venta";

    public override string TextoAccion => "Registrar venta";

    public override double AnchoEditor => Ancho.Estandar;

    public IReadOnlyList<Modelo> Modelos { get; }

    private Modelo? _modeloSeleccionado;
    public Modelo? ModeloSeleccionado
    {
        get => _modeloSeleccionado;
        set
        {
            if (!SetProperty(ref _modeloSeleccionado, value))
                return;

            // El precio se prellena del modelo elegido, pero sigue editable: un precio distinto
            // al del catálogo (oferta, ajuste) no debería obligar a ir a cambiar el modelo antes.
            if (value is not null)
                PrecioUnitario = value.PrecioVenta == 0 ? string.Empty : value.PrecioVenta.ToString("0.##");

            OnPropertyChanged(nameof(ArticuloSeleccionado));
            OnPropertyChanged(nameof(ExistenciaTexto));
            OnPropertyChanged(nameof(TotalBsTexto));
        }
    }

    /// <summary>El artículo de Inventario vinculado al modelo elegido, con su existencia ya rellena.</summary>
    public Articulo? ArticuloSeleccionado =>
        ModeloSeleccionado is { } modelo ? _articulosPorModelo.GetValueOrDefault(modelo.Id) : null;

    public string ExistenciaTexto => ArticuloSeleccionado is { } articulo
        ? $"Disponible: {articulo.Existencia:N2} {articulo.UnidadCorta}"
        : string.Empty;

    /// <summary>Se pinta en rojo en la vista si se pide más de lo disponible; el servicio vuelve a comprobarlo al guardar.</summary>
    public bool SePasa => ArticuloSeleccionado is { } articulo
        && decimal.TryParse(Cantidad, out var cantidad) && cantidad > articulo.Existencia;

    private DateTime _fecha = DateTime.Today;
    public DateTime Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
    }

    private string _cantidad = string.Empty;
    public string Cantidad
    {
        get => _cantidad;
        set
        {
            if (SetProperty(ref _cantidad, value))
            {
                OnPropertyChanged(nameof(TotalBsTexto));
                OnPropertyChanged(nameof(SePasa));
            }
        }
    }

    private string _precioUnitario = string.Empty;
    public string PrecioUnitario
    {
        get => _precioUnitario;
        set
        {
            if (SetProperty(ref _precioUnitario, value))
                OnPropertyChanged(nameof(TotalBsTexto));
        }
    }

    private string _clienteNombre = string.Empty;
    public string ClienteNombre
    {
        get => _clienteNombre;
        set => SetProperty(ref _clienteNombre, value);
    }

    /// <summary>Vista previa en vivo del total en bolívares, con la tasa vigente al abrir el editor.</summary>
    public string TotalBsTexto =>
        _tasaCambio is { } tasa
        && decimal.TryParse(Cantidad, out var cantidad) && decimal.TryParse(PrecioUnitario, out var precio)
        && cantidad > 0 && precio > 0
            ? $"≈ Bs. {(cantidad * precio * tasa):N2}"
            : _tasaCambio is null
                ? "Sin tasa de cambio registrada todavía."
                : string.Empty;

    protected override bool Validar(out string? error)
    {
        if (!decimal.TryParse(Cantidad, out var cantidad) || cantidad <= 0)
        {
            error = "La cantidad debe ser un número mayor que cero.";
            return false;
        }

        if (!decimal.TryParse(PrecioUnitario, out var precio) || precio < 0)
        {
            error = "El precio debe ser un número mayor o igual a cero.";
            return false;
        }

        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override Venta ObtenerResultado()
    {
        var venta = _original.Clonar();
        venta.Fecha = Fecha.Date;
        venta.ModeloId = ModeloSeleccionado?.Id ?? 0;
        venta.ModeloNombre = ModeloSeleccionado?.Nombre ?? string.Empty;
        venta.MarcaNombre = ModeloSeleccionado?.MarcaNombre ?? string.Empty;
        venta.Cantidad = decimal.TryParse(Cantidad, out var cantidad) ? cantidad : 0m;
        venta.PrecioUnitarioUsd = decimal.TryParse(PrecioUnitario, out var precio) ? precio : 0m;
        venta.TasaCambioUsada = _tasaCambio ?? 0m;
        venta.ClienteNombre = ClienteNombre.Trim();
        return venta;
    }
}
