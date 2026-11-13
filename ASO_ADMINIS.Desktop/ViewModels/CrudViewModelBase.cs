using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Base para pantallas "listado con alta/edición/baja" de un catálogo maestro:
/// selección, filtro de texto y los cuatro comandos CRUD con su gating por permiso.
/// Cada maestro solo aporta los cuatro puntos de extensión de abajo.
/// </summary>
public abstract class CrudViewModelBase<T, TId> : ViewModelBase where T : IEntidad<TId>
{
    private readonly ICrudDataSource<T, TId> _source;
    private readonly IServicioDialogo _dialogo;
    private readonly ISesionActual _sesion;

    public ObservableCollection<T> Items { get; }
    public ICollectionView ItemsView { get; }

    protected CrudViewModelBase(ICrudDataSource<T, TId> source,
                                IServicioDialogo? dialogo = null,
                                ISesionActual? sesion = null)
    {
        _source = source;
        _dialogo = dialogo ?? new ServicioDialogo();
        _sesion = sesion ?? SesionActual.Instancia;

        Items = new ObservableCollection<T>(_source.GetAll());
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = FiltrarItem;

        AgregarCommand = new RelayCommand(Agregar, () => _sesion.Puede($"{ModuloPermiso}.Crear"));
        EditarCommand = new RelayCommand(Editar, () => SelectedItem is { } e && PuedeEditar(e) && _sesion.Puede($"{ModuloPermiso}.Editar"));
        EliminarCommand = new RelayCommand(Eliminar, () => SelectedItem is { } b && PuedeEliminar(b) && _sesion.Puede($"{ModuloPermiso}.Eliminar"));
        AbrirCommand = new RelayCommand(Abrir, () => SelectedItem is not null);
    }

    private T? _selectedItem;
    public T? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _textoBusqueda = string.Empty;
    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set { if (SetProperty(ref _textoBusqueda, value)) ItemsView.Refresh(); }
    }

    public ICommand AgregarCommand { get; }
    public ICommand EditarCommand { get; }
    public ICommand EliminarCommand { get; }

    /// <summary>
    /// Doble clic (o Enter) sobre una fila, ver <c>Controls/DobleClicFila</c>. Si la fila se
    /// puede editar, hace lo mismo que el botón Editar; si no, abre su ficha de solo lectura
    /// (<see cref="CrearDetalle"/>), que solo pide acceso a la pantalla, no el permiso de editar.
    /// </summary>
    public ICommand AbrirCommand { get; }

    /// <summary>Prefijo de permiso RBAC del módulo, p. ej. "Empleados" (→ "Empleados.Crear").</summary>
    protected abstract string ModuloPermiso { get; }

    protected abstract bool CoincideBusqueda(T item, string texto);
    protected abstract T CrearNuevo();
    protected abstract CrudEditorViewModelBase<T> CrearEditor(T item);

    /// <summary>
    /// Filtro adicional propio del maestro (p. ej. por categoría), que se combina con el
    /// buscador de texto. Por defecto no filtra nada. Al cambiar el criterio, llamar a
    /// <c>ItemsView.Refresh()</c>.
    /// </summary>
    protected virtual bool PasaFiltroExtra(T item) => true;

    /// <summary>
    /// ¿El elemento admite edición? Los documentos con máquina de estados quedan inmutables
    /// al confirmarse. Por defecto todo es editable (comportamiento de un maestro simple).
    /// </summary>
    protected virtual bool PuedeEditar(T item) => true;

    /// <summary>
    /// Ficha de solo lectura del elemento, para lo que no se puede editar: un documento emitido
    /// (factura, entrada, boleto) solo se ve entero aquí. Por defecto ninguna: en un maestro el
    /// doble clic abre el editor, y sin permiso de editar no hace nada.
    /// </summary>
    protected virtual CrudEditorViewModelBase? CrearDetalle(T item) => null;

    /// <summary>¿El elemento admite borrado? Ver <see cref="PuedeEditar"/>.</summary>
    protected virtual bool PuedeEliminar(T item) => true;

    /// <summary>Texto de la confirmación antes de borrar; se redefine para decir qué se conserva.</summary>
    protected virtual string MensajeConfirmarEliminar(T item) =>
        "¿Eliminar el registro seleccionado? Esta acción no se puede deshacer.";

    private bool FiltrarItem(object obj)
        => obj is T item
           && (string.IsNullOrWhiteSpace(TextoBusqueda) || CoincideBusqueda(item, TextoBusqueda.Trim()))
           && PasaFiltroExtra(item);

    // Los tres comandos guardan y no tocan la colección: de eso se encarga la recarga que
    // dispara el bus de cambios (ver CambiosDeDatos). Mantener aquí además el alta/reemplazo a
    // mano duplicaría las filas — la fila entraría una vez por Items.Add y otra por la recarga.
    // Lo único que se conserva es a QUIÉN dejar seleccionado después.

    /// <summary>
    /// Da de alta directo contra la fuente. Las pantallas cuyo documento tiene servicio de
    /// dominio lo redefinen para pasar por él: aquí no hay validación de negocio ni se dispara
    /// nada de lo que el alta arrastre en otros módulos.
    /// </summary>
    protected virtual void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());
        if (!_dialogo.MostrarEditor(editor))
            return;

        var agregado = _source.Add(editor.ObtenerResultado());
        _idASeleccionar = agregado.Id;
    }

    /// <summary>
    /// Guarda la edición directo contra la fuente. Las pantallas cuyo documento tiene servicio de
    /// dominio lo redefinen para pasar por él: aquí no hay validación de estado ni constancia de
    /// lo que cambió.
    /// </summary>
    protected virtual void Editar()
    {
        if (SelectedItem is not { } actual)
            return;

        var editor = CrearEditor(actual);
        if (!_dialogo.MostrarEditor(editor))
            return;

        var actualizado = editor.ObtenerResultado();
        _source.Update(actualizado);
        _idASeleccionar = actualizado.Id;
    }

    /// <summary>
    /// Pasa por <see cref="EditarCommand"/> y no por <see cref="Editar"/> directo: así respeta el
    /// permiso y las pantallas que redefinen Editar para ir por su servicio.
    /// </summary>
    private void Abrir()
    {
        if (EditarCommand.CanExecute(null))
        {
            EditarCommand.Execute(null);
            return;
        }

        if (SelectedItem is { } actual && CrearDetalle(actual) is { } detalle)
            _dialogo.MostrarEditor(detalle);
    }

    /// <summary>
    /// Borra la fila tras confirmar. Se redefine cuando borrar arrastra algo más —lo que cuelga
    /// del documento en otras tablas, que aquí no se conoce.
    /// </summary>
    protected virtual void Eliminar()
    {
        if (SelectedItem is not { } actual)
            return;

        if (!_dialogo.Confirmar("Eliminar", MensajeConfirmarEliminar(actual)))
            return;

        _source.Delete(actual.Id);
        _idASeleccionar = null;
        SelectedItem = default;
    }

    /// <summary>
    /// Id que debe quedar seleccionado tras la próxima recarga. Se guarda como <c>object</c>
    /// porque <c>TId</c> puede ser <c>int</c> o <c>string</c> y hace falta poder decir "ninguno"
    /// en los dos casos.
    /// </summary>
    private object? _idASeleccionar;

    /// <summary>Deja apuntado qué fila reseleccionar; lo usan las pantallas con transiciones.</summary>
    protected void SeleccionarTrasRecargar(TId id) => _idASeleccionar = id;

    /// <summary>
    /// Relee el listado.
    ///
    /// Conserva la fila seleccionada BUSCÁNDOLA POR ID, no por referencia: la recarga trae
    /// objetos nuevos y la instancia anterior ya no está en la lista. Sin esto, y como ahora la
    /// recarga es automática y no un botón, cada acción dejaría la tabla sin selección y los
    /// comandos que dependen de ella —Editar, Confirmar, Anular— se apagarían solos.
    /// </summary>
    public virtual void Recargar()
    {
        var idBuscado = _idASeleccionar ?? (SelectedItem is { } actual ? (object?)actual.Id : null);
        _idASeleccionar = null;

        Items.Clear();
        foreach (var item in _source.GetAll())
            Items.Add(item);

        // El refresco va ANTES de reseleccionar: re-aplica el filtro y puede mover el elemento
        // actual de la vista, así que hacerlo después dejaría la selección recién puesta a medias.
        ItemsView.Refresh();

        SelectedItem = idBuscado is null
            ? default
            : Items.FirstOrDefault(i => Equals(i.Id, idBuscado));

        // Los resúmenes derivados (totales, contadores) se recalculan solos al reevaluarse
        // todos los bindings; enumerarlos a mano sería una lista que se queda corta.
        OnTodasLasPropiedadesCambiaron();
    }
}
