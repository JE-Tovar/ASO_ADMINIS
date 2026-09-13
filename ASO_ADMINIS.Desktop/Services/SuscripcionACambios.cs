using System;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// La escucha de <see cref="CambiosDeDatos"/> de una pantalla, con su baja.
///
/// Existe como clase aparte —y no como miembros de una base— porque las pantallas llegan por dos
/// ramas de herencia distintas, <c>PantallaViewModelBase</c> y <c>PantallaCrudViewModel</c>, y C#
/// no tiene herencia múltiple (la misma restricción que ya obliga a duplicar el preámbulo de
/// <c>IPantalla</c>). Por composición el cableado se escribe una vez y las dos lo usan.
///
/// Darse de baja no es opcional: <see cref="CambiosDeDatos.Ocurrieron"/> es un evento estático y
/// vive más que cualquier pantalla. Una pantalla que se quedara suscrita seguiría viva y
/// recargándose desde la base de datos mucho después de haber salido de la vista, y se
/// acumularía una por cada navegación. La baja la da <c>MainWindow.Navegar</c>, que es el único
/// sitio por el que se cambia de pantalla.
/// </summary>
public sealed class SuscripcionACambios : IDisposable
{
    private Action? _alCambiar;

    public SuscripcionACambios(Action alCambiar)
    {
        _alCambiar = alCambiar;
        CambiosDeDatos.Ocurrieron += Manejar;
    }

    private void Manejar() => _alCambiar?.Invoke();

    public void Dispose()
    {
        CambiosDeDatos.Ocurrieron -= Manejar;
        _alCambiar = null;
    }
}
