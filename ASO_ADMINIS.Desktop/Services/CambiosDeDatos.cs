using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Avisa de que algo se escribió en la base de datos. Es lo que permite que las pantallas se
/// actualicen solas y que no haya un botón "Actualizar" en ninguna barra.
///
/// Es estática como <see cref="Ajustes"/> y por el mismo motivo: quien publica es
/// <c>AsoAdminisDbContext.SaveChanges</c>, en el fondo de la capa de datos, y quien escucha es un
/// ViewModel — no hay constructor común donde inyectar nada entre esos dos.
///
/// <para><b>No dice QUÉ cambió, y es deliberado.</b> El shell muestra una sola pantalla a la vez
/// (<c>MainWindow.Navegar</c> reemplaza el contenido), así que filtrar por tipo de entidad
/// ahorraría como mucho una consulta a LocalDB y a cambio traería un modo de fallo nuevo:
/// declararse corto en la lista de tipos y volver justo al problema que esto viene a resolver —
/// datos viejos en pantalla. Si algún día hiciera falta afinar, el sitio es este.</para>
///
/// <para><b>Punto de extensión para el caso multiusuario.</b> Un evento en proceso no puede
/// enterarse de lo que escribe OTRA máquina contra el mismo SQL Server (el caso claro es la
/// bandeja de Peticiones). Como el bus no distingue quién publica, un sondeo periódico solo
/// tendría que llamar a <see cref="Publicar"/> desde un temporizador; no hay nada más que
/// cambiar. Hoy no está implementado.</para>
/// </summary>
public static class CambiosDeDatos
{
    /// <summary>
    /// Hubo una escritura. Se dispara SIEMPRE en el hilo de interfaz, así que quien escucha
    /// puede tocar sus <c>ObservableCollection</c> sin más ceremonia.
    /// </summary>
    public static event Action? Ocurrieron;

    /// <summary>
    /// 1 mientras hay un aviso esperando en la cola del despachador. Evita la ráfaga: una sola
    /// acción de dominio puede guardar tres veces —confirmar un vale toca el vale, el stock de
    /// combustible y el activo— y sin esto serían tres recargas para el mismo clic.
    /// </summary>
    private static int _enCola;

    /// <summary>
    /// Anuncia la escritura.
    ///
    /// El aviso se ENCOLA, nunca se entrega en el acto, y esa es la parte importante: si se
    /// invocara de forma síncrona, la recarga correría <i>dentro</i> del <c>Add</c> o del
    /// <c>Update</c> que la disparó, reentrando en el ViewModel a mitad de una operación que
    /// todavía no terminó. Encolado, la pantalla se recarga cuando la acción ya se completó y el
    /// diálogo modal, si lo había, ya se cerró.
    /// </summary>
    public static void Publicar()
    {
        if (Interlocked.Exchange(ref _enCola, 1) == 1)
            return;

        var despachador = Application.Current?.Dispatcher;

        // Sin interfaz (arranque temprano) no hay a quién avisar ni cola donde dejarlo.
        if (despachador is null)
        {
            Interlocked.Exchange(ref _enCola, 0);
            return;
        }

        // El indicador se libera ANTES de avisar: una escritura que ocurra durante la propia
        // recarga es un cambio posterior al que se está entregando y merece su propio aviso.
        despachador.BeginInvoke(new Action(() =>
        {
            Interlocked.Exchange(ref _enCola, 0);
            Ocurrieron?.Invoke();
        }), DispatcherPriority.Background);
    }
}
