namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Autenticacion contra el padron de usuarios.
///
/// Depende solo de <see cref="IUsuarioDataSource"/>, no de EF: el salto del filtro por
/// organizacion que exige el login queda encapsulado en la fuente de datos, no aqui.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUsuarioDataSource _usuarios;

    private readonly Func<DateTime> _ahora;

    public AuthService(IUsuarioDataSource usuarios, Func<DateTime>? ahora = null)
    {
        _usuarios = usuarios;
        _ahora = ahora ?? (() => DateTime.Now);
    }

    public ResultadoAutenticacion? ValidarCredenciales(string nombreUsuario, string password)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario))
            return null;

        var usuario = _usuarios.BuscarPorNombreSinAmbito(nombreUsuario.Trim());

        // Un usuario inactivo se trata igual que uno inexistente: no se le dice al que
        // intenta entrar cual de las dos cosas fallo.
        if (usuario is null || !usuario.Activo)
            return null;

        var ahora = _ahora();
        var correcta = Passwords.Verificar(password, usuario.PasswordHash, usuario.PasswordSalt);

        // Bloqueada: el aviso solo se da a quien escribió la contraseña buena. A quien está
        // probando claves se le contesta igual que siempre, sin decirle que la cuenta existe ni
        // que ya está bloqueada (y sin alargar el bloqueo, que es lo que buscaría para molestar).
        if (usuario.EstaBloqueado(ahora))
        {
            if (correcta)
                throw new SesionRechazadaException(
                    $"La cuenta está bloqueada por intentos fallidos hasta las {usuario.BloqueadoHasta:HH:mm}. " +
                    "Espera, o pídele a un administrador que la desbloquee en Administración · Usuarios.");
            return null;
        }

        if (!correcta)
        {
            var quedo = _usuarios.RegistrarIntentoFallido(usuario.Id, PoliticaSeguridad.IntentosAntesDeBloquear,
                                                          ahora + PoliticaSeguridad.DuracionBloqueo);
            if (quedo.EstaBloqueado(ahora))
                Registro.Advertencia("Cuenta {Nombre} bloqueada por intentos fallidos hasta {Hasta}",
                                     usuario.NombreUsuario, quedo.BloqueadoHasta);
            return null;
        }

        _usuarios.ReiniciarIntentos(usuario.Id);
        return new ResultadoAutenticacion(usuario, _usuarios.AjustesDe(usuario.Id));
    }
}
