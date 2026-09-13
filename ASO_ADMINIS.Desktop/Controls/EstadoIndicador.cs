namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Lectura de un indicador: si el número que muestra está bien, pide atención o es un problema.
///
/// Existe para que el color de un <see cref="KpiTile"/> signifique algo. Antes los indicadores del
/// dashboard eran cinco cadenas ya formateadas (etiqueta, valor, nota) sin ninguna noción de si el
/// valor era bueno o malo, así que todas las tarjetas se pintaban igual y había que leerlas una a
/// una para enterarse de que algo iba mal.
/// </summary>
public enum EstadoIndicador
{
    Normal,
    Atencion,
    Critico,
}
