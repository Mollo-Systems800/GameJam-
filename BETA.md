# Beta del laberinto

Abre `Assets/00_Scenes/BetaMaze.unity` en Unity 6000.5.6f1 y pulsa Play. La escena beta es la primera de Build Settings. El escenario se construye al iniciar Play; en modo edición solo se ve su configuración. SampleScene se conserva.

## Controles

- WASD o flechas: desplazamiento relativo a la cámara.
- Shift izquierdo: correr.
- Espacio: saltar. Incluye una breve tolerancia al salir de un borde y al pulsar justo antes de aterrizar; no hay doble salto.
- Mando: stick izquierdo para moverse, L3 para correr.
- Botón inferior del mando (A en Xbox / cruz en PlayStation): saltar.
- B o Select del mando: abrir o cerrar la tienda. También hay un botón en pantalla.
- Escape: pausar o continuar. Los botones de pausa y fin permiten repetir la misma semilla o generar otra.

## Reglas de esta versión

El jugador comienza en una esquina de un laberinto de 12 por 12 celdas. Hay tres salidas exteriores por defecto, todas marcadas SALIDA; cruzar cualquiera termina la partida. `Exit Count` permite configurar entre una y ocho. Se distribuyen en celdas distintas del perímetro, evitando el inicio. El cartel «SE BUSCA EMPLEO» persigue por los pasillos; si alcanza al jugador, causa derrota.

Hasta diez tramos rectos contienen precipicios con bordes de despegue/aterrizaje y plataformas elevadas. Los huecos no tienen suelo invisible debajo. Espacio permite saltar a la plataforma o cruzar el tramo; caer por debajo de -3 termina la partida. Los tramos se separan entre sí y evitan el inicio, las salidas y sus celdas adyacentes. El boss flotante cruza estos huecos siguiendo su ruta. El número efectivo puede ser menor en laberintos pequeños o con pocos pasillos rectos.

La velocidad inicial del jugador es 4 unidades por segundo y correr aplica un multiplicador de 1,35. Cada 60 segundos pierde 0,35 de velocidad, hasta un mínimo de 2,6. Recoger una caja de antidepresivos restaura la velocidad inicial y reinicia el contador de fatiga. Este efecto es una decisión provisional para la beta.

El boss empieza a 2,2 unidades por segundo, espera 5 segundos antes de perseguir y aumenta 0,1 cada 30 segundos desde el comienzo de la partida. La pausa detiene los tiempos de juego. Se distribuyen hasta 12 cajas en celdas diferentes con suelo seguro, evitando inicio, boss, salidas y obstáculos.

## Configuración

Selecciona `Beta Game - Configuracion` en la escena para cambiar dimensiones, cantidad de salidas, semilla, tamaño de celda, velocidades, intervalos, cantidad de cajas, obstáculos, altura del salto, altura de caída mortal y cámara. Desactiva `Random Seed` para reproducir la semilla del Inspector. La semilla efectiva se muestra en pantalla. Los saltos están dimensionados para las celdas y velocidades predeterminadas; al cambiar escalas extremas hay que ajustar también el salto.

`Wall Prefab` y `Floor Prefab` usan las piezas de 3 metros del Maze Modular Kit. Se normalizan sus pivotes y dimensiones; colisionadores propios dan grosor a los pisos e impiden atravesar las paredes. `Player Prefab` referencia el prefab Player y el jugador gira hacia donde se mueve.

El jugador se instancia desde `03_Prefabs/Player.prefab`: incluye `CharacterController`, el script `Player` y un hijo `Character Visual` con el modelo `rigAnimated.fbx` mostrado en la referencia. También se puede arrastrar este prefab a una escena con cámara y suelo para probar movimiento y salto. En Play se ajusta la altura visual a 1,8 y se desactiva root motion para que el controlador gestione el desplazamiento. `rigged character low poly.fbx` y su animación de reposo se conservan como recursos alternativos; no se superponen dos cuerpos al jugador.

El prefab incluye `PlayerLimbAnimator`. El FBX proporcionado tiene mallas separadas y carece de huesos o clips de locomoción; al iniciar se crea un esqueleto procedural de caderas, torso, brazos, antebrazos, muslos y rodillas. Se generan copias de sus mallas con pesos suaves para animación GPU, sin modificar las mallas originales. Read/Write está habilitado en el importador de rigAnimated para construir esas copias. Las formas de control `cs_` se ocultan y se excluyen del cálculo de altura.

Los brazos y piernas se alternan según el desplazamiento real: contra una pared no sigue caminando. Correr y el power-up de velocidad aceleran el ciclo. En el aire flexiona rodillas y adelanta brazos; al caer cambia de pose y al aterrizar comprime ligeramente el cuerpo. Reposo y cambios de estado se mezclan suavemente. Pausa, tienda y fin de partida congelan la pose. `Stride Length`, `Leg Swing`, `Arm Swing` y `Blend Speed` se ajustan en el prefab. Es una animación provisional por pesos espaciales, pensada para esta beta; los clips artísticos pueden sustituirla más adelante.

`Platform Prefab` usa `03_Prefabs/JumpPlatform.prefab`, una plataforma simulada a partir del piso del kit, con colisión y tamaño adaptados al tramo. El boss usa `03_Prefabs/SolicitudTrabajo.fbx`, copiado del archivo proporcionado. Este FBX referencia una imagen externa `Solicitud.jpeg` que no se recibió: mientras falte, se aplica un material y un rótulo provisionales al modelo. Para recuperar su apariencia original hay que importar esa textura y asignarla a su material.

`Pill Visual Prefab` admite un futuro modelo o prefab con sprite; mientras está vacío se muestran cajas geométricas. Los recursos visuales se ajustan y se desactivan sus colisionadores para que la lógica use los del juego. Estos campos son solo para presentación, no deben contener scripts de comportamiento.

## Algoritmos y alcance

### Enemigos, monedas y tienda

La beta añade hasta seis Spade y cuatro torretas, además del boss. `03_Prefabs/SpadeEnemy.prefab` usa el modelo `Spade.fbx` proporcionado. Cada Spade describe un círculo alrededor de un ancla fija dentro de su celda, sin perseguir al jugador. Su esfera sólida ocupa temporalmente el paso y hace un punto de daño al contacto. Se puede esperar a una abertura en su órbita o saltar para esquivarlo. El FBX referencia `texture/spade_texture.png`, que no estaba entre los archivos disponibles; se conserva la malla con material rojo provisional.

`TurretEnemy.prefab` genera una base y un cañón provisionales, apunta al jugador dentro de 12 unidades y dispara cada 2,2 segundos si tiene línea de visión. `EnemyProjectile.prefab` recorre una trayectoria recta y comprueba su recorrido completo para chocar con paredes y jugadores incluso a alta velocidad. Los proyectiles desaparecen al impactar o después de cinco segundos. Las torretas se colocan hacia un lado de su celda para dejar paso.

El jugador tiene tres puntos de vida. Spade y proyectiles quitan uno y conceden 1,2 segundos de protección contra impactos repetidos. El boss sigue siendo letal al contacto sin protección; las caídas mortales no se pueden bloquear con un escudo. Las pastillas siguen restaurando velocidad, sin sustituir las monedas o la curación.

Se distribuyen hasta 36 `CoinPickup.prefab` en suelo seguro. Cada moneda añade una unidad al saldo de esa partida. La tienda pausa al jugador, enemigos, proyectiles, efectos y cronómetro; al cerrarla se recupera el estado anterior, incluida una pausa que ya estuviera activa.

| Power-up | Precio | Efecto inmediato |
| --- | --- | --- |
| Velocidad | 6 monedas | Multiplicador x1,5 durante 10 segundos |
| Escudo | 8 monedas | Protección contra daño de enemigos durante 8 segundos |
| Curación | 4 monedas | Recupera un punto de vida, hasta tres |

Las compras requieren saldo suficiente. Curación se deshabilita con la vida completa. Volver a comprar velocidad o escudo renueva su duración, sin acumular multiplicadores. Las monedas, vida y mejoras se reinician al repetir una partida. Todos los enemigos, monedas y balas pertenecen al escenario generado y se eliminan al reiniciarlo. En mapas pequeños puede haber menos enemigos o monedas que la cantidad configurada.

### Tiempo y puntuación

El cronómetro mide tiempo activo: las pausas y la tienda no cuentan. La puntuación provisional es `max(0, 10000 - floor(segundos * 20)) + monedas recogidas * 50`; gastar monedas no reduce el premio por haberlas encontrado. La pantalla final muestra el tiempo y el score. Solo escapar actualiza el mejor tiempo y la mejor puntuación, que se guardan localmente con PlayerPrefs y se conservan al cerrar el juego. Los récords de distintas semillas se agrupan en esta beta.

### Generación

DFS aleatorio iterativo genera un árbol conectado: todas las celdas son accesibles y existe una ruta única entre cada par. BFS elige primero la celda más distante del inicio situada en el perímetro y después distribuye las otras salidas según su distancia al inicio y a las salidas ya elegidas. Cada salida abre un borde exterior. La generación y los obstáculos usan semillas reproducibles. El boss también usa BFS y cambia de pasillo desde los centros de las celdas para evitar recortar esquinas a través de paredes. No necesita hornear un NavMesh.

Esta beta genera el laberinto al comenzar cada partida. Todavía no transforma las paredes durante la persecución, no exige llaves y usa animación procedural y arte provisional. La pantalla de victoria muestra tiempo y puntuación. El balance de dificultad queda abierto para la jam.

## Verificación

`pwsh -File Tools/VerifyMaze.ps1` prueba el algoritmo sin abrir Unity: 1000 semillas, dimensiones hasta 40, conectividad, simetría de pasillos, una o varias salidas, ausencia de ciclos, salida lejana, rutas BFS, reproducibilidad, variación y selección segura de obstáculos no adyacentes.

`pwsh -File Tools/VerifyRunFeatures.ps1` comprueba saldos, compras sin fondos, precios, curación, escudo, duración de velocidad, daño e invulnerabilidad, puntuación, reinicio y 1000 distribuciones reproducibles de enemigos/monedas. Ejecuta cada script en un proceso independiente de PowerShell para evitar duplicar los tipos de prueba.

`pwsh -File Tools/VerifyPlayerRig.ps1` comprueba más de 28000 posiciones del personaje: pesos normalizados y no negativos, índices de huesos válidos, simetría y asignación de pies, manos y torso. Las pruebas Play Mode también verifican que el rig se inicialice y flexione las rodillas durante el salto.

Los scripts de juego y de editor pueden compilarse con el compilador y las referencias de Unity. `MazeBetaVerification.Run` prepara pruebas automáticas en Play Mode de movimiento, colisiones, objetos recogibles, fatiga, persecución, aceleración, salto y aterrizaje, huecos reales, caída mortal, todas las salidas, derrota y reintentos. También comprueba compras y pausa de tienda, recogida de moneda, órbita fija, impactos de bala y línea de visión de torretas; los récords se desactivan durante la prueba. Ejecutar desde terminal, sin `-quit`:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe' -batchmode -projectPath 'D:\Descargas\GameJam\GameJam-' -executeMethod MazeBetaVerification.Run -logFile 'D:\Descargas\GameJam\GameJam-\Logs\beta-verification.log'
```

El verificador finaliza automáticamente el editor: usarlo sin trabajo sin guardar. La ejecución inicial de Play Mode quedó pendiente porque Unity se cerró antes de cargar el proyecto; el registro indicó un fallo de validación del servicio de licencias. La compilación estática y las pruebas de topología se verifican por separado y no sustituyen una prueba visual y jugable.
