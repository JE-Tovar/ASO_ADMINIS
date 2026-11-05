# ASO Adminis

Sistema de gestión de escritorio (WPF · .NET 8) para un negocio de **venta de artículos**, genérico
para cualquier comercio (zapatería, pinturas, ferretería…): **Ventas** con documentos fiscales
según el SENIAT, **Inventario**, **Finanzas** y **Reportes**, sobre el armazón técnico de **ASO**
(autenticación, roles y permisos, aislamiento por organización, framework CRUD/MVVM, tema
claro/oscuro, peticiones de cambio). Ver `CLAUDE.md` para el detalle completo de qué trae, cómo
está organizado y cómo se agrega un módulo nuevo.

## Instalar en otra PC

Se reparte el instalador de Velopack (`AsoAdminis-win-Setup.exe`, en los Releases de GitHub), que
además actualiza la app sola. Cómo generar una versión: "Publicar una versión" más abajo. Cómo
instalar en una PC sola o en varias cajas contra un SQL Server compartido:
**[INSTALACION.md](INSTALACION.md)**.

## Desarrollo

Requisitos: .NET 8 SDK, Windows 10/11 y SQL Server LocalDB (viene con Visual Studio).

```bash
cd ASO_ADMINIS.Desktop
dotnet run
```

O abrir `ASO_ADMINIS.slnx` en Visual Studio 2022. Pruebas: `dotnet test ASO_ADMINIS.slnx`.

La base de desarrollo es un archivo LocalDB en `ASO_ADMINIS.Desktop/App_Data/AsoAdminis.mdf`
(no se sube al repo). Al arrancar, la aplicación **aplica sola las migraciones pendientes** y, si
la base no tiene usuarios, pide el nombre de la organización y crea el usuario desarrollador. No
hay usuarios ni contraseñas por defecto.

Para apuntar a otro SQL Server, copiar `ASO_ADMINIS.Desktop/appsettings.local.example.json` como
`appsettings.local.json` y poner ahí la cadena de conexión (no se sube al repo ni viaja en el
instalador).

## Publicar una versión (Velopack)

El instalador y las actualizaciones automáticas van con [Velopack](https://velopack.io)
(`dotnet tool install -g vpk`). Desde la raíz del repo, subiendo antes `<Version>` en
`ASO_ADMINIS.Desktop/ASO_ADMINIS.Desktop.csproj`:

```bash
dotnet publish ASO_ADMINIS.Desktop -c Release -r win-x64 --self-contained true -o publish
vpk download github --repoUrl https://github.com/JE-Tovar/ASO_ADMINIS -o Releases  # solo desde la 2.ª versión: deja que se generen deltas
vpk pack -u AsoAdminis -v <versión> -p publish -e ASO_ADMINIS.Desktop.exe --packTitle "ASO Adminis" --packAuthors ASO -i ASO_ADMINIS.Desktop/Assets/Logo/aso-adminis.ico -o Releases
vpk upload github --repoUrl https://github.com/JE-Tovar/ASO_ADMINIS -o Releases --publish --releaseName "<versión>" --tag v<versión> --token <token>
```

`-u AsoAdminis` (el packId) no cambia nunca entre versiones: es la identidad de la app para
Velopack. Se reparte `Releases/AsoAdminis-win-Setup.exe` (sin asistente ni permisos de
administrador). Instalada, la app vive en `%LocalAppData%\AsoAdminis`, busca versiones nuevas en
los Releases de GitHub después del login y guarda lo de la máquina (`App_Data` y
`appsettings.local.json`) en `%LocalAppData%\ASO Adminis`, fuera de lo que reemplaza cada
actualización. `appsettings.local.json` nunca entra al paquete (el repo y sus releases son
públicos). La máquina destino necesita **LocalDB**, salvo que `appsettings.local.json` apunte a
otro SQL Server. El `.exe` no está firmado: SmartScreen puede avisar "Editor desconocido" la
primera vez.

## Estructura

```
ASO_ADMINIS/
├── ASO_ADMINIS.slnx
├── ASO_ADMINIS.Desktop/      # Aplicación WPF (MVVM ligero)
│   ├── Models/               # Entidades del dominio
│   ├── Services/             # Servicios de dominio, sesión/permisos, fiscal, contratos de datos
│   ├── ViewModels/           # Lógica de presentación
│   ├── Views/                # Pantallas y editores
│   ├── Navigation/           # Catálogo de módulos y submódulos (fuente única)
│   ├── Configuration/        # Configuración y composición de fuentes de datos
│   ├── BD/                   # EF Core / SQL Server (DbContext + fuentes Sql…)
│   ├── Migrations/           # Migraciones EF Core
│   ├── Controls/             # Sidebar y componentes reutilizables
│   └── Styles/               # Paleta y estilos
└── ASO_ADMINIS.Tests/        # Pruebas xUnit (en memoria, sin SQL Server)
```

## Módulos

| Módulo | Submódulos |
|---|---|
| Ventas | Registro de ventas (factura, nota de crédito y débito, contingencia) · Clientes · Cierre del día (X/Z) |
| Finanzas | Cuentas por Pagar · Gastos varios · Banco · Proveedores |
| Inventario | Almacén · Entradas · Salidas |
| Reportes | Ventas · Gastos · Libro de ventas |

Además hay **cuatro secciones fijas** según el rol: **Inicio**, **Peticiones**, **Administración**
(usuarios y permisos, datos de la organización y tasa BCV) y **Configuración** (tema, escala,
contraseña propia, caja de la máquina). Las preferencias se guardan en
`%AppData%\ASO Adminis\ajustes.json`, no en la base de datos.

## Roles

Cuatro roles genéricos (Operador, Supervisor, Administrador de organización, Desarrollador), sin
atar todavía a ningún puesto real — ver la tabla de "Roles y permisos" y "PROVISIONAL" en
`CLAUDE.md`. **Una sola organización trabaja por instalación.**

## Pendiente

- **Emisor fiscal real**: hoy todo se emite con el emisor **simulado**, sin valor fiscal. Falta el
  conector de la máquina fiscal o imprenta digital que elija el cliente.
- Roles reales, color de marca y razón social: ver "PROVISIONAL" y "Próximo paso sugerido" en
  `CLAUDE.md`.
