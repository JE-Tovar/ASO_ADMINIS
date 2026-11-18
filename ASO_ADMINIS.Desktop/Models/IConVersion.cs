namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Entidad con concurrencia optimista: <see cref="Version"/> es una columna <c>rowversion</c> que
/// SQL Server cambia en cada escritura. Al guardar se exige que siga siendo la que se leyó; si
/// otro usuario la cambió entre medio, el guardado se rechaza con
/// <c>ConflictoDeEdicionException</c> en vez de pisar su cambio en silencio.
///
/// La marca como rowversion <c>AsoAdminisDbContext</c>, para toda entidad que implemente esta
/// interfaz. Para que funcione, la versión tiene que viajar con lo editado: <c>Clonar()</c> la
/// conserva, y <c>CrudViewModelBase.Editar</c> la copia a lo que devuelve el editor.
/// </summary>
public interface IConVersion
{
    byte[]? Version { get; set; }
}
