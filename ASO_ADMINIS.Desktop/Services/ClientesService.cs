using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Reglas del padrón de clientes. Las usan el editor de Ventas · Clientes y la factura, que da
/// de alta (o pone al día) al cliente al emitir: así el padrón se llena solo, sin ir aparte a
/// cargar a cada persona.
/// </summary>
public sealed partial class ClientesService
{
    /// <summary>Tope de la lista de sugerencias del campo RIF/C.I. de la factura.</summary>
    public const int MaximoSugerencias = 8;

    private readonly IClienteDataSource _clientes;
    private readonly ISesionActual _sesion;

    public ClientesService(IClienteDataSource clientes, ISesionActual sesion)
    {
        _clientes = clientes;
        _sesion = sesion;
    }

    // ---------------------------------------------------------------- Validación

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex PatronCorreo();

    /// <summary>Vacío vale; si se escribe, tiene que tener la forma algo@dominio.ext.</summary>
    public static bool CorreoValido(string? correo) =>
        string.IsNullOrWhiteSpace(correo) || PatronCorreo().IsMatch(correo.Trim());

    /// <summary>Lo que debe cumplir un cliente del padrón (identificación, nombre, dirección si tiene RIF, no repetido).</summary>
    public static bool Validar(Cliente cliente, IClienteDataSource clientes, out string? error)
    {
        if (Rif.Error(cliente.Rif) is { } errorRif)
        {
            error = errorRif;
            return false;
        }

        if (string.IsNullOrWhiteSpace(cliente.Nombre))
        {
            error = "Indique el nombre y apellido, o la razón social.";
            return false;
        }

        // Un contribuyente (RIF completo) necesita la dirección fiscal en la factura para
        // poder usarla como crédito fiscal.
        if (Rif.EsRifValido(cliente.Rif) && string.IsNullOrWhiteSpace(cliente.DireccionFiscal))
        {
            error = "Un cliente con RIF necesita su dirección fiscal: figura en la factura.";
            return false;
        }

        if (!CorreoValido(cliente.Correo))
        {
            error = $"El correo \"{cliente.Correo.Trim()}\" no tiene la forma nombre@dominio.com.";
            return false;
        }

        var repetido = clientes.GetAll()
            .Any(c => c.Id != cliente.Id && MismaIdentificacion(c.Rif, cliente.Rif));
        if (repetido)
        {
            error = $"Ya existe un cliente con la identificación {cliente.Rif}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>"v12345678", "V-12345678" y "V 12.345.678" son la misma persona.</summary>
    public static bool MismaIdentificacion(string? a, string? b)
    {
        var na = Rif.Normalizar(a);
        return na is not null && string.Equals(na, Rif.Normalizar(b), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- Búsqueda

    /// <summary>El cliente con esa identificación (escrita como sea), o null.</summary>
    public Cliente? PorIdentificacion(string? rifOCedula) =>
        Rif.Normalizar(rifOCedula) is null
            ? null
            : _clientes.GetAll().FirstOrDefault(c => MismaIdentificacion(c.Rif, rifOCedula));

    /// <summary>
    /// Clientes activos cuyo RIF/C.I. (sin guiones ni puntos) o cuyo nombre contienen lo escrito,
    /// sin acentos ni mayúsculas. Primero la identificación exacta, luego por nombre.
    /// </summary>
    public IReadOnlyList<Cliente> Sugerencias(string? texto)
    {
        var buscado = Texto.Normalizar(texto);
        if (buscado.Length < 2)
            return [];

        var digitosBuscados = SoloLetrasYDigitos(buscado);

        return [.. _clientes.GetAll()
            .Where(c => c.Activo)
            .Select(c => (Cliente: c, Id: SoloLetrasYDigitos(Texto.Normalizar(c.Rif)), Nombre: Texto.Normalizar(c.Nombre)))
            .Where(x => (digitosBuscados.Length > 0 && x.Id.Contains(digitosBuscados, StringComparison.Ordinal))
                        || x.Nombre.Contains(buscado, StringComparison.Ordinal))
            .OrderBy(x => MismaIdentificacion(x.Cliente.Rif, texto) ? 0 : 1)
            .ThenBy(x => x.Cliente.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaximoSugerencias)
            .Select(x => x.Cliente)];
    }

    private static string SoloLetrasYDigitos(string texto) => new([.. texto.Where(char.IsLetterOrDigit)]);

    // ---------------------------------------------------------------- Alta desde la factura

    /// <summary>
    /// Da de alta al cliente de la factura, o pone al día su ficha con lo que se escribió
    /// (nombre, dirección, teléfono, correo), y devuelve su Id para el documento.
    ///
    /// <b>Nunca impide facturar:</b> sin permiso (<c>Clientes.Crear</c> / <c>Clientes.Editar</c>)
    /// o si la ficha no pasa la validación, devuelve lo que haya (el Id del existente, o null) y
    /// la factura sigue con su propio snapshot del cliente.
    /// </summary>
    public int? RegistrarDesdeFactura(Venta factura)
    {
        if (factura.EsConsumidorFinal)
            return null;

        var rif = Rif.Normalizar(factura.ClienteRif);
        if (rif is null)
            return null;

        var existente = PorIdentificacion(rif);
        if (existente is null)
        {
            if (!_sesion.Puede(Permisos.Clientes.Crear))
                return null;

            var nuevo = new Cliente
            {
                Rif = rif,
                Nombre = factura.ClienteNombre.Trim(),
                DireccionFiscal = factura.ClienteDireccion.Trim(),
                Telefono = factura.ClienteTelefono.Trim(),
                Correo = factura.ClienteCorreo.Trim(),
                Activo = true
            };
            return Validar(nuevo, _clientes, out _) ? _clientes.Add(nuevo).Id : null;
        }

        var actualizado = existente.Clonar();
        actualizado.Nombre = factura.ClienteNombre.Trim();
        actualizado.DireccionFiscal = factura.ClienteDireccion.Trim();
        // Teléfono y correo vacíos en la factura no borran lo que ya tenía la ficha.
        if (!string.IsNullOrWhiteSpace(factura.ClienteTelefono))
            actualizado.Telefono = factura.ClienteTelefono.Trim();
        if (!string.IsNullOrWhiteSpace(factura.ClienteCorreo))
            actualizado.Correo = factura.ClienteCorreo.Trim();
        // Se le está vendiendo: vuelve a estar activo.
        actualizado.Activo = true;

        var cambio = actualizado.Nombre != existente.Nombre
                     || actualizado.DireccionFiscal != existente.DireccionFiscal
                     || actualizado.Telefono != existente.Telefono
                     || actualizado.Correo != existente.Correo
                     || actualizado.Activo != existente.Activo;

        if (cambio && _sesion.Puede(Permisos.Clientes.Editar) && Validar(actualizado, _clientes, out _))
            _clientes.Update(actualizado);

        return existente.Id;
    }
}
