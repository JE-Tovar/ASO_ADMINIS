# Instalar ASO Adminis en otra PC

El instalador es el de **Velopack**: `AsoAdminis-win-Setup.exe`, publicado en los Releases de
GitHub (cómo generar una versión: README, "Publicar una versión"). No pide permisos de
administrador, no necesita .NET instalado (va dentro) y después **se actualiza sola**: tras el
login busca una versión nueva y ofrece reiniciar.

Instalada, la app vive en `%LocalAppData%\AsoAdminis` y guarda lo de la máquina en
`%LocalAppData%\ASO Adminis` (la base LocalDB en `App_Data\` y, si existe,
`appsettings.local.json`), fuera de lo que reemplaza cada actualización.

El `.exe` no está firmado: la primera vez Windows SmartScreen puede avisar "Editor desconocido"
(→ *Más información* → *Ejecutar de todas formas*).

## Modo 1: una sola PC

Necesita **SQL Server LocalDB**. Si la PC no lo tiene: descargar el instalador de **SQL Server
Express**, elegir "Descargar medios" → **LocalDB** e instalar `SqlLocalDB.msi`. Luego ejecutar
`AsoAdminis-win-Setup.exe`. No hay nada que configurar.

LocalDB es **por usuario de Windows**: si dos personas usan la misma PC con cuentas distintas,
cada una vería su propia base. En ese caso, usar el modo red apuntando a esa misma PC.

## Modo 2: varias cajas en red

Los datos viven en **una** PC que hace de servidor, con **SQL Server Express** (gratuito). Las
demás se conectan a ella.

**En el servidor (una sola vez):**
1. Instalar SQL Server Express (instancia por defecto `SQLEXPRESS`).
2. *SQL Server Configuration Manager* → Protocolos de SQLEXPRESS → habilitar **TCP/IP**; activar
   y arrancar el servicio **SQL Server Browser**; reiniciar el servicio de SQL Server.
3. Firewall de Windows: permitir **TCP 1433** y **UDP 1434** (o el programa `sqlservr.exe`).
4. En una red de trabajo sin dominio lo más simple es un **usuario SQL**: en SQL Server
   Management Studio, habilitar autenticación mixta, crear un login (p. ej. `aso`) y darle el
   rol `dbcreator` (la app crea la base sola la primera vez).

**En cada puesto (también en el servidor, si se usa como caja):**
1. Ejecutar `AsoAdminis-win-Setup.exe`.
2. Crear `%LocalAppData%\ASO Adminis\appsettings.local.json` con la cadena del servidor (la
   barra invertida va doble):

   ```json
   {
     "ConnectionStrings": {
       "AsoAdminisDb": "Server=NOMBRE-DEL-SERVIDOR\\SQLEXPRESS;Database=AsoAdminis;User Id=aso;Password=LA_CLAVE;TrustServerCertificate=True;"
     }
   }
   ```

   Con dominio de Windows, en lugar de `User Id`/`Password` va `Trusted_Connection=True;`.
3. Abrir la app. Todas las cajas deben usar el mismo `Database`.

El archivo está en la carpeta de cada usuario de Windows: si varias personas usan la misma caja
con cuentas distintas, cada una necesita su copia.

> La contraseña SQL queda en texto plano en ese archivo. Usar un login dedicado solo a esta base.

## Primer arranque

Contra una base vacía, la aplicación **crea las tablas sola** (aplica las migraciones) y muestra
la pantalla de primer arranque: nombre y código de la organización y el usuario Desarrollador.
Después, en Administración · Organización, completar los datos fiscales (razón social, RIF,
dirección). Luego, en **Administración · Cajas**, crear una caja por puesto de venta (CAJA-01,
CAJA-02…) y, en **Administración · Usuarios**, crear los usuarios de rol **Cajero** con su caja
asignada. La caja va con el usuario, no con la PC: un cajero factura en su caja desde cualquier
puesto. Para facturar, cada cajero abre su turno en **Ventas · Cierre del día** (con el fondo de
la gaveta) y lo cierra con el arqueo al terminar.

> Al actualizar desde una versión anterior a cajas y turnos, las cajas que ya figuraban en las
> ventas se crean solas, pero hay que asignarle su caja a cada usuario que venda.

## Actualizaciones

Llegan solas desde GitHub Releases. Con varias cajas en red conviene que todas se actualicen el
mismo día: la primera que abra la versión nueva migra la base, y una caja vieja contra una base
nueva puede fallar.

## Contingencia fiscal

Si el emisor fiscal no responde, la factura se puede hacer en un **formato de contingencia** en
papel, de una imprenta autorizada. Antes de necesitarlo, un administrador tiene que cargar en
**Administración · Contingencia** el rango de números de control impreso en esos formatos (imprenta,
su RIF, la autorización, y el primero y el último, por ejemplo `00-000001` al `00-000500`). **Sin un
talonario activo no se puede registrar ninguna factura en contingencia**, y no se acepta un número
fuera de rango ni repetido. El formulario propone el siguiente libre y avisa cuando quedan pocos.

Para saber si el emisor de una caja responde, sin emitir nada: **Ventas · Cierre del día · Probar
emisor**.

## Seguridad de las cuentas

- **Contraseñas:** al menos 8 caracteres, con letras y números, y sin el nombre de usuario. Cuando
  un administrador crea un usuario o le pone una contraseña nueva, esa persona tiene que cambiarla
  por una suya al entrar.
- **Bloqueo:** 5 intentos fallidos seguidos bloquean la cuenta 15 minutos. Un administrador la
  desbloquea antes en Administración · Usuarios (editar → *Desbloquear al guardar*, o ponerle
  una contraseña nueva).
- **Inactividad:** tras 15 minutos sin uso se pide la contraseña de nuevo (o *Cambiar de usuario*).
  Se ajusta, o se apaga con 0, en Administración · Organización.
- Si un administrador cambia el rol, los permisos o la caja de alguien, o lo desactiva, se aplica
  en su sesión abierta en menos de un minuto.

## Registro de errores

Cada PC guarda su registro técnico en `%LocalAppData%\ASO Adminis\logs\` (un archivo por día, se
conservan 30). Si algo falla, el aviso de la aplicación dice dónde está; también se abre desde
**Configuración · Aplicación · Abrir carpeta de registros**. Es lo que hay que enviar a soporte.

Quién cambió qué y cuándo (precios, permisos, artículos…) queda en la base, en
**Administración · Bitácora**, y no se puede borrar.

## Respaldo

La aplicación respalda sola, **una vez al día**, en el primer inicio de sesión de cualquier PC.
Si dos PCs entran a la vez, solo una lo hace. Cada respaldo son dos archivos:

- **`aso-respaldo-AAAAMMDD-HHMMSS.bak`**: copia nativa de SQL Server, verificada con
  `RESTORE VERIFYONLY`. Es la restauración completa y rápida, pero **solo sirve en SQL Server**.
  La escribe el **servidor** de base de datos: con LocalDB queda en
  `%LocalAppData%\ASO Adminis\Respaldos\`; con SQL Server Express, en la carpeta de respaldos de
  la instancia (normalmente `C:\Program Files\Microsoft SQL Server\MSSQL…\MSSQL\Backup\` del servidor).
- **`aso-exportacion-AAAAMMDD-HHMMSS.zip`**: exportación portable (un JSON por tabla y un
  manifiesto). No depende del motor: es la que servirá para pasar los datos a PostgreSQL. Queda en
  `%LocalAppData%\ASO Adminis\Respaldos\` de la PC que respaldó.

Se conservan los **14 más recientes** de cada tipo (los `.bak` solo se limpian solos si la base
está en la misma PC). El historial, y el botón **Respaldar ahora**, están en
**Administración · Respaldos**.

**Copiar los respaldos fuera de la máquina.** Un respaldo en el mismo disco que la base no sirve
si se pierde ese disco: copiar la carpeta de respaldos a otro disco, un pendrive o la nube, por
ejemplo con una tarea programada de `robocopy`.

**Carpetas a medida** (`appsettings.local.json`):

```json
"Respaldos": {
  "Carpeta": "D:\\Respaldos\\ASO",
  "CarpetaServidor": "D:\\Respaldos\\SQL"
}
```

`CarpetaServidor` es una ruta **del servidor de SQL** y la cuenta del servicio de SQL Server tiene
que poder escribir en ella.

**Programarlo en el servidor** (recomendado en red, para no depender de que alguien entre): en el
Programador de tareas de Windows, una tarea diaria que ejecute

```
"%LocalAppData%\AsoAdminis\current\ASO_ADMINIS.Desktop.exe" --respaldar
```

con el usuario de Windows que tiene la aplicación instalada. No abre ventanas; termina con código
0 si los dos respaldos salieron bien, 1 si alguno falló y 2 si no pudo empezar (el detalle queda en
el registro de errores).

**Restaurar un .bak** (con la aplicación cerrada en todas las cajas): en SQL Server Management
Studio, clic derecho en *Bases de datos* → *Restaurar base de datos…* → *Dispositivo* → elegir el
`.bak` → en *Opciones*, marcar *Sobrescribir la base de datos existente*. O con `sqlcmd`:

```
sqlcmd -S .\SQLEXPRESS -Q "RESTORE DATABASE [AsoAdminis] FROM DISK = N'C:\...\aso-respaldo-....bak' WITH REPLACE"
```

Conviene **probar una restauración** de vez en cuando con otro nombre de base (`AsoAdminisPrueba`,
con `WITH MOVE` para los archivos), para saber que el respaldo sirve antes de necesitarlo.

Además, respaldar `%AppData%\ASO Adminis\diario-fiscal.jsonl` de cada caja si existe: anota
documentos fiscales emitidos que no se pudieron guardar en la base.
