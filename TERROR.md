# Ambiente nocturno

BetaMaze mantiene las tres áreas y sus prefabs de ambientación, con cielo y fondo azul casi negro, luz lunar débil, luz ambiental fría y niebla oscura. Se aplica tanto al entrar como al generar cada nivel/reintento, incluida la cámara de cinemáticas.

Los puntos de luz son lámparas de oficina sobre soportes finos, con difusor cálido y sombras. Tienen variaciones lentas y leves de intensidad. La salida usa un punto verde de emergencia estable. El inicio, salida y extremos del parkour reciben luz; hay puntos adicionales sobre el camino y ramificaciones según la semilla. Los postes no se colocan dentro de los precipicios ni añaden colisiones al recorrido.

Una luz corta de orientación sigue a la cámara para leer al personaje, las plataformas y las púas sin iluminar todo el laberinto. Solo existe una por nivel; los puntos anteriores se eliminan al regenerar. El número de lámparas está limitado y la selección es reproducible con la semilla.

En **Beta Game → Terror: cielo oscuro y luces de oficina** se pueden cambiar colores, niebla, intensidad lunar, máximo de lámparas y estos tres prefabs:

- `Assets/03_Prefabs/OfficeNightLamp.prefab`
- `Assets/03_Prefabs/OfficeExitBeacon.prefab`
- `Assets/03_Prefabs/PlayerGuideLight.prefab`

Los Light de cada prefab permiten editar intensidad, color, rango, cono y sombras. `HorrorLamp` controla la oscilación suave. Configuración: Tools → Maze Beta → Configure horror atmosphere.

Verificación nativa de los nueve niveles: `Logs/verify-horror-atmosphere.request`. Resultado en `Logs/horror-atmosphere-verification-result.txt`, exposición en `Logs/horror-lighting-exposure.txt` y capturas `Logs/horror-area-*-player.png`, `horror-area-*-overview.png`, `horror-parkour.png` y `horror-cinematic-player.png`.
