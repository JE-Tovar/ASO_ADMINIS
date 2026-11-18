using System.IO;
using System.Diagnostics;
using ASO_ADMINIS.Desktop.BD;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;
using Microsoft.EntityFrameworkCore;

namespace ASO_ADMINIS.Tests;

/// <summary>
/// Solo corre con <c>ASO_PRUEBAS_SQL=1</c>: necesita SQL Server LocalDB. Las demás pruebas usan
/// fuentes en memoria y no lo tocan.
/// </summary>
public sealed class FactSqlAttribute : FactAttribute
{
    public FactSqlAttribute()
    {
        if (Environment.GetEnvironmentVariable("ASO_PRUEBAS_SQL") != "1")
            Skip = "Prueba de integración con SQL Server: activar con ASO_PRUEBAS_SQL=1.";
    }
}

/// <summary>
/// Una base LocalDB propia y temporal: se crea con todas las migraciones al empezar y se borra al
/// terminar. La cadena entra por la variable ASO_ConnectionStrings__AsoAdminisDb, que AppConfig
/// lee por encima de los appsettings, así que nunca se toca la base de la máquina.
/// </summary>
public sealed class BaseTemporal : IDisposable
{
    public Organizacion Organizacion { get; }
    public Caja Caja { get; }
    public Turno Turno { get; } = new();

    public BaseTemporal()
    {
        if (Environment.GetEnvironmentVariable("ASO_PRUEBAS_SQL") != "1")
        {
            Organizacion = new Organizacion();
            Caja = new Caja();
            return;
        }

        var nombre = $"AsoPruebaIntegracion_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable("ASO_ConnectionStrings__AsoAdminisDb",
            $"Server=(localdb)\\MSSQLLocalDB;Database={nombre};Trusted_Connection=True;TrustServerCertificate=True;");

        using (var db = new AsoAdminisDbContext())
            db.Database.Migrate();

        using (var db = new AsoAdminisDbContext())
        {
            Organizacion = new Organizacion
            {
                Codigo = "PRB", Nombre = "Prueba", RazonSocial = "Comercio de Prueba, C.A.",
                Rif = "J-00002961-0", DireccionFiscal = "Caracas"
            };
            db.Organizaciones.Add(Organizacion);
            db.SaveChanges();
        }

        Ambito.Fijar(Organizacion);
        Caja = new SqlCajaDataSource().Add(new Caja { Codigo = "CAJA-01", Activa = true });

        // Un turno real: Ventas.TurnoId tiene clave foránea a Turnos.
        Turno = new SqlTurnoDataSource().Add(new Turno
        {
            CajaId = Caja.Id, CajaCodigo = Caja.Codigo, UsuarioId = 1, UsuarioNombre = "Pruebas",
            Estado = EstadoTurno.Abierto, Apertura = DateTime.Now
        });
    }

    public void Dispose()
    {
        if (Environment.GetEnvironmentVariable("ASO_PRUEBAS_SQL") != "1")
            return;

        Ambito.Fijar(null);
        using var db = new AsoAdminisDbContext();
        db.Database.EnsureDeleted();
    }
}

[CollectionDefinition("SQL", DisableParallelization = true)]
public sealed class ColeccionSql : ICollectionFixture<BaseTemporal>;

[Collection("SQL")]
public sealed class IntegracionSqlTests(BaseTemporal bd) : IDisposable
{
    private readonly IDisposable _unidad = UnidadDeTrabajo.Usar(new UnidadDeTrabajoSql());

    public void Dispose() => _unidad.Dispose();

    // ---------------------------------------------------------------- Transacciones

    [FactSql]
    public void Si_la_operacion_falla_no_queda_nada_de_lo_escrito()
    {
        var proveedores = new SqlProveedorDataSource();
        var cuentas = new SqlCuentaBancariaDataSource();
        var nombre = $"Rollback {Guid.NewGuid():N}";

        Assert.Throws<InvalidOperationException>(() =>
            UnidadDeTrabajo.Ejecutar(null, () =>
            {
                // Dos fuentes, dos contextos, una transacción: no debe escalar a MSDTC.
                proveedores.Add(new Proveedor { Nombre = nombre, Rif = "J-00000000-0" });
                cuentas.Add(new CuentaBancaria { Nombre = nombre, Moneda = "Bs", FechaApertura = DateTime.Today });
                throw new InvalidOperationException("falla a propósito");
            }));

        Assert.DoesNotContain(proveedores.GetAll(), p => p.Nombre == nombre);
        Assert.DoesNotContain(cuentas.GetAll(), c => c.Nombre == nombre);
    }

    [FactSql]
    public void Si_la_operacion_termina_queda_todo()
    {
        var proveedores = new SqlProveedorDataSource();
        var nombre = $"Commit {Guid.NewGuid():N}";

        UnidadDeTrabajo.Ejecutar(UnidadDeTrabajo.Documentos, () =>
        {
            proveedores.Add(new Proveedor { Nombre = nombre + " A", Rif = "J-00000000-0" });
            proveedores.Add(new Proveedor { Nombre = nombre + " B", Rif = "J-00000000-0" });
        });

        Assert.Equal(2, proveedores.GetAll().Count(p => p.Nombre.StartsWith(nombre)));
    }

    [FactSql]
    public void El_candado_de_documentos_hace_esperar_a_la_segunda_caja()
    {
        using var dentro = new ManualResetEventSlim();
        var reloj = new Stopwatch();

        var primera = Task.Run(() =>
        {
            using var _ = UnidadDeTrabajo.Usar(new UnidadDeTrabajoSql());
            UnidadDeTrabajo.Ejecutar(UnidadDeTrabajo.Documentos, () =>
            {
                dentro.Set();
                Thread.Sleep(1500);
            });
        });

        dentro.Wait(TimeSpan.FromSeconds(10));
        reloj.Start();
        UnidadDeTrabajo.Ejecutar(UnidadDeTrabajo.Documentos, () => reloj.Stop());
        primera.Wait();

        Assert.True(reloj.ElapsedMilliseconds >= 1000, $"No esperó al candado ({reloj.ElapsedMilliseconds} ms)");
    }

    [FactSql]
    public void Dos_cajas_no_venden_la_misma_ultima_unidad()
    {
        var articulos = new SqlArticuloDataSource();
        var entradas = new SqlEntradaInventarioDataSource();
        var articulo = articulos.Add(new Articulo
        {
            Codigo = $"U{Guid.NewGuid():N}"[..12], Descripcion = "Última unidad", Unidad = UnidadMedida.Pieza,
            PrecioVenta = 10m, Alicuota = TipoAlicuota.Exento, Activo = true
        });
        entradas.Add(new EntradaInventario
        {
            Numero = $"ENT-{Guid.NewGuid():N}"[..20], Tipo = TipoEntrada.Ajuste, Estado = EstadoEntrada.Registrada,
            Fecha = DateTime.Today, FechaCreacion = DateTime.Now,
            Lineas = [new EntradaInventarioLinea { ArticuloId = articulo.Id, Cantidad = 1m, CantidadPresentacion = 1m, Factor = 1m }]
        });

        var resultados = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            using var u = UnidadDeTrabajo.Usar(new UnidadDeTrabajoSql());
            try
            {
                ServicioDeVentas().EmitirFactura(FacturaDe(articulo.Id), usuarioId: 1);
                return "vendida";
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        })).ToArray();
        Task.WaitAll(resultados);

        Assert.True(resultados.Count(r => r.Result == "vendida") == 1, string.Join(" || ", resultados.Select(r => r.Result)));
        Assert.Single(resultados, r => r.Result != "vendida");
    }

    // ---------------------------------------------------------------- Concurrencia optimista

    [FactSql]
    public void La_segunda_edicion_sobre_una_version_vieja_choca()
    {
        var fuente = new SqlProveedorDataSource();
        var creado = fuente.Add(new Proveedor { Nombre = $"Conflicto {Guid.NewGuid():N}", Rif = "J-00000000-0" });

        var ventanaA = fuente.GetById(creado.Id)!;
        var ventanaB = fuente.GetById(creado.Id)!;

        ventanaA.Telefono = "Editado en A";
        fuente.Update(ventanaA);

        ventanaB.Telefono = "Editado en B";
        Assert.Throws<ConflictoDeEdicionException>(() => fuente.Update(ventanaB));

        Assert.Equal("Editado en A", fuente.GetById(creado.Id)!.Telefono);
    }

    // ---------------------------------------------------------------- Bitácora

    [FactSql]
    public void Cada_cambio_deja_su_fila_en_la_bitacora_con_antes_y_despues()
    {
        var fuente = new SqlProveedorDataSource();
        var creado = fuente.Add(new Proveedor { Nombre = $"Auditado {Guid.NewGuid():N}", Rif = "J-00000000-0", Telefono = "Ana" });

        var editado = fuente.GetById(creado.Id)!;
        editado.Telefono = "Luis";
        fuente.Update(editado);

        using var db = new AsoAdminisDbContext();
        var filas = db.RegistrosAuditoria
            .Where(r => r.Entidad == nameof(Proveedor) && r.EntidadId == creado.Id.ToString())
            .OrderBy(r => r.Id)
            .ToList();

        Assert.Equal([AccionAuditoria.Alta, AccionAuditoria.Modificacion], filas.Select(f => f.Accion).ToArray());
        var cambio = Assert.Single(Auditoria.Leer(filas[1].Detalle));
        Assert.Equal(new CambioDeCampo("Telefono", "Ana", "Luis"), cambio);
    }

    // ---------------------------------------------------------------- Seguridad

    [FactSql]
    public void El_bloqueo_se_guarda_sin_sesion_y_queda_en_la_bitacora()
    {
        var (hash, salt) = Passwords.Crear("Buena2026");
        var nombre = $"bloq{Guid.NewGuid():N}"[..16];
        new SqlUsuarioDataSource().Add(new Usuario
        {
            NombreUsuario = nombre, NombreCompleto = "Bloqueable", PasswordHash = hash, PasswordSalt = salt,
            Activo = true, Rol = Rol.Supervisor
        });

        // Como en el login: sin organización fijada.
        Ambito.Fijar(null);
        try
        {
            var auth = new AuthService(new SqlUsuarioDataSource());
            for (var i = 0; i < PoliticaSeguridad.IntentosAntesDeBloquear; i++)
                Assert.Null(auth.ValidarCredenciales(nombre, "mala1234"));

            Assert.Throws<SesionRechazadaException>(() => auth.ValidarCredenciales(nombre, "Buena2026"));
        }
        finally
        {
            Ambito.Fijar(bd.Organizacion);
        }

        var usuario = new SqlUsuarioDataSource().GetAll().Single(u => u.NombreUsuario == nombre);
        Assert.NotNull(usuario.BloqueadoHasta);

        using var db = new AsoAdminisDbContext();
        Assert.Contains(db.RegistrosAuditoria.Where(r => r.Entidad == nameof(Usuario) && r.EntidadId == usuario.Id.ToString()).ToList(),
                        r => r.Detalle.Contains(nameof(Usuario.BloqueadoHasta)));
    }

    // ---------------------------------------------------------------- Respaldos

    [FactSql]
    public void El_respaldo_nativo_se_puede_restaurar_en_otra_base()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "aso-pruebas", Guid.NewGuid().ToString("N"));
        var copia = $"AsoPruebaRestaurada_{Guid.NewGuid():N}";
        var marca = $"Antes del respaldo {Guid.NewGuid():N}";
        new SqlProveedorDataSource().Add(new Proveedor { Nombre = marca, Rif = "J-00000000-0" });

        var servicio = new RespaldoService(new SqlRespaldoDataSource(), new OpcionesRespaldo(carpeta, null));
        var resultado = servicio.Respaldar(OrigenRespaldo.Manual, "pruebas");
        Assert.True(resultado.TodoBien, resultado.Resumen);

        var bak = resultado.Registros.Single(r => r.Tipo == TipoRespaldo.Nativo);
        Assert.True(bak.Verificado);
        Assert.True(File.Exists(bak.Archivo));

        try
        {
            using var db = new AsoAdminisDbContext();
            var archivos = db.Database.SqlQueryRaw<ArchivoLogico>(
                "RESTORE FILELISTONLY FROM DISK = {0}", bak.Archivo).ToList();
            var mover = string.Join(", ", archivos.Select(a =>
                $"MOVE N'{a.LogicalName}' TO N'{Path.Combine(carpeta, copia + (a.Type == "L" ? ".ldf" : ".mdf"))}'"));
            db.Database.ExecuteSqlRaw($"RESTORE DATABASE [{copia}] FROM DISK = N'{bak.Archivo}' WITH {mover}");

            var contados = db.Database.SqlQueryRaw<int>(
                $"SELECT COUNT(*) AS Value FROM [{copia}].dbo.Proveedores WHERE Nombre = {{0}}", marca).ToList();
            Assert.Equal(1, contados.Single());
        }
        finally
        {
            using var db = new AsoAdminisDbContext();
            db.Database.ExecuteSqlRaw($"IF DB_ID(N'{copia}') IS NOT NULL BEGIN ALTER DATABASE [{copia}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{copia}]; END");
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [FactSql]
    public void La_exportacion_portable_trae_todas_las_tablas_con_sus_renglones()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "aso-pruebas", Guid.NewGuid().ToString("N"));
        try
        {
            var ruta = Path.Combine(carpeta, "exportacion.zip");
            new SqlRespaldoDataSource().ExportacionPortable(ruta, "pruebas");

            using var zip = System.IO.Compression.ZipFile.OpenRead(ruta);
            using var manifiesto = System.Text.Json.JsonDocument.Parse(zip.GetEntry("manifiesto.json")!.Open());
            var tablas = manifiesto.RootElement.GetProperty("Tablas");

            using var db = new AsoAdminisDbContext();
            Assert.Equal(db.Proveedores.IgnoreQueryFilters().Count(), tablas.GetProperty("Proveedores").GetInt32());
            Assert.Contains(zip.Entries, e => e.FullName == "tablas/Ventas.json");
            Assert.Contains(zip.Entries, e => e.FullName == "tablas/RegistrosAuditoria.json");

            // Los renglones (owned) van anidados en su documento.
            using var entradas = System.Text.Json.JsonDocument.Parse(zip.GetEntry("tablas/EntradasInventario.json")!.Open());
            if (entradas.RootElement.GetArrayLength() > 0)
                Assert.True(entradas.RootElement[0].TryGetProperty("Lineas", out _));
        }
        finally
        {
            if (Directory.Exists(carpeta))
                Directory.Delete(carpeta, recursive: true);
        }
    }

    private sealed class ArchivoLogico
    {
        public string LogicalName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    [FactSql]
    public void La_bitacora_no_se_puede_modificar_ni_por_fuera()
    {
        new SqlProveedorDataSource().Add(new Proveedor { Nombre = $"Para bitácora {Guid.NewGuid():N}", Rif = "J-00000000-0" });

        using var db = new AsoAdminisDbContext();
        Assert.ThrowsAny<Exception>(() => db.Database.ExecuteSqlRaw("UPDATE RegistrosAuditoria SET Resumen = N'borrado'"));
        Assert.ThrowsAny<Exception>(() => db.Database.ExecuteSqlRaw("DELETE FROM RegistrosAuditoria"));
        Assert.DoesNotContain(db.RegistrosAuditoria.ToList(), r => r.Resumen == "borrado");
    }

    // ---------------------------------------------------------------- Apoyo

    private VentasService ServicioDeVentas()
    {
        var articulos = new SqlArticuloDataSource();
        var salidas = new SqlSalidaInventarioDataSource();
        var entradas = new SqlEntradaInventarioDataSource();
        var sesion = new SesionTotal { CajaActual = bd.Caja };
        var turno = bd.Turno;

        return new VentasService(new SqlVentaDataSource(), articulos, salidas, entradas,
                                 new InventarioService(articulos, entradas, salidas),
                                 sesion, new EmisorDePrueba(),
                                 organizacion: () => bd.Organizacion,
                                 turno: () => turno);
    }

    private static Venta FacturaDe(int articuloId) => new()
    {
        TasaCambioUsada = 40m,
        ClienteRif = "V-11111111",
        ClienteNombre = "Cliente",
        ClienteDireccion = "Caracas",
        Lineas = [new VentaLinea
        {
            ArticuloId = articuloId, Descripcion = "x", PresentacionNombre = "pza", Factor = 1, Cantidad = 1,
            PrecioUnitarioUsd = 10m, Alicuota = TipoAlicuota.Exento
        }],
        Pagos = [new VentaPago { Metodo = MetodoPago.EfectivoBs, MontoBs = 400m }]
    };
}
