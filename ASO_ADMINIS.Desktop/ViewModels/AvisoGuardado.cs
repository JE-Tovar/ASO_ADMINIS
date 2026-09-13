using System;
using System.Windows.Threading;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// El "Guardado" discreto que aparece un momento y se va.
///
/// Existe porque los ajustes pasaron a aplicarse al instante, y eso deja una pregunta abierta:
/// sin botón que pulsar, ¿cómo sabe quien lo cambió que se guardó? La respuesta anterior era un
/// <c>MessageBox</c> del sistema ("Preferencias guardadas", con su Aceptar), que para confirmar
/// algo que ya está hecho pide un clic más que el propio ajuste.
/// </summary>
public sealed class AvisoGuardado : ViewModelBase
{
    private readonly DispatcherTimer _reloj;

    public AvisoGuardado(int segundos = 3)
    {
        _reloj = new DispatcherTimer { Interval = TimeSpan.FromSeconds(segundos) };
        _reloj.Tick += (_, _) =>
        {
            _reloj.Stop();
            Texto = string.Empty;
        };
    }

    private string _texto = string.Empty;
    public string Texto
    {
        get => _texto;
        private set => SetProperty(ref _texto, value);
    }

    /// <summary>Muestra el aviso y reinicia la cuenta atrás.</summary>
    public void Mostrar(string texto = "Guardado")
    {
        Texto = texto;
        _reloj.Stop();
        _reloj.Start();
    }
}
