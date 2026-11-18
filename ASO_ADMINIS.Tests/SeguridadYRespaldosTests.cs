using System.IO;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Tests;

public class SeguridadYRespaldosTests
{
    // ---------------------------------------------------------------- Política de contraseñas

    [Theory]
    [InlineData("corta1", "al menos")]
    [InlineData("solamenteletras", "letras y números")]
    [InlineData("1234567890", "letras y números")]
    [InlineData("maria2026x", "nombre de usuario")]
    public void La_politica_rechaza_contrasenas_debiles(string password, string motivo)
    {
        Assert.Contains(motivo, PoliticaSeguridad.ErrorPassword(password, "maria"));
    }

    [Fact]
    public void La_politica_acepta_una_buena()
    {
        Assert.Null(PoliticaSeguridad.ErrorPassword("Caja2026segura", "maria"));
    }

    // ---------------------------------------------------------------- Bloqueo por intentos

    [Fact]
    public void Cinco_intentos_fallidos_bloquean_la_cuenta()
    {
        var (auth, usuarios, reloj) = Montar();

        for (var i = 0; i < PoliticaSeguridad.IntentosAntesDeBloquear; i++)
            Assert.Null(auth.ValidarCredenciales("caja1", "mala1234"));

        var bloqueada = usuarios.GetById(1)!;
        Assert.Equal(reloj.Ahora + PoliticaSeguridad.DuracionBloqueo, bloqueada.BloqueadoHasta);

        // Con la contraseña buena se le dice que está bloqueada; con una mala, nada nuevo.
        var ex = Assert.Throws<SesionRechazadaException>(() => auth.ValidarCredenciales("caja1", "Buena2026"));
        Assert.Contains("bloqueada", ex.Message);
        Assert.Null(auth.ValidarCredenciales("caja1", "mala1234"));
        Assert.Equal(reloj.Ahora + PoliticaSeguridad.DuracionBloqueo, usuarios.GetById(1)!.BloqueadoHasta);
    }

    [Fact]
    public void Pasado_el_bloqueo_se_entra_y_el_contador_vuelve_a_cero()
    {
        var (auth, usuarios, reloj) = Montar();
        for (var i = 0; i < PoliticaSeguridad.IntentosAntesDeBloquear; i++)
            auth.ValidarCredenciales("caja1", "mala1234");

        reloj.Ahora += PoliticaSeguridad.DuracionBloqueo + TimeSpan.FromSeconds(1);

        Assert.NotNull(auth.ValidarCredenciales("caja1", "Buena2026"));
        Assert.Equal(0, usuarios.GetById(1)!.IntentosFallidos);
        Assert.Null(usuarios.GetById(1)!.BloqueadoHasta);
    }

    [Fact]
    public void Entrar_bien_reinicia_los_intentos()
    {
        var (auth, usuarios, _) = Montar();
        auth.ValidarCredenciales("caja1", "mala1234");
        auth.ValidarCredenciales("caja1", "mala1234");

        auth.ValidarCredenciales("caja1", "Buena2026");

        Assert.Equal(0, usuarios.GetById(1)!.IntentosFallidos);
    }

    private sealed class Reloj
    {
        public DateTime Ahora { get; set; } = new(2026, 11, 18, 10, 0, 0);
    }

    private static (AuthService, UsuariosEnMemoria, Reloj) Montar()
    {
        var usuarios = new UsuariosEnMemoria();
        var (hash, salt) = Passwords.Crear("Buena2026");
        usuarios.Add(new Usuario { NombreUsuario = "caja1", NombreCompleto = "Caja uno", PasswordHash = hash, PasswordSalt = salt, Activo = true });
        var reloj = new Reloj();
        return (new AuthService(usuarios, () => reloj.Ahora), usuarios, reloj);
    }

    // ---------------------------------------------------------------- Respaldos

    [Fact]
    public void Si_otra_PC_esta_respaldando_no_se_hace_nada()
    {
        var fuente = new RespaldosFalsos { CandadoLibre = false };

        var resultado = Servicio(fuente).Respaldar(OrigenRespaldo.Manual, "yo");

        Assert.False(resultado.Hecho);
        Assert.Empty(fuente.Anotados);
    }

    [Fact]
    public void Si_falla_el_nativo_igual_se_hace_el_portable_y_se_anotan_los_dos()
    {
        var fuente = new RespaldosFalsos { FallaNativo = true };

        var resultado = Servicio(fuente).Respaldar(OrigenRespaldo.Manual, "yo");

        Assert.False(resultado.TodoBien);
        Assert.Equal(2, fuente.Anotados.Count);
        Assert.False(fuente.Anotados.Single(r => r.Tipo == TipoRespaldo.Nativo).Exitoso);
        Assert.True(fuente.Anotados.Single(r => r.Tipo == TipoRespaldo.Portable).Exitoso);
    }

    [Fact]
    public void El_automatico_no_se_repite_si_ya_hay_respaldos_recientes()
    {
        var fuente = new RespaldosFalsos();
        fuente.Exitosos[TipoRespaldo.Nativo] = DateTime.Now.AddHours(-2);
        fuente.Exitosos[TipoRespaldo.Portable] = DateTime.Now.AddHours(-2);

        var resultado = Servicio(fuente).RespaldarSiToca("yo");

        Assert.False(resultado.Hecho);
        Assert.Empty(fuente.Anotados);
    }

    [Fact]
    public void El_automatico_corre_si_falta_alguno_de_los_dos()
    {
        var fuente = new RespaldosFalsos();
        fuente.Exitosos[TipoRespaldo.Nativo] = DateTime.Now.AddHours(-2);   // el portable nunca se hizo

        Assert.True(Servicio(fuente).RespaldarSiToca("yo").Hecho);
    }

    [Fact]
    public void Se_conservan_solo_los_ultimos_catorce_y_nada_ajeno()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "aso-pruebas", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            for (var i = 1; i <= 20; i++)
                File.WriteAllText(Path.Combine(carpeta, $"aso-exportacion-202601{i:D2}-000000.zip"), "x");
            File.WriteAllText(Path.Combine(carpeta, "otra-cosa.zip"), "no tocar");

            var fuente = new RespaldosFalsos { CarpetaZip = carpeta };
            new RespaldoService(fuente, new OpcionesRespaldo(carpeta, null)).Respaldar(OrigenRespaldo.Manual, "yo");

            var quedan = Directory.GetFiles(carpeta, "aso-exportacion-*.zip");
            Assert.Equal(RespaldoService.Conservar, quedan.Length);
            Assert.True(File.Exists(Path.Combine(carpeta, "otra-cosa.zip")));
            Assert.DoesNotContain(quedan, f => f.EndsWith("20260101-000000.zip"));
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    private static RespaldoService Servicio(RespaldosFalsos fuente) =>
        new(fuente, new OpcionesRespaldo(Path.Combine(Path.GetTempPath(), "aso-pruebas", "no-existe"), null));

    private sealed class RespaldosFalsos : IRespaldoDataSource
    {
        public bool CandadoLibre { get; set; } = true;
        public bool FallaNativo { get; set; }
        public string? CarpetaZip { get; set; }
        public List<RegistroRespaldo> Anotados { get; } = [];
        public Dictionary<TipoRespaldo, DateTime> Exitosos { get; } = [];

        public RegistroRespaldo Anotar(RegistroRespaldo registro)
        {
            Anotados.Add(registro);
            if (registro.Exitoso)
                Exitosos[registro.Tipo] = registro.Fecha;
            return registro;
        }

        public IReadOnlyList<RegistroRespaldo> Ultimos(int cuantos) => Anotados;
        public DateTime? UltimoExitoso(TipoRespaldo tipo) => Exitosos.TryGetValue(tipo, out var f) ? f : null;

        public (string Ruta, long Tamano, bool Verificado) RespaldoNativo(string carpetaServidor, string nombreArchivo) =>
            FallaNativo ? throw new InvalidOperationException("disco lleno") : (Path.Combine(carpetaServidor, nombreArchivo), 10, true);

        public (string Ruta, long Tamano) ExportacionPortable(string rutaZip, string versionApp)
        {
            if (CarpetaZip is not null)
                File.WriteAllText(rutaZip, "zip");
            return (rutaZip, 3);
        }

        public string CarpetaServidor(string? configurada, string carpetaLocal) => configurada ?? carpetaLocal;
        public bool ServidorEsLocal() => false;
        public IDisposable? TomarCandadoDeRespaldo() => CandadoLibre ? new MemoryStream() : null;
    }
}
