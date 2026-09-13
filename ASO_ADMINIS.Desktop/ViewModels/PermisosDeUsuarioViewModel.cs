using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Todos los permisos de UN usuario, editables en sitio. Sustituye al CRUD de ajustes sueltos:
/// para saber qué podía hacer alguien había que recordar qué trae su rol y cruzarlo a mano con
/// las filas de <see cref="PermisoUsuario"/>, dispersas en una grilla ordenada por otra cosa.
///
/// La tabla de ajustes sigue guardando SOLO deltas respecto al rol, que es lo que significa. Por
/// eso <see cref="Guardar"/> borra el ajuste cuando lo marcado vuelve a coincidir con el rol, en
/// vez de dejar una fila redundante que repita lo que ya dice la matriz.
/// </summary>
public sealed class PermisosDeUsuarioViewModel : ViewModelBase
{
    private readonly IPermisoUsuarioDataSource _ajustes;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;

    public PermisosDeUsuarioViewModel(IPermisoUsuarioDataSource ajustes,
                                      IServicioDialogo dialogos,
                                      ISesionActual sesion)
    {
        _ajustes = ajustes;
        _dialogos = dialogos;
        _sesion = sesion;

        FilasView = CollectionViewSource.GetDefaultView(Filas);
        FilasView.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(PermisoFilaViewModel.Grupo)));
        FilasView.SortDescriptions.Add(
            new SortDescription(nameof(PermisoFilaViewModel.OrdenGrupo), ListSortDirection.Ascending));
        FilasView.SortDescriptions.Add(
            new SortDescription(nameof(PermisoFilaViewModel.Grupo), ListSortDirection.Ascending));
        FilasView.SortDescriptions.Add(
            new SortDescription(nameof(PermisoFilaViewModel.Etiqueta), ListSortDirection.Ascending));

        GuardarCommand = new RelayCommand(Guardar, () => PuedeEditar && HayCambios);
    }

    public ObservableCollection<PermisoFilaViewModel> Filas { get; } = [];
    public ICollectionView FilasView { get; }

    public ICommand GuardarCommand { get; }

    private Usuario? _usuario;
    public Usuario? Usuario
    {
        get => _usuario;
        private set
        {
            if (!SetProperty(ref _usuario, value)) return;

            OnPropertyChanged(nameof(HayUsuario));
            OnPropertyChanged(nameof(Encabezado));
            OnPropertyChanged(nameof(PuedeEditar));
            OnPropertyChanged(nameof(NotaBloqueo));
            OnPropertyChanged(nameof(HayBloqueo));
        }
    }

    public bool HayUsuario => Usuario is not null;

    public string Encabezado => Usuario is { } u ? $"{u.NombreUsuario} · {u.RolTexto}" : string.Empty;

    /// <summary>
    /// Nadie ajusta sus propios permisos: revocarse el acceso a esta misma pantalla dejaría el
    /// núcleo sin quien lo administre. Mismo criterio con el que
    /// <see cref="UsuariosCrudViewModel.PuedeEliminar"/> impide borrarse a uno mismo.
    /// </summary>
    public bool PuedeEditar =>
        Usuario is { } u
        && u.Id != _sesion.UsuarioActual?.Id
        && _sesion.Puede(Permisos.Usuarios.Editar);

    public bool HayBloqueo => NotaBloqueo.Length > 0;

    /// <summary>Por qué el panel está en solo lectura, o vacío si se puede editar.</summary>
    public string NotaBloqueo
    {
        get
        {
            if (Usuario is not { } usuario)
                return string.Empty;

            if (usuario.Id == _sesion.UsuarioActual?.Id)
                return "Son tus propios permisos: los ajusta otro administrador, no tú.";

            return _sesion.Puede(Permisos.Usuarios.Editar)
                ? string.Empty
                : "No tienes permiso para ajustar permisos.";
        }
    }

    /// <summary>
    /// El conjunto efectivo se calcula una sola vez al iniciar sesión (ver
    /// <see cref="SesionActual"/>), así que un cambio hecho aquí no se nota en caliente. Decirlo
    /// junto al botón evita que alguien conceda algo y crea que no funcionó.
    /// </summary>
    public string NotaVigencia =>
        "Los cambios se aplican la próxima vez que el usuario inicie sesión.";

    public bool HayCambios => Filas.Any(f => f.Cambio);

    /// <summary>Reconstruye las filas para el usuario dado; con null deja el panel vacío.</summary>
    public void Cargar(Usuario? usuario)
    {
        foreach (var fila in Filas)
            fila.PropertyChanged -= OnFilaCambio;

        Filas.Clear();
        Usuario = usuario;

        if (usuario is null)
        {
            RefrescarEstado();
            return;
        }

        var baseDelRol = MatrizPermisos.Base(usuario.Rol);
        var propios = AjustesDe(usuario.Id);
        var puedeEditar = PuedeEditar;

        foreach (var permiso in MatrizPermisos.Todos)
        {
            var enRol = baseDelRol.Contains(permiso);
            var concedido = propios.TryGetValue(permiso, out var ajuste) ? ajuste.Concedido : enRol;

            // Nadie concede lo que no tiene: si no, un administrador se fabricaría un usuario con
            // más alcance que el suyo y entraría con él.
            var loTengo = _sesion.Puede(permiso);
            var motivo = puedeEditar && !loTengo
                ? "No puedes conceder un permiso que tú no tienes."
                : null;

            var fila = new PermisoFilaViewModel(permiso, enRol, concedido, puedeEditar && loTengo, motivo);
            fila.PropertyChanged += OnFilaCambio;
            Filas.Add(fila);
        }

        RefrescarEstado();
    }

    private void OnFilaCambio(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PermisoFilaViewModel.Concedido))
            RefrescarEstado();
    }

    private void RefrescarEstado()
    {
        OnPropertyChanged(nameof(HayCambios));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Los ajustes del usuario, por permiso. Agrupa por si hubiera duplicados viejos.</summary>
    private Dictionary<string, PermisoUsuario> AjustesDe(int usuarioId) =>
        _ajustes.GetAll()
            .Where(a => a.UsuarioId == usuarioId)
            .GroupBy(a => a.Permiso)
            .ToDictionary(g => g.Key, g => g.First());

    private void Guardar()
    {
        if (Usuario is not { } usuario || !PuedeEditar)
            return;

        var existentes = AjustesDe(usuario.Id);

        try
        {
            foreach (var fila in Filas.Where(f => f.Cambio))
            {
                existentes.TryGetValue(fila.Permiso, out var ajuste);

                if (!fila.NecesitaAjuste)
                {
                    // Vuelve a coincidir con lo que da el rol: el ajuste sobra.
                    if (ajuste is not null)
                        _ajustes.Delete(ajuste.Id);

                    continue;
                }

                if (ajuste is null)
                {
                    _ajustes.Add(new PermisoUsuario
                    {
                        UsuarioId = usuario.Id,
                        UsuarioNombre = usuario.NombreUsuario,
                        Permiso = fila.Permiso,
                        Concedido = fila.Concedido
                    });
                }
                else
                {
                    var actualizado = ajuste.Clonar();
                    actualizado.UsuarioNombre = usuario.NombreUsuario;
                    actualizado.Concedido = fila.Concedido;
                    _ajustes.Update(actualizado);
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudieron guardar los permisos", ex.Message);
            return;
        }

        Cargar(usuario);
        _dialogos.Informar("Permisos guardados", NotaVigencia);
    }
}
