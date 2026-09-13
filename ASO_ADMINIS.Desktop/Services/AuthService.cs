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

    public AuthService(IUsuarioDataSource usuarios) => _usuarios = usuarios;

    public ResultadoAutenticacion? ValidarCredenciales(string nombreUsuario, string password)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario))
            return null;

        var usuario = _usuarios.BuscarPorNombreSinAmbito(nombreUsuario.Trim());

        // Un usuario inactivo se trata igual que uno inexistente: no se le dice al que
        // intenta entrar cual de las dos cosas fallo.
        if (usuario is null || !usuario.Activo)
            return null;

        if (!Passwords.Verificar(password, usuario.PasswordHash, usuario.PasswordSalt))
            return null;

        return new ResultadoAutenticacion(usuario, _usuarios.AjustesDe(usuario.Id));
    }
}
