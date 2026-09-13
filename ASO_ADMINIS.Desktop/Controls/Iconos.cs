namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Catalogo de glifos de la interfaz. Un solo sitio donde vive el punto de codigo de cada
/// icono, con un nombre que dice de que es: antes estaban sueltos por el XAML y por los
/// modelos como literales sin nombre, ilegibles sin abrir el visor de fuentes.
///
/// Los valores son de la fuente <b>Phosphor</b> (regular), empotrada en
/// <c>Assets/Fonts/Phosphor.ttf</c> y expuesta como el token <c>IconFont</c> de Tokens.xaml.
///
/// Para anadir uno: busca el icono en phosphoricons.com, toma su punto de codigo de
/// <c>@phosphor-icons/web/src/regular/style.css</c> y declaralo aqui con el nombre del
/// concepto -no el de la forma-, para que cambiar de dibujo no obligue a renombrar.
///
/// En XAML: <c>{x:Static controls:Iconos.Loque}</c>. En C#, directamente.
///
/// PROVISIONAL: este es el set mínimo que usa el armazón + el único módulo de ejemplo
/// (Finanzas · Cuentas por Pagar y Banco). Cada módulo de negocio nuevo suma sus propios
/// glifos aquí.
/// </summary>
public static class Iconos
{
    // Nota: las constantes de abajo son caracteres reales (zona de uso privado de Unicode,
    // U+E000-U+F8FF), no la secuencia de texto "\uXXXX" — un editor de texto normal los
    // muestra en blanco o como un cuadro, y solo se ven como icono con la fuente Phosphor
    // cargada (ver Styles/Tokens.xaml, IconFont). Es exactamente equivalente en C# a escribir
    // el escape "\uXXXX".

    // =============================== Navegacion ===============================

    public const string Inicio =           "";     // house U+E2C2
    public const string Peticiones =       "";     // tray U+E4AA

    public const string Catalogo =         ""; // storefront U+E470
    public const string Marca =            ""; // tag U+E478
    public const string Modelo =           ""; // sneaker U+E80C

    public const string Finanzas =         "";     // currency-circle-dollar U+E54C
    public const string CuentasPorPagar =  "";     // invoice U+EE42
    public const string Banco =            "";     // bank U+E0B4
    public const string Inventario =       "";     // package U+E390
    public const string Almacen =          "";     // warehouse U+ECD4
    public const string Entradas =         "";     // tray-arrow-down U+E010
    public const string Salidas =          "";     // tray-arrow-up U+EE52

    public const string Administracion =   "";     // shield-check U+E40C
    public const string Configuracion =    "";     // gear U+E270

    // =========================== Acciones y estados ===========================

    public const string Buscar =           "";     // magnifying-glass U+E30C
    public const string Limpiar =          "";     // x U+E4F6

    // ========================= Documentos y terceros =========================

    public const string Proveedor =        "";     // handshake U+E582
    public const string Cuenta =           "";     // wallet U+E68A
    public const string Usuarios =         "";     // users U+E4D6
    public const string Calendario =       "";     // calendar-blank U+E10A

    // =============================== Chevrones ===============================
    // Plegado del menu, separador de migas y navegacion del calendario.

    public const string ChevronArriba =    "";     // caret-up U+E13C
    public const string ChevronAbajo =     "";     // caret-down U+E136
    public const string ChevronIzquierda = "";     // caret-left U+E138
    public const string ChevronDerecha =   "";     // caret-right U+E13A
}
