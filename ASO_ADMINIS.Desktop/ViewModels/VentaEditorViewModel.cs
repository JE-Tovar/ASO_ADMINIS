using System;
using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Registro de una venta: un solo artículo con su cantidad, no un carrito. La validación de fondo
/// (existencia suficiente, precio) la hace <see cref="VentasService"/>: el editor se la pide y
/// muestra el mensaje que devuelva.
/// </summary>
public sealed class VentaEditorViewModel : CrudEditorViewModelBase<Venta>
{
    private readonly Venta _original;
    private readonly VentasService _servicio;
    private readonly decimal? _tasaCambio;

    /// <param name="articulos">Artículos activos con su existencia ya rellena.</param>
    public VentaEditorViewModel(Venta original,
                                IReadOnlyList<Articulo> articulos,
                                VentasService servicio,
                                decimal? tasaCambio)
    {
        _original = original;
        _servicio = servicio;
        _tasaCambio = tasaCambio;

        Articulos = articulos;

        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        Cantidad = original.Cantidad == 0 ? string.Empty : original.Cantidad.ToString("0.##");
        PrecioUnitario = original.PrecioUnitarioUsd == 0 ? string.Empty : original.PrecioUnitarioUsd.ToString("0.##");
        ClienteNombre = original.ClienteNombre;

        ArticuloSeleccionado = Articulos.FirstOrDefault(a => a.Id == original.ArticuloId);

        // Sin preselección a propósito: un método por defecto se quedaría puesto sin que nadie
        // lo mirara, y el cuadre de caja por método saldría mal.
        MetodoPagoSeleccionado = MetodosPago.FirstOrDefault(m => m.Valor == original.MetodoPago);
    }

    /// <summary>Una opción del combo: el valor y cómo se lee.</summary>
    public sealed record OpcionMetodoPago(MetodoPago Valor, string Texto);

    public IReadOnlyList<OpcionMetodoPago> MetodosPago { get; } =
        [.. Enum.GetValues<MetodoPago>().Select(m => new OpcionMetodoPago(m, Venta.TextoDe(m)))];

    private OpcionMetodoPago? _metodoPagoSeleccionado;
    public OpcionMetodoPago? MetodoPagoSeleccionado
    {
        get => _metodoPagoSeleccionado;
        set => SetProperty(ref _metodoPagoSeleccionado, value);
    }

    public override string Titulo => "Registrar venta";

    public override string TextoAccion => "Registrar venta";

    public override double AnchoEditor => Ancho.Estandar;

    public IReadOnlyList<Articulo> Articulos { get; }

    private Articulo? _articuloSeleccionado;
    public Articulo? ArticuloSeleccionado
    {
        get => _articuloSeleccionado;
        set
        {
            if (!SetProperty(ref _articuloSeleccionado, value))
                return;

            // Cada artículo trae sus presentaciones; se arranca en la unidad base, que siempre
            // existe y es la única para la mayoría de los artículos.
            Presentaciones = value?.OpcionesDePresentacion() ?? [];
            OnPropertyChanged(nameof(Presentaciones));
            OnPropertyChanged(nameof(TienePresentaciones));
            PresentacionSeleccionada = Presentaciones.FirstOrDefault();
        }
    }

    public IReadOnlyList<OpcionPresentacion> Presentaciones { get; private set; } = [];

    /// <summary>El combo solo se muestra si hay algo que elegir además de la unidad base.</summary>
    public bool TienePresentaciones => Presentaciones.Count > 1;

    private OpcionPresentacion? _presentacionSeleccionada;
    public OpcionPresentacion? PresentacionSeleccionada
    {
        get => _presentacionSeleccionada;
        set
        {
            if (!SetProperty(ref _presentacionSeleccionada, value))
                return;

            // El precio se prellena de la presentación elegida, pero sigue editable: un precio
            // distinto al de referencia (oferta, ajuste) no debería obligar a cambiar el artículo.
            if (value is not null)
                PrecioUnitario = value.PrecioVenta == 0 ? string.Empty : value.PrecioVenta.ToString("0.##");

            OnPropertyChanged(nameof(ExistenciaTexto));
            OnPropertyChanged(nameof(SePasa));
            OnPropertyChanged(nameof(TotalBsTexto));
        }
    }

    private decimal Factor => PresentacionSeleccionada?.Factor ?? 1m;

    /// <summary>La existencia en unidad base y, si se vende por presentación, su equivalente.</summary>
    public string ExistenciaTexto => ArticuloSeleccionado is not { } articulo
        ? string.Empty
        : Factor == 1m
            ? $"Disponible: {articulo.Existencia:N2} {articulo.UnidadCorta}"
            : $"Disponible: {articulo.Existencia:N2} {articulo.UnidadCorta} " +
              $"(≈ {articulo.Existencia / Factor:N2} {PresentacionSeleccionada!.Nombre})";

    /// <summary>Se pinta en rojo en la vista si se pide más de lo disponible; el servicio vuelve a comprobarlo al guardar.</summary>
    public bool SePasa => ArticuloSeleccionado is { } articulo
        && decimal.TryParse(Cantidad, out var cantidad) && cantidad * Factor > articulo.Existencia;

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
        venta.ArticuloId = ArticuloSeleccionado?.Id ?? 0;
        venta.ArticuloCodigo = ArticuloSeleccionado?.Codigo ?? string.Empty;
        venta.ArticuloNombre = ArticuloSeleccionado?.Nombre ?? string.Empty;
        venta.Cantidad = decimal.TryParse(Cantidad, out var cantidad) ? cantidad : 0m;
        venta.PresentacionNombre = PresentacionSeleccionada?.Nombre ?? string.Empty;
        venta.Factor = PresentacionSeleccionada?.Factor ?? 1m;
        venta.PrecioUnitarioUsd = decimal.TryParse(PrecioUnitario, out var precio) ? precio : 0m;
        venta.TasaCambioUsada = _tasaCambio ?? 0m;
        venta.MetodoPago = MetodoPagoSeleccionado?.Valor;
        venta.ClienteNombre = ClienteNombre.Trim();
        return venta;
    }
}
