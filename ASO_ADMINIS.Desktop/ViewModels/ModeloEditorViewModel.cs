using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>Opción de un desplegable de enum, con su texto legible (mismo arquetipo que <see cref="OpcionRol"/>).</summary>
public sealed record OpcionCategoriaProducto(CategoriaProducto Valor, string Texto);

/// <summary>
/// Alta/edición de un modelo. La validación de fondo (marca activa, precio) la hace
/// <see cref="CatalogoService"/>: el editor se la pide y muestra el mensaje que devuelva.
///
/// La marca no tiene pantalla propia: se crea y se edita inline desde aquí, con los botones
/// "+ Nueva"/"Editar" junto al combo — ver <see cref="NuevaMarcaCommand"/>/<see cref="EditarMarcaCommand"/>.
/// </summary>
public sealed class ModeloEditorViewModel : CrudEditorViewModelBase<Modelo>
{
    private readonly Modelo _original;
    private readonly CatalogoService _servicio;
    private readonly IMarcaDataSource _marcasDataSource;
    private readonly IServicioDialogo _dialogos;

    public ModeloEditorViewModel(Modelo original,
                                 IReadOnlyList<Marca> marcas,
                                 CatalogoService servicio,
                                 IMarcaDataSource marcasDataSource,
                                 IServicioDialogo dialogos)
    {
        _original = original;
        _servicio = servicio;
        _marcasDataSource = marcasDataSource;
        _dialogos = dialogos;

        Marcas = new ObservableCollection<Marca>(marcas);

        Nombre = original.Nombre;
        Notas = original.Notas;
        PrecioVenta = original.PrecioVenta == 0 ? string.Empty : original.PrecioVenta.ToString("0.##");
        Activo = original.Id == 0 || original.Activo;
        CategoriaSeleccionada = CategoriasDisponibles.FirstOrDefault(o => o.Valor == original.Categoria)
            ?? CategoriasDisponibles[0];
        UnidadSeleccionada = original.Id == 0 ? UnidadMedida.Par : original.Unidad;

        MarcaSeleccionada = Marcas.FirstOrDefault(m => m.Id == original.MarcaId)
            ?? (Marcas.Count == 1 ? Marcas[0] : null);

        NuevaMarcaCommand = new RelayCommand(NuevaMarca);
        EditarMarcaCommand = new RelayCommand(EditarMarca, () => MarcaSeleccionada is not null);
    }

    public override string Titulo => _original.Id == 0 ? "Nuevo modelo" : $"Editar {_original.Nombre}";

    public override double AnchoEditor => Ancho.Estandar;

    public ObservableCollection<Marca> Marcas { get; }

    public ICommand NuevaMarcaCommand { get; }
    public ICommand EditarMarcaCommand { get; }

    public IReadOnlyList<OpcionCategoriaProducto> CategoriasDisponibles { get; } =
    [
        new(CategoriaProducto.Calzado, "Calzado"),
        new(CategoriaProducto.Ropa, "Ropa"),
        new(CategoriaProducto.CorreasYCarteras, "Correas y carteras"),
        new(CategoriaProducto.Otro, "Otro")
    ];

    public IReadOnlyList<UnidadMedida> Unidades { get; } = [UnidadMedida.Par, UnidadMedida.Pieza];

    private Marca? _marcaSeleccionada;
    public Marca? MarcaSeleccionada
    {
        get => _marcaSeleccionada;
        set
        {
            if (SetProperty(ref _marcaSeleccionada, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private OpcionCategoriaProducto _categoriaSeleccionada = null!;
    public OpcionCategoriaProducto CategoriaSeleccionada
    {
        get => _categoriaSeleccionada;
        set => SetProperty(ref _categoriaSeleccionada, value);
    }

    private UnidadMedida _unidadSeleccionada;
    public UnidadMedida UnidadSeleccionada
    {
        get => _unidadSeleccionada;
        set => SetProperty(ref _unidadSeleccionada, value);
    }

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private string _precioVenta = string.Empty;
    public string PrecioVenta
    {
        get => _precioVenta;
        set => SetProperty(ref _precioVenta, value);
    }

    private string _notas = string.Empty;
    public string Notas
    {
        get => _notas;
        set => SetProperty(ref _notas, value);
    }

    private bool _activo = true;
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    /// <summary>Da de alta una marca sin salir del editor de modelo y la deja seleccionada.</summary>
    private void NuevaMarca()
    {
        var editor = new MarcaEditorViewModel(new Marca { Activo = true }, _marcasDataSource);
        if (!_dialogos.MostrarEditor(editor))
            return;

        var creada = _marcasDataSource.Add(editor.ObtenerResultado());
        Marcas.Add(creada);
        MarcaSeleccionada = creada;
    }

    /// <summary>
    /// Edita la marca elegida en el combo. Se reemplaza por índice en vez de mutar en sitio
    /// porque <see cref="Marca"/> no notifica cambios (mismo motivo por el que Almacén refresca
    /// a mano tras rellenar existencias) — sin esto el combo seguiría mostrando el nombre viejo.
    /// </summary>
    private void EditarMarca()
    {
        if (MarcaSeleccionada is not { } actual)
            return;

        var editor = new MarcaEditorViewModel(actual, _marcasDataSource);
        if (!_dialogos.MostrarEditor(editor))
            return;

        var actualizada = editor.ObtenerResultado();
        _marcasDataSource.Update(actualizada);

        var indice = Marcas.IndexOf(actual);
        if (indice >= 0)
            Marcas[indice] = actualizada;

        MarcaSeleccionada = actualizada;
    }

    protected override bool Validar(out string? error)
    {
        if (!string.IsNullOrWhiteSpace(PrecioVenta) && !decimal.TryParse(PrecioVenta, out _))
        {
            error = "El precio de venta debe ser un número.";
            return false;
        }

        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override Modelo ObtenerResultado()
    {
        var modelo = _original.Clonar();
        modelo.MarcaId = MarcaSeleccionada?.Id ?? 0;
        modelo.MarcaNombre = MarcaSeleccionada?.Nombre ?? string.Empty;
        modelo.Nombre = Nombre.Trim();
        modelo.Categoria = CategoriaSeleccionada.Valor;
        modelo.Unidad = UnidadSeleccionada;
        modelo.PrecioVenta = decimal.TryParse(PrecioVenta, out var precio) ? precio : 0m;
        modelo.Notas = Notas.Trim();
        modelo.Activo = Activo;
        return modelo;
    }
}
