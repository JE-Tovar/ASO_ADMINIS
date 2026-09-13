using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Bandeja de peticiones de cambio.
///
/// El remesero entra y ve las suyas; el administrador ve las de su núcleo y las resuelve.
/// Aprobar aquí NO ejecuta el cambio: deja constancia de la autorización y el administrador
/// hace la acción en la pantalla que corresponde, con sus reglas intactas. El texto de la
/// vista lo dice, para que nadie espere que la remesa se anule sola.
/// </summary>
public sealed class PeticionesViewModel : PantallaViewModelBase
{
    private readonly IPeticionCambioDataSource _fuente;
    private readonly PeticionService _servicio;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;

    private string _filtroEstado = "Pendientes";

    public PeticionesViewModel(Modulo modulo)
        : this(modulo, DataSourceFactory.CrearPeticiones(), new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    public PeticionesViewModel(Modulo modulo,
                               IPeticionCambioDataSource fuente,
                               IServicioDialogo dialogos,
                               ISesionActual sesion)
        : base(modulo)
    {
        _fuente = fuente;
        _servicio = new PeticionService(fuente, sesion);
        _dialogos = dialogos;
        _sesion = sesion;

        Peticiones = new ObservableCollection<PeticionCambio>(_fuente.GetAll());
        PeticionesView = CollectionViewSource.GetDefaultView(Peticiones);
        PeticionesView.Filter = Filtrar;

        AprobarCommand = new RelayCommand(Aprobar, PuedeResolverSeleccionada);
        RechazarCommand = new RelayCommand(Rechazar, PuedeResolverSeleccionada);

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtroEstado = filtro;
            PeticionesView.Refresh();
        });
    }

    public ObservableCollection<PeticionCambio> Peticiones { get; }
    public ICollectionView PeticionesView { get; }

    private PeticionCambio? _seleccionada;
    public PeticionCambio? Seleccionada
    {
        get => _seleccionada;
        set
        {
            if (SetProperty(ref _seleccionada, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand AprobarCommand { get; }
    public ICommand RechazarCommand { get; }
    public ICommand CambiarFiltroCommand { get; }

    /// <summary>Aviso permanente en la pantalla: aprobar autoriza, no ejecuta.</summary>
    public string NotaAprobacion =>
        "Aprobar deja constancia de la autorización; el cambio hay que hacerlo después en la " +
        "pantalla correspondiente, para que sus validaciones se sigan aplicando.";

    public int Pendientes => Peticiones.Count(p => p.EstaPendiente);
    public string ResumenPendientes =>
        Pendientes == 1 ? "1 petición pendiente" : $"{Pendientes} peticiones pendientes";

    private bool PuedeResolverSeleccionada() =>
        Seleccionada is { } p
        && _servicio.PuedeResolver(p)
        && _sesion.Puede(Permisos.Peticiones.Resolver)
        // Solo las de su dominio: quien aprueba es quien tiene que ejecutar el cambio despues.
        && _servicio.EsDeSuDominio(p)
        && p.SolicitadoPorId != _sesion.UsuarioActual?.Id;

    private bool Filtrar(object item) =>
        item is PeticionCambio p &&
        _filtroEstado switch
        {
            "Pendientes" => p.EstaPendiente,
            "Resueltas" => !p.EstaPendiente,
            _ => true
        };

    /// <summary>
    /// Relee la bandeja conservando la fila seleccionada.
    ///
    /// Es la pantalla donde mas se notaria un sondeo periodico: las peticiones las crea OTRO
    /// usuario, y un bus en proceso no puede enterarse de lo que escribe otra maquina. Ver la
    /// nota de extension en Services/CambiosDeDatos.cs.
    /// </summary>
    public override void Recargar()
    {
        var seleccionadaId = Seleccionada?.Id;

        Peticiones.Clear();
        foreach (var p in _fuente.GetAll())
            Peticiones.Add(p);

        Seleccionada = Peticiones.FirstOrDefault(p => p.Id == seleccionadaId);
        NotificarTotales();
    }

    private void Aprobar() => Resolver(
        "Aprobar solicitud",
        "Comentario para quien la solicitó",
        (peticion, usuario, comentario) => _servicio.Aprobar(peticion, usuario, comentario));

    private void Rechazar() => Resolver(
        "Rechazar solicitud",
        "Motivo del rechazo",
        (peticion, usuario, comentario) => _servicio.Rechazar(peticion, usuario, comentario));

    private void Resolver(string titulo,
                          string etiqueta,
                          Func<PeticionCambio, Usuario, string, PeticionCambio> transicion)
    {
        if (Seleccionada is not { } peticion || _sesion.UsuarioActual is not { } usuario)
            return;

        var editor = new MotivoEditorViewModel(
            titulo,
            $"{peticion.Resumen}\n\nSolicitada por {peticion.SolicitadoPorNombre}: {peticion.Motivo}",
            etiqueta,
            "Indique el motivo de la decisión.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        try
        {
            var resuelta = transicion(peticion, usuario, editor.Motivo);
            var indice = Peticiones.IndexOf(peticion);
            if (indice >= 0)
                Peticiones[indice] = resuelta;

            Seleccionada = resuelta;
            PeticionesView.Refresh();
            NotificarTotales();
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo resolver la petición", ex.Message);
        }
    }

    private void NotificarTotales()
    {
        OnPropertyChanged(nameof(Pendientes));
        OnPropertyChanged(nameof(ResumenPendientes));
    }
}
