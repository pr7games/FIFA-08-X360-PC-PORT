# 1.0.0

Primera versión estable final. Las entregas de desarrollo anteriores se conservan como preliminares archivadas.

- FSR 1.0.2 oficial con reconstrucción EASU a la resolución seleccionada. Calidad a 1080p utiliza un buffer interno de 1600×900.
- Corrección del tamaño del radar en todos los perfiles y resoluciones FSR, incluido 4K Calidad. Los modos nativos conservan su radar.
- Los nativos mantienen su resolución interna y de salida. RCAS aplica nitidez a 1:1; 720p y 1080p usan mayor nitidez y omiten FXAA para conservar textos y detalles. El launcher explica este comportamiento.
- Interfaz simplificada: VSync sin texto adicional, aviso FSR unificado y contador con FPS, API, buffer, resolución, ventana y acceso a F10.
- El launcher muestra «FSR» y conserva las opciones de idioma y actualización.
- D3D12 evita consultas redundantes de la fence de lectura cuando las escrituras ya terminaron. Se conservan ROV, el público y las protecciones de sincronización de arena.
- El limitador mantiene plazos fijos, utiliza un temporizador de alta resolución y recupera intervalos perdidos para mejorar el frame pacing.
- Incluye las correcciones existentes de texturas e interpolación de Vulkan.
- Empaquetado desde una instalación local mediante una lista de componentes permitidos y validación de hashes. Se corrige el hash del ejecutable registrado en port-manifest.json.

Los cambios visuales de FSR y nativos fueron confirmados por el usuario. No se ejecutó el juego para preparar este paquete; el rendimiento comparativo de las API y la ausencia total de stuttering no se dan por comprobados. La actualización conserva partidas, archivos del juego, mods y ajustes personales.
