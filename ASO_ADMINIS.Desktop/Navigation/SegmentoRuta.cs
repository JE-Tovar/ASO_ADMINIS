using System.Windows.Input;

namespace ASO_ADMINIS.Desktop.Navigation;

/// <summary>
/// Un tramo de la ruta que se muestra en la barra superior: "Operaciones › Registro de Operación".
///
/// El último tramo es dónde se está y no lleva comando; los anteriores sí, y son el camino de
/// vuelta. Eso es lo que sustituye a los quince botones "Volver al módulo" repartidos por las
/// pantallas, cada uno con su propio estilo (tres distintos, y una sección sin ninguno).
/// </summary>
public sealed class SegmentoRuta(string texto, ICommand? comando, bool esPrimero)
{
    public string Texto { get; } = texto;

    /// <summary>Null en el tramo final: no se navega a donde ya se está.</summary>
    public ICommand? Comando { get; } = comando;

    public bool EsNavegable => Comando is not null;

    /// <summary>El primer tramo no lleva separador delante.</summary>
    public bool EsPrimero { get; } = esPrimero;
}
