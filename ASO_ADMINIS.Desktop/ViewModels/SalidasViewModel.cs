using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Inventario · Salidas: el historial de boletos de salida y la emisión de uno nuevo.
///
/// Misma forma que Entradas: las filas son documentos, así que no se editan ni se borran, se
/// anulan. Anular devuelve la existencia sola, porque el kardex no cuenta lo anulado.
/// </summary>
public sealed class SalidasViewModel : PantallaCrudViewModel<SalidaInventario, int>
{
    private const string FiltroTodos = "Todos";

    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly InventarioService _inventario;
    private readonly SalidasInventarioService _servicio;

    private string _filtro = FiltroTodos;

    public SalidasViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearSalidasInventario(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private SalidasViewModel(Modulo modulo,
                             Submodulo submodulo,
                             ISalidaInventarioDataSource salidas,
                             IServicioDialogo dialogos,
                             ISesionActual sesion)
        : base(modulo, submodulo, salidas, dialogos, sesion)
    {
        _dialogos = dialogos;
        _sesionActual = sesion;

        _inventario = new InventarioService(DataSourceFactory.CrearArticulos(),
                                            DataSourceFactory.CrearEntradasInventario(),
                                            salidas);

        _servicio = new SalidasInventarioService(salidas, _inventario, sesion);

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } s && _servicio.PuedeAnular(s)
                  && _sesionActual.Puede(Permisos.SalidasInventario.Anular));
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand AnularCommand { get; }

    public string Resumen =>
        $"{Items.Count(s => s.Estado == EstadoSalida.Registrada)} boletos · " +
        $"{_servicio.DelMes().Count} este mes";

    protected override string ModuloPermiso => "SalidasInventario";

    protected override bool CoincideBusqueda(SalidaInventario item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.DestinoTexto.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.RetiradoPor.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.AutorizadoPorNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Lineas.Any(l => l.ArticuloNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
                                || l.ArticuloCodigo.Contains(texto, StringComparison.OrdinalIgnoreCase));

    protected override bool PasaFiltroExtra(SalidaInventario item) => _filtro switch
    {
        "Consumo" => item.Motivo == MotivoSalida.Consumo,
        "Merma" => item.Motivo == MotivoSalida.Merma,
        "Devolución" => item.Motivo == MotivoSalida.Devolucion,
        "Traslado" => item.Motivo == MotivoSalida.Traslado,
        "Venta" => item.Motivo == MotivoSalida.Venta,
        "Anulados" => item.Estado == EstadoSalida.Anulada,
        _ => true
    };

    protected override bool PuedeEditar(SalidaInventario item) => false;

    protected override bool PuedeEliminar(SalidaInventario item) => false;

    protected override SalidaInventario CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        Estado = EstadoSalida.Registrada
    };

    protected override CrudEditorViewModelBase<SalidaInventario> CrearEditor(SalidaInventario item) =>
        new SalidaEditorViewModel(item,
                                  _inventario.ActivosConExistencia(),
                                  _sesionActual.UsuarioActual?.NombreCompleto ?? string.Empty,
                                  _servicio);

    /// <summary>
    /// La emisión pasa por el servicio de dominio: es él quien asigna el número del boleto,
    /// estampa quién autoriza y comprueba que haya existencia suficiente.
    /// </summary>
    protected override void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Registrar(editor.ObtenerResultado(),
                                          _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Anular()
    {
        if (SelectedItem is not { } salida)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular boleto {salida.Numero}",
            $"{salida.DestinoTexto} — {salida.MotivoTexto} — retiró {salida.RetiradoPor}",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(salida, editor.Motivo));
    }

    private void Aplicar(Func<SalidaInventario> transicion)
    {
        try
        {
            SeleccionarTrasRecargar(transicion().Id);
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo completar la operación", ex.Message);
        }
    }
}

/// <summary>Opción de un desplegable de enum, con su texto legible.</summary>
public sealed record OpcionAreaDestino(AreaDestino Valor, string Texto);

/// <summary>Opción de un desplegable de enum, con su texto legible.</summary>
public sealed record OpcionMotivoSalida(MotivoSalida Valor, string Texto);

/// <summary>
/// El boleto de salida. Lo que se llevaron, a qué proceso va, quién lo retira y quién lo
/// autoriza.
///
/// "Autoriza" no es un campo: se muestra de solo lectura y lo estampa el servicio con el usuario
/// de la sesión, para que no se pueda escribir otro nombre en el papel.
/// </summary>
public sealed class SalidaEditorViewModel : CrudEditorViewModelBase<SalidaInventario>
{
    private readonly SalidaInventario _original;
    private readonly SalidasInventarioService _servicio;

    public SalidaEditorViewModel(SalidaInventario original,
                                 IReadOnlyList<Articulo> articulos,
                                 string autorizadoPorNombre,
                                 SalidasInventarioService servicio)
    {
        _original = original;
        _servicio = servicio;

        Articulos = articulos;
        AutorizadoPorNombre = autorizadoPorNombre;

        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        DestinoDetalle = original.DestinoDetalle;
        RetiradoPor = original.RetiradoPor;
        Observaciones = original.Observaciones;

        AreaSeleccionada = Areas.First(a => a.Valor == original.Destino);
        MotivoSeleccionado = Motivos.First(m => m.Valor == original.Motivo);

        Lineas.CollectionChanged += AlCambiarLineas;

        AgregarLineaCommand = new RelayCommand(() => Lineas.Add(NuevaLinea()));

        QuitarLineaCommand = new RelayCommand<LineaSalidaEditorViewModel>(linea =>
        {
            if (linea is not null)
                Lineas.Remove(linea);
        });

        Lineas.Add(NuevaLinea());
    }

    public override string Titulo => "Boleto de salida";

    /// <summary>Amplio: lleva una grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Emitir boleto";

    public IReadOnlyList<Articulo> Articulos { get; }

    /// <summary>Quién autoriza: el usuario de la sesión, de solo lectura.</summary>
    public string AutorizadoPorNombre { get; }

    public IReadOnlyList<OpcionAreaDestino> Areas { get; } =
    [
        new(AreaDestino.ControlDeCalidad, "Control de calidad"),
        new(AreaDestino.Lavado, "Lavado"),
        new(AreaDestino.Empacado, "Empacado"),
        new(AreaDestino.Etiquetado, "Etiquetado"),
        new(AreaDestino.Mantenimiento, "Mantenimiento"),
        new(AreaDestino.Administracion, "Administración"),
        new(AreaDestino.Otro, "Otro")
    ];

    public IReadOnlyList<OpcionMotivoSalida> Motivos { get; } =
    [
        new(MotivoSalida.Consumo, "Consumo"),
        new(MotivoSalida.Merma, "Merma"),
        new(MotivoSalida.Devolucion, "Devolución"),
        new(MotivoSalida.Traslado, "Traslado")
    ];

    public ObservableCollection<LineaSalidaEditorViewModel> Lineas { get; } = [];

    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }

    private OpcionAreaDestino _areaSeleccionada = null!;
    public OpcionAreaDestino AreaSeleccionada
    {
        get => _areaSeleccionada;
        set
        {
            if (SetProperty(ref _areaSeleccionada, value))
                OnPropertyChanged(nameof(MuestraDetalleDestino));
        }
    }

    /// <summary>El detalle libre solo tiene sentido cuando el destino no está en la lista.</summary>
    public bool MuestraDetalleDestino => AreaSeleccionada?.Valor == AreaDestino.Otro;

    private OpcionMotivoSalida _motivoSeleccionado = null!;
    public OpcionMotivoSalida MotivoSeleccionado
    {
        get => _motivoSeleccionado;
        set => SetProperty(ref _motivoSeleccionado, value);
    }

    private DateTime _fecha = DateTime.Today;
    public DateTime Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
    }

    private string _destinoDetalle = string.Empty;
    public string DestinoDetalle
    {
        get => _destinoDetalle;
        set => SetProperty(ref _destinoDetalle, value);
    }

    private string _retiradoPor = string.Empty;
    public string RetiradoPor
    {
        get => _retiradoPor;
        set => SetProperty(ref _retiradoPor, value);
    }

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
    }

    public decimal TotalUnidades => Lineas.Sum(l => l.CantidadValor);

    public string TotalUnidadesTexto => TotalUnidades.ToString("N2");

    protected override bool Validar(out string? error)
    {
        if (Lineas.Any(l => l.ArticuloSeleccionado is not null && !l.CantidadEsValida))
        {
            error = "Hay una cantidad que no es un número válido.";
            return false;
        }

        // La autoridad es el servicio, que vuelve a mirar la existencia real en el momento de
        // guardar: entre que se abrió el boleto y se emite, otro puesto pudo haber sacado material.
        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override SalidaInventario ObtenerResultado()
    {
        var salida = _original.Clonar();
        salida.Fecha = Fecha.Date;
        salida.Destino = AreaSeleccionada.Valor;
        salida.DestinoDetalle = MuestraDetalleDestino ? DestinoDetalle.Trim() : string.Empty;
        salida.Motivo = MotivoSeleccionado.Valor;
        salida.RetiradoPor = RetiradoPor.Trim();
        salida.Observaciones = Observaciones.Trim();

        salida.Lineas = [.. Lineas
            .Where(l => l.ArticuloSeleccionado is not null)
            .Select(l => l.Construir())];

        return salida;
    }

    private LineaSalidaEditorViewModel NuevaLinea()
    {
        var linea = new LineaSalidaEditorViewModel(Articulos);
        linea.Cambio += AlCambiarLinea;
        return linea;
    }

    private void AlCambiarLineas(object? remitente, NotifyCollectionChangedEventArgs e)
    {
        foreach (var linea in e.OldItems?.OfType<LineaSalidaEditorViewModel>() ?? [])
            linea.Cambio -= AlCambiarLinea;

        AlCambiarLinea(this, EventArgs.Empty);
    }

    private void AlCambiarLinea(object? remitente, EventArgs e)
    {
        OnPropertyChanged(nameof(TotalUnidades));
        OnPropertyChanged(nameof(TotalUnidadesTexto));
    }
}

/// <summary>
/// Un renglón del boleto mientras se escribe. Como el de las entradas, pero sin precio y con la
/// existencia disponible a la vista: avisa en el acto de que se está pidiendo de más, sin esperar
/// a que el servicio rechace el boleto entero.
/// </summary>
public sealed class LineaSalidaEditorViewModel : ViewModelBase
{
    public event EventHandler? Cambio;

    public LineaSalidaEditorViewModel(IReadOnlyList<Articulo> articulos)
    {
        Articulos = articulos;
    }

    public IReadOnlyList<Articulo> Articulos { get; }

    private Articulo? _articuloSeleccionado;
    public Articulo? ArticuloSeleccionado
    {
        get => _articuloSeleccionado;
        set { if (SetProperty(ref _articuloSeleccionado, value)) Recalcular(); }
    }

    private string _cantidad = string.Empty;
    public string Cantidad
    {
        get => _cantidad;
        set { if (SetProperty(ref _cantidad, value)) Recalcular(); }
    }

    public bool CantidadEsValida =>
        !string.IsNullOrWhiteSpace(Cantidad) && decimal.TryParse(Cantidad, out var v) && v > 0;

    public decimal CantidadValor => decimal.TryParse(Cantidad, out var valor) ? valor : 0m;

    public string UnidadTexto => ArticuloSeleccionado?.UnidadCorta ?? string.Empty;

    public decimal Disponible => ArticuloSeleccionado?.Existencia ?? 0m;

    public string DisponibleTexto => ArticuloSeleccionado is { } articulo
        ? $"{articulo.Existencia:N2} {articulo.UnidadCorta}"
        : string.Empty;

    /// <summary>Se pinta en rojo en la grilla; la comprobación de verdad la hace el servicio.</summary>
    public bool SePasa => ArticuloSeleccionado is not null && CantidadValor > Disponible;

    public SalidaInventarioLinea Construir() => new()
    {
        ArticuloId = ArticuloSeleccionado!.Id,
        ArticuloCodigo = ArticuloSeleccionado.Codigo,
        ArticuloNombre = ArticuloSeleccionado.Nombre,
        UnidadTexto = ArticuloSeleccionado.UnidadCorta,
        Cantidad = CantidadValor
    };

    private void Recalcular()
    {
        OnPropertyChanged(nameof(UnidadTexto));
        OnPropertyChanged(nameof(Disponible));
        OnPropertyChanged(nameof(DisponibleTexto));
        OnPropertyChanged(nameof(SePasa));
        Cambio?.Invoke(this, EventArgs.Empty);
    }
}
