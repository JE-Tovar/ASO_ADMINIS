using System.Windows;
using Velopack;
using Velopack.Sources;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Actualizaciones automaticas con Velopack, desde los Releases de GitHub del repositorio.
/// </summary>
public static class Actualizaciones
{
    private const string Repositorio = "https://github.com/JE-Tovar/ASO_ADMINIS";

    /// <summary>
    /// Busca una version nueva y, si la hay, la descarga en segundo plano y ofrece reiniciar.
    /// Si el usuario dice que no, Velopack la aplica sola en el siguiente arranque.
    ///
    /// Nunca lanza: sin red, con GitHub caido o con un release a medio subir, lo correcto es
    /// seguir trabajando con la version instalada y volver a intentarlo la proxima vez. En
    /// dotnet run / F5 no hace nada (no hay instalacion que actualizar).
    /// </summary>
    public static async Task BuscarAsync()
    {
        try
        {
            var gestor = new UpdateManager(new GithubSource(Repositorio, null, false));
            if (!gestor.IsInstalled)
                return;

            var nueva = await gestor.CheckForUpdatesAsync();
            if (nueva is null)
                return;

            await gestor.DownloadUpdatesAsync(nueva);

            var respuesta = MessageBox.Show(
                $"Hay una version nueva de ASO Adminis ({nueva.TargetFullRelease.Version}) lista para instalar.\n\n" +
                "¿Reiniciar ahora para aplicarla? Si eliges No, se aplicara la proxima vez que abras la aplicacion.",
                "ASO Adminis", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (respuesta == MessageBoxResult.Yes)
                gestor.ApplyUpdatesAndRestart(nueva);
        }
        catch
        {
            // Ver el comentario del metodo: un fallo aqui no debe interrumpir a nadie.
        }
    }
}
