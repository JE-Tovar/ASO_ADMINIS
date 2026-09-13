using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

public sealed record OpcionTema(TemaApp Valor, string Texto);

public sealed record OpcionEscala(double Valor, string Texto);

/// <summary>
/// Tema y escala de la interfaz.
///
/// No lleva botón de guardar: los dos ajustes se aplican en el momento y se persisten al
/// cambiarlos. Un "Guardar" aquí sobraría — el resultado ya está a la vista, y pedir
/// confirmación de algo que se ve es pedir dos veces lo mismo.
/// </summary>
public sealed class AparienciaViewModel : ViewModelBase
{
    public AparienciaViewModel()
    {
        TemasDisponibles =
        [
            new OpcionTema(TemaApp.Claro, "Claro"),
            new OpcionTema(TemaApp.Oscuro, "Oscuro")
        ];

        EscalasDisponibles =
        [
            new OpcionEscala(1.0, "100 % (normal)"),
            new OpcionEscala(1.1, "110 % (algo más grande)"),
            new OpcionEscala(1.25, "125 % (grande)")
        ];

        _tema = TemasDisponibles.First(o => o.Valor == Ajustes.Actual.Tema);

        // Si el archivo trae una escala que ya no está en la lista (o venía corrupto), se
        // muestra el 100 % en vez de dejar el combo en blanco.
        _escala = EscalasDisponibles.FirstOrDefault(o => o.Valor == Ajustes.Actual.EscalaInterfaz)
                  ?? EscalasDisponibles[0];
    }

    public IReadOnlyList<OpcionTema> TemasDisponibles { get; }
    public IReadOnlyList<OpcionEscala> EscalasDisponibles { get; }

    private OpcionTema _tema;
    public OpcionTema Tema
    {
        get => _tema;
        set
        {
            if (!SetProperty(ref _tema, value))
                return;

            Services.Tema.Aplicar(value.Valor);
            Ajustes.Actual.Tema = value.Valor;

            if (Ajustes.Guardar())
                Aviso.Mostrar();

            OnPropertyChanged(nameof(EsClaro));
            OnPropertyChanged(nameof(EsOscuro));
        }
    }

    public AvisoGuardado Aviso { get; } = new();

    /// <summary>
    /// El tema, como dos interruptores en vez de una lista desplegable.
    ///
    /// Elegir entre dos cosas no necesita abrir un menu: se ven las dos y se pulsa una. Y como el
    /// tema se aplica al marcar, la aplicacion entera es la vista previa.
    ///
    /// El setter solo actua al marcar, igual que las pestanas: al desmarcar ya hay otro boton del
    /// grupo encendiendose.
    /// </summary>
    public bool EsClaro
    {
        get => Tema.Valor == TemaApp.Claro;
        set { if (value) Tema = TemasDisponibles.First(o => o.Valor == TemaApp.Claro); }
    }

    public bool EsOscuro
    {
        get => Tema.Valor == TemaApp.Oscuro;
        set { if (value) Tema = TemasDisponibles.First(o => o.Valor == TemaApp.Oscuro); }
    }

    private OpcionEscala _escala;
    public OpcionEscala Escala
    {
        get => _escala;
        set
        {
            if (!SetProperty(ref _escala, value))
                return;

            // La ventana escucha Ajustes.Cambiaron y reaplica el LayoutTransform: guardar es
            // lo que dispara el cambio, no hay que avisar a nadie más.
            Ajustes.Actual.EscalaInterfaz = value.Valor;

            if (Ajustes.Guardar())
                Aviso.Mostrar();
        }
    }
}
