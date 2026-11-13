using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Buscador de artículos de los documentos con renglones (factura, entrada, salida): se escribe
/// parte del código, de la descripción o de la categoría, y el elegido se agrega al documento.
///
/// Reemplaza el combo plano por renglón, que con un catálogo grande obligaba a recorrer la
/// lista a mano y solo aprovechaba la descripción. Lo pinta <c>Controls/BuscadorArticulos</c>;
/// aquí no hay nada de WPF salvo los comandos, así que se prueba sin interfaz.
///
/// <b>Cómo busca:</b> cada palabra escrita tiene que aparecer en el código, la descripción o la
/// categoría, sin mayúsculas ni acentos (<see cref="Services.Texto.Normalizar"/>). Primero va el
/// código exacto, luego los códigos que empiezan por lo escrito, luego el resto por código.
/// <b>Enter</b> con un código exacto lo elige aunque haya otros resultados: es lo que manda un
/// lector de código de barras.
///
/// <b>Granel:</b> "1,25*CLA-2P" agrega 1,25 del código exacto CLA-2P (convención de punto de
/// venta), y la etiqueta EAN-13 de la balanza (ver <see cref="EtiquetaBalanza"/>) agrega el
/// artículo de ese <see cref="Articulo.CodigoBalanza"/> con el peso impreso. Sin cantidad, el
/// editor decide (suma 1, o abre un renglón vacío si es a granel).
/// </summary>
public sealed class BuscadorArticulosViewModel : ViewModelBase
{
    public const string TodasLasCategorias = "Todas las categorías";

    /// <summary>Tope de filas en la lista: más de esto no se lee, se sigue escribiendo.</summary>
    public const int MaximoResultados = 50;

    private readonly IReadOnlyList<Articulo> _articulos;
    private readonly Action<Articulo, decimal?> _alElegir;
    private readonly FormatoEtiqueta? _etiquetas;

    /// <param name="etiquetas">Formato de las etiquetas de balanza; nulo = no se leen.</param>
    public BuscadorArticulosViewModel(IReadOnlyList<Articulo> articulos,
                                      Action<Articulo, decimal?> alElegir,
                                      FormatoEtiqueta? etiquetas = null)
    {
        _articulos = articulos;
        _alElegir = alElegir;
        _etiquetas = etiquetas;

        Categorias = [TodasLasCategorias, .. articulos
            .Select(a => a.Categoria.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)];

        ElegirCommand = new RelayCommand<Articulo>(a => { if (a is not null) Elegir(a, CantidadEscrita()); });
        ConfirmarCommand = new RelayCommand(Confirmar);
        BajarCommand = new RelayCommand(() => Mover(+1));
        SubirCommand = new RelayCommand(() => Mover(-1));
        CerrarCommand = new RelayCommand(Cerrar);
    }

    public IReadOnlyList<string> Categorias { get; }

    public ObservableCollection<Articulo> Resultados { get; } = [];

    public ICommand ElegirCommand { get; }
    public ICommand ConfirmarCommand { get; }
    public ICommand BajarCommand { get; }
    public ICommand SubirCommand { get; }
    public ICommand CerrarCommand { get; }

    private string _texto = string.Empty;
    public string Texto
    {
        get => _texto;
        set
        {
            if (!SetProperty(ref _texto, value ?? string.Empty))
                return;
            Aviso = string.Empty;
            Buscar(abrir: true);
        }
    }

    private string _aviso = string.Empty;
    /// <summary>Por qué no se agregó lo escrito (etiqueta de un producto que no está asignado…).</summary>
    public string Aviso
    {
        get => _aviso;
        private set { if (SetProperty(ref _aviso, value)) OnPropertyChanged(nameof(HayAviso)); }
    }

    public bool HayAviso => Aviso.Length > 0;

    /// <summary>
    /// "1,25*CLA" → (1,25, "CLA"). Sin asterisco (o con una cantidad que no se lee) no hay cantidad
    /// y todo el texto es lo que se busca. Coma o punto valen como decimal.
    /// </summary>
    public static (decimal? Cantidad, string Busqueda) Separar(string texto)
    {
        var i = texto.IndexOf('*');
        if (i <= 0)
            return (null, texto);

        var numero = texto[..i].Trim().Replace(',', '.');
        return decimal.TryParse(numero, System.Globalization.NumberStyles.Number,
                                System.Globalization.CultureInfo.InvariantCulture, out var cantidad) && cantidad > 0
            ? (cantidad, texto[(i + 1)..])
            : (null, texto);
    }

    private decimal? CantidadEscrita() => Separar(Texto).Cantidad;

    private string Busqueda => Separar(Texto).Busqueda;

    private string _categoriaSeleccionada = TodasLasCategorias;
    public string CategoriaSeleccionada
    {
        get => _categoriaSeleccionada;
        set { if (SetProperty(ref _categoriaSeleccionada, value ?? TodasLasCategorias)) Buscar(abrir: true); }
    }

    private Articulo? _resultadoSeleccionado;
    public Articulo? ResultadoSeleccionado
    {
        get => _resultadoSeleccionado;
        set => SetProperty(ref _resultadoSeleccionado, value);
    }

    private bool _mostrarResultados;
    /// <summary>Si la lista desplegable está abierta.</summary>
    public bool MostrarResultados
    {
        get => _mostrarResultados;
        set => SetProperty(ref _mostrarResultados, value);
    }

    public bool HayResultados => Resultados.Count > 0;

    public string AvisoResultados => Resultados.Count switch
    {
        0 => "Ningún producto coincide. Pruebe con otra palabra, parte del código o la categoría.",
        MaximoResultados => $"Se muestran los primeros {MaximoResultados}: escriba más para afinar.",
        1 => "1 producto",
        var n => $"{n} productos"
    };

    private bool FiltraCategoria => !string.Equals(CategoriaSeleccionada, TodasLasCategorias, StringComparison.Ordinal);

    /// <summary>Lo que coincide con el texto y la categoría, en el orden en que se muestra.</summary>
    public IReadOnlyList<Articulo> Coincidencias()
    {
        var palabras = Services.Texto.Normalizar(Busqueda).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Length == 0 && !FiltraCategoria)
            return [];

        var completo = Services.Texto.Normalizar(Busqueda);

        return [.. _articulos
            .Where(a => !FiltraCategoria || string.Equals(a.Categoria.Trim(), CategoriaSeleccionada, StringComparison.OrdinalIgnoreCase))
            .Select(a => (Articulo: a,
                          Codigo: Services.Texto.Normalizar(a.Codigo),
                          Todo: Services.Texto.Normalizar($"{a.Codigo} {a.Descripcion} {a.Categoria}")))
            .Where(x => palabras.All(p => x.Todo.Contains(p, StringComparison.Ordinal)))
            .OrderBy(x => completo.Length > 0 && x.Codigo == completo ? 0
                        : completo.Length > 0 && x.Codigo.StartsWith(completo, StringComparison.Ordinal) ? 1
                        : 2)
            .ThenBy(x => x.Articulo.Codigo, StringComparer.OrdinalIgnoreCase)
            .Take(MaximoResultados)
            .Select(x => x.Articulo)];
    }

    /// <summary>El artículo cuyo código es exactamente lo escrito, si lo hay.</summary>
    public Articulo? CodigoExacto()
    {
        var completo = Services.Texto.Normalizar(Busqueda);
        return completo.Length == 0
            ? null
            : _articulos.FirstOrDefault(a => Services.Texto.Normalizar(a.Codigo) == completo);
    }

    private void Buscar(bool abrir)
    {
        Resultados.Clear();
        foreach (var articulo in Coincidencias())
            Resultados.Add(articulo);

        ResultadoSeleccionado = Resultados.FirstOrDefault();
        MostrarResultados = abrir && (Busqueda.Trim().Length > 0 || FiltraCategoria);
        OnPropertyChanged(nameof(HayResultados));
        OnPropertyChanged(nameof(AvisoResultados));
    }

    /// <summary>
    /// Enter: una etiqueta de balanza (artículo y peso), si no el código exacto, si no el
    /// resaltado, si no el primero. "cantidad*código" exige el código exacto, para que un "2*"
    /// no agregue dos de lo primero que aparezca.
    /// </summary>
    public void Confirmar()
    {
        if (LeerEtiqueta())
            return;

        var (cantidad, _) = Separar(Texto);
        var elegido = cantidad is null
            ? CodigoExacto() ?? ResultadoSeleccionado ?? Resultados.FirstOrDefault()
            : CodigoExacto() ?? (Resultados.Count == 1 ? Resultados[0] : null);

        if (elegido is not null)
            Elegir(elegido, cantidad);
        else if (cantidad is not null)
            Aviso = $"Después de \"{Texto[..(Texto.IndexOf('*') + 1)]}\" escriba el código exacto del producto.";
    }

    /// <summary>Si lo escrito es una etiqueta de balanza, agrega su artículo con el peso. True si lo era.</summary>
    private bool LeerEtiqueta()
    {
        if (_etiquetas is null || !EtiquetaBalanza.Leer(Texto, _etiquetas, out var plu, out var peso))
            return false;

        var normal = EtiquetaBalanza.NormalizarPlu(plu);
        var articulo = _articulos.FirstOrDefault(a => a.CodigoBalanza.Trim().Length > 0
                                                      && EtiquetaBalanza.NormalizarPlu(a.CodigoBalanza) == normal);
        if (articulo is null)
        {
            // Una etiqueta válida de un producto que nadie tiene asignado: se avisa en vez de
            // buscarla como texto (no coincidiría con nada y el cajero no sabría por qué).
            Aviso = $"La etiqueta es del producto {plu} de la balanza, que no está asignado a ningún artículo. " +
                    "Asígnelo en Inventario · Almacén (\"Código en la balanza\").";
            MostrarResultados = false;
            return true;
        }

        Elegir(articulo, peso);
        return true;
    }

    public void Elegir(Articulo articulo, decimal? cantidad = null)
    {
        _alElegir(articulo, cantidad);
        Aviso = string.Empty;

        // Se limpia para el siguiente; la categoría se conserva (se suele elegir varios de la misma).
        _texto = string.Empty;
        OnPropertyChanged(nameof(Texto));
        Buscar(abrir: false);
    }

    private void Mover(int paso)
    {
        if (Resultados.Count == 0)
            return;

        if (!MostrarResultados)
        {
            MostrarResultados = true;
            return;
        }

        var i = ResultadoSeleccionado is null ? -1 : Resultados.IndexOf(ResultadoSeleccionado);
        ResultadoSeleccionado = Resultados[Math.Clamp(i + paso, 0, Resultados.Count - 1)];
    }

    private void Cerrar() => MostrarResultados = false;
}
