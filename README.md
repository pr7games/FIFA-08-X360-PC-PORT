# FIFA-08-RECOMP-PR7

**Version 1.0.0 — desarrollado por PR7.** Port experimental de FIFA 08 de Xbox 360 para Windows x64, basado en ReXGlue/Xenia. Este repositorio contiene el código mantenido por el proyecto y las adaptaciones del SDK; **no contiene el juego ni una descarga jugable**.

Canal: https://www.youtube.com/@GolazoVirtual7

## Funciones

- Vulkan como API principal y predeterminada; D3D12 como alternativa, resoluciones 720p/1080p/1440p/2160p y límite de FPS configurable.
- FSR 1.0.2 oficial: EASU en los perfiles Calidad/Equilibrado/Rendimiento; RCAS a 1:1 en nativos, con mayor nitidez y sin FXAA en 720p y 1080p. El radar se ajusta en todos los perfiles FSR.
- Launcher e instalador ES/EN, controles configurables e idiomas detectados en la copia del usuario.
- Logros locales, avisos con título y descripción en ES/EN según la interfaz, y sonido opcional; F7 abre los logros y F10 el menú PC.
- Editor de jugadores y texturas, tema claro/oscuro y restauración de elementos.
- Instalador autónomo que extrae los archivos de una copia compatible aportada por el usuario.

## Qué contiene

| Ruta | Contenido |
| --- | --- |
| `native/src`, `native/config`, `native/shaders` | Host C++, políticas de CPU/GPU, configuración de recompilación y shaders propios/adaptados |
| `launcher-src`, `installer-src` | Interfaz y lógica de lanzamiento/instalación en C# |
| `modding/src` | Editor y pruebas de los formatos |
| `sdk-overlay` | Los 56 archivos nuevos o modificados respecto a ReXGlue v0.10.0, con hashes de procedencia |
| `compat` | Dos cabeceras de compatibilidad del SDK bajo su licencia original |
| `patches`, `patch-*.cjs`, `tools/apply-*.py` | Parches y herramientas de adaptación; algunos scripts históricos generan informes locales ignorados por Git |
| `validation` | Pruebas auxiliares de CPU y presentación |
| `scripts`, `build-source.ps1` | Preparación de dependencias y compilación desde fuentes |

Se excluyen ISO/GOD/XEX, archivos BIG/BH, traducción C++ generada a partir del juego, compiladores, SDK completo, ejecutables, DLL, partidas, logros de usuarios, respaldos, capturas y cachés. Tampoco se distribuyen aquí la portada, logos, botones comerciales ni el sonido de Xbox usado localmente.

## Requisitos para compilar

Windows x64, Visual Studio Build Tools con MSVC y Windows SDK (el desarrollo original usó VS 2019), .NET Framework 4.x con `csc.exe`, Clang/LLVM con C++23, CMake **3.25 o superior**, Ninja, Node.js y Python **3.10 o superior**. Ejecutar los comandos en una consola de herramientas x64 de Visual Studio, con `clang`, `clang++`, `cmake`, `ninja`, `node` y `python` en PATH. La compilación nativa usa AVX2 por defecto; el procesador de destino debe soportarlo.

Las dependencias exactas del SDK se obtienen de sus referencias de submódulos v0.10.0. El bootstrap también descarga DirectX-Headers v1.614.1 y tl::expected v1.1.0. Consultar las licencias de cada dependencia antes de distribuir sus binarios. No se requiere REA para compilar ni ejecutar el proyecto.

## Preparación local

1. Desde la raíz de **esta carpeta de fuentes**, ejecutar:

   ```powershell
   python scripts/bootstrap.py
   powershell -ExecutionPolicy Bypass -File scripts/development-assets.ps1
   ```

   El bootstrap descarga las fuentes oficiales de ReXGlue v0.10.0, valida las bases de los archivos modificados, obtiene dependencias y aplica `sdk-overlay`. Para usar un archivo fuente descargado previamente: `python scripts/bootstrap.py --archive ruta-al-zip`. `--sdk-only` omite las descargas de submódulos; sirve para inspección, no para una compilación completa.

2. Aportar localmente una copia compatible del juego extraída. Colocar sus recursos en `analysis/private/game/` y el `default.xex` correspondiente también en `analysis/private/default.xex`, según `native/fifa08_manifest.toml`. Estas rutas están ignoradas por Git. No subirlas. El instalador comprueba la identidad de la copia compatible mediante SHA-256; otras revisiones no están garantizadas.

3. Compilar:

   ```powershell
   powershell -ExecutionPolicy Bypass -File build-source.ps1 -SkipPackage
   ```

   Se construye primero el generador del SDK, se genera localmente la traducción del juego, se aplican las adaptaciones de CPU y se compilan el port, launcher, editor e instalador. Para recompilar con dependencias ya preparadas puede usarse `-SkipBootstrap`. Los `.cmd` de las interfaces conservan el uso del compilador .NET Framework de Windows; ajustar su ubicación si el equipo difiere.

4. Para generar la distribución integrada, aportar los recursos visuales que se tenga derecho a utilizar dentro de `launcher-assets/` y un MP3 propio o autorizado en `sounds/logro-xbox.mp3`. Los recursos de desarrollo generados por el script son placeholders neutrales y **no reproducen la portada comercial de la versión local**. Después ejecutar:

   ```powershell
   powershell -ExecutionPolicy Bypass -File crear-distribucion.ps1
   ```

   Resultado: `dist/FIFA08-PC-Setup.exe`. No contiene los archivos del juego. El sonido personalizado es opcional en ejecución: si no está disponible o el decodificador falla, se usa el sonido del sistema. El empaquetador actual exige ese MP3 para la distribución con sonido personalizado. Los iconos PlayStation requieren recursos locales adicionales y su licencia correspondiente.

La compilación completa desde una descarga limpia de Internet no se ha repetido para este paquete público. Se han comprobado los fuentes de las interfaces, la integridad del overlay y los scripts de preparación; los binarios de desarrollo existentes sí fueron compilados y probados durante el desarrollo. Instalar dependencias nuevas o compilar en otros equipos puede requerir ajustes.

## Estructura de una instalación nueva

```text
FIFA 08.exe
FIFA 08 Launcher.exe
FIFA08 editor by pr7.exe
bin/       # componentes, configuración y documentación
game/     # archivos del juego aportados por el usuario
mods/      # exportaciones y respaldos del editor
data/      # partidas, logros, caché y registros
```

`FIFA 08.exe` es la entrada de lanzamiento; el host nativo y sus DLL están dentro de `bin/native/build/`. El instalador conserva internamente otra estructura de payload y la distribuye en estas cuatro carpetas al instalar. No migra ni elimina automáticamente instalaciones antiguas.

## Estado y limitaciones de 1.0.0

Vulkan funciona mejor en varios equipos probados. Esta versión incluye las correcciones de la interpolación de capas de textura en Vulkan y la lectura del exponente del descriptor. La captura guardada de la camiseta confirma la diferencia antes/después; se elimina la compensación anterior de intensidad. Se conserva la constante original compartida de animación. D3D12 conserva reportes de oscurecimiento y tintes intermitentes en arena, especialmente en una RTX 3050 móvil. Las últimas correcciones de sincronización están incluidas, pero **su eliminación definitiva no está confirmada**. El rendimiento varía con GPU, CPU, resolución, controladores y pantallas. FSR 1 es espacial: no reconstruye detalle temporal ni equivale al renderizado nativo.

Los logros son locales y no se sincronizan con Xbox Live; las funciones en línea del juego original no están implementadas como servicio propio. El proyecto no tiene asociación ni respaldo de Electronic Arts, FIFA, Microsoft, Sony, AMD o NVIDIA.

Los cambios de 1.0.0 incluyen Calidad a 900p para salida 1080p, corrección del radar en todas las resoluciones FSR, nitidez RCAS a resolución nativa, omisión de FXAA en nativos 720p/1080p, menos consultas redundantes de sincronización en D3D12 y mejoras del limitador de fotogramas. Se conservan ROV y las protecciones de sincronización de arena. No se afirma paridad de rendimiento entre las API. Ver [CHANGELOG.md](CHANGELOG.md).

Para empaquetar los binarios locales ya comprobados sin recompilar el juego ni copiar partidas o recursos del juego:

```powershell
powershell -ExecutionPolicy Bypass -File crear-distribucion.ps1 -InstalledRoot "F:\Proyectos Port\fifa08-research" -StandaloneDestination "dist\FIFA08-PR7-Setup-1.0.0.exe"
```

`-InstalledRoot` toma los componentes permitidos de la instalación indicada; compila el launcher y el instalador desde estas fuentes y valida los hashes de ambos backends. Las configuraciones incluidas son valores iniciales, no los ajustes personales de esa instalación.

## Releases y actualizaciones

`VERSION` identifica esta base como **1.0.0**. El instalador completo se distribuye en [Releases](https://github.com/pr7games/FIFA-08-X360-PC-PORT/releases).

El launcher consulta la ultima release estable publica de este repositorio al abrirse. Si hay una version mas reciente, marca la pestana Acerca de. Al pulsar Actualizar version, descarga el instalador completo con porcentaje de progreso, comprueba el SHA-256 publicado por GitHub y extrae sus componentes sin pedir otra ISO/GOD. Un proceso auxiliar espera el cierre del launcher, actualiza sus ejecutables y componentes en `bin/`, y lo abre de nuevo. Si una copia falla, intenta restaurar los archivos anteriores y conserva el respaldo temporal si la recuperacion requiere intervencion. El registro esta en `data/logs/update.log`.

No usa deltas. Conserva `game/`, `mods/`, `data/`, `bin/launcher-settings.json` y `bin/launcher-controls.json`. Se requiere cerrar el juego y el editor antes de actualizar. La instalacion debe tener la estructura limpia documentada arriba y acceso a Internet. La comprobacion no necesita una cuenta de GitHub; esta sujeta a sus limites de consultas publicas. No contiene credenciales.

Las publicaciones anteriores, inicialmente numeradas 1.0.0 y 1.0.1 durante el desarrollo, se conservan como preliminares archivadas. La numeración estable comienza con esta entrega 1.0.0; el código, launcher e instalador comparten esa versión.

Para publicar futuras actualizaciones: incrementar `VERSION` y `PortUpdater.CurrentVersion` en `launcher-src/Updater.cs`, compilar el launcher de produccion (sin `UPDATE_TEST_BUILD`), crear el instalador completo y adjuntarlo a una release estable `vX.Y.Z` con nombre **`FIFA08-PR7-Setup-X.Y.Z.exe`**. GitHub debe proporcionar el digest SHA-256 del asset. No reemplazar silenciosamente versiones ya publicadas. El define `UPDATE_TEST_BUILD` compila una version local 0.0.5 para probar la deteccion; nunca debe entrar en un paquete de produccion.

Pruebas del actualizador: `powershell -File tests/test-updater.ps1` (versiones, JSON, URL, integridad, rollback y preservacion de datos). Agregar `-Installer "ruta/al/instalador.exe"` para probar el paquete real o `-CheckGitHub` para descargar, verificar y probar la ultima release en una instalacion temporal aislada.

Los futuros cambios se mantendrán principalmente en `native`, `sdk-overlay`, las interfaces y los scripts. Tras editar un archivo del overlay, actualizar su SHA-256 en `sdk-overlay/manifest.json`; `upstream_sha256` sigue identificando la base v0.10.0. Antes de subir, revisar `git status` y no forzar la inclusión de entradas ignoradas.

## Licencias

Las contribuciones originales de PR7 se publican bajo **MIT**: ver `LICENSE`. Las partes de ReXGlue/Xenia mantienen **BSD-3-Clause**; las adaptaciones procedentes del proyecto FIFA09/NOBK conservan sus avisos y licencia **MIT**. Ver `THIRD_PARTY_NOTICES.md`, `LICENSES/` y las cabeceras de los archivos. La licencia del código no concede derechos sobre el juego, logos, música, sonidos, imágenes o marcas comerciales.

Software experimental proporcionado sin garantías. Para informar errores: indicar versión, API, GPU, resolución/FSR y pasos de reproducción; adjuntar solo registros revisados, sin datos personales ni archivos del juego.
