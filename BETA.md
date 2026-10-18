# Beta del laberinto

Abre `Assets/00_Scenes/BetaMaze.unity` en Unity 6000.5.6f1 y pulsa Play. La escena beta es la primera de Build Settings. El escenario se construye al iniciar Play; en modo edición solo se ve su configuración. SampleScene se conserva.

## Controles

- WASD o flechas: desplazamiento relativo a la cámara.
- Ratón o stick derecho: orientar la cámara detrás del jugador, con límite vertical para evitar voltear la vista.
- Shift izquierdo: correr.
- C, Ctrl izquierdo/derecho o botón derecho del mando (B en Xbox / círculo en PlayStation): voltereta.
- Espacio: saltar. Incluye una breve tolerancia al salir de un borde y al pulsar justo antes de aterrizar; no hay doble salto.
- Mando: stick izquierdo para moverse, L3 para correr.
- Botón inferior del mando (A en Xbox / cruz en PlayStation): saltar.
- B del teclado o Select del mando: abrir o cerrar la tienda. También hay un botón en pantalla.
- Escape: pausar o continuar. Los botones de pausa y fin permiten repetir la misma semilla o generar otra.

## Reglas de esta versión

`Sprinting Forward Roll.fbx` se conserva en `03_Prefabs` y su adaptación al rig actual está asignada como `ForwardRoll.json` en `Player.prefab`. La voltereta dura 1,17 segundos, avanza a 6 unidades por segundo y tiene 0,6 segundos de recarga al terminar. Usa la dirección de movimiento al activarse, o el frente de la cámara si el jugador está quieto. Solo comienza con apoyo en el suelo, impide saltar durante su ejecución y se interrumpe al caer de un borde. Una comprobación de apoyo bajo los pies y una tolerancia de 0,12 segundos evitan cancelarla por un fotograma sin contacto al cruzar uniones del suelo. La entrada se guarda durante 0,2 segundos para aceptar pulsaciones justo antes de aterrizar. Conserva las colisiones y el daño normales; no concede invulnerabilidad. La pausa y la tienda congelan tanto la animación como su desplazamiento y recarga.

La rotación completa del cuerpo se separa de las rotaciones limitadas de las articulaciones para conservar la vuelta de 360 grados sin retorcer el codo. La cámara y el controlador permanecen verticales. El apoyo visual se calcula con los vértices de la pose actual para seguir el cuerpo recogido durante el giro. `python Tools/ExtractIdleMotion.py --all` regenera también esta animación. El verificador de Play Mode incluye activación, movimiento contra una pared, finalización y pausa de la voltereta; su ejecución sigue pendiente en el editor.

El jugador comienza en una esquina de un laberinto de 12 por 12 celdas. Cada nivel tiene una sola salida exterior, marcada SALIDA, y conserva cruces, bucles y varios caminos internos. La puerta se escoge entre celdas del perímetro cuya distancia Manhattan al inicio alcanza al menos el 75 % de la diagonal en celdas; entre esas candidatas, BFS elige la de mayor distancia por pasillos. En el mapa predeterminado esto exige al menos 17 pasos de separación física en la cuadrícula, además del recorrido por el laberinto. El boss «SE BUSCA EMPLEO» persigue por los pasillos; si alcanza al jugador, causa derrota.

El laberinto incluye una zona continua de parkour: con los valores predeterminados reserva cinco celdas, dos de acceso seguro y tres de plataformas sobre un lecho de púas. Hay tres saltos con 1,8 m de espacio vacío entre bordes. No existe suelo invisible a la altura de las plataformas; debajo hay un fondo sólido a 1,9 m y púas visibles. Tocarlas causa derrota, incluso con escudo. Los hasta diez obstáculos individuales también tienen púas y un hueco amplio que exige saltar. El boss flotante cruza estos tramos siguiendo su ruta. Las zonas evitan inicio y salida, mantienen la conectividad y no generan puertas laterales en medio del parkour. En laberintos con ambos lados menores que cinco celdas no cabe la zona continua y se conservan los obstáculos individuales disponibles.

En `Beta Game - Configuracion > Parkour sobre puas`, `Parkour Enabled` activa el tramo continuo, `Parkour Cell Length` modifica su longitud, `Parkour Gap` controla la distancia vacía entre plataformas, y `Spike Bed Depth`/`Spike Height` ajustan el lecho. El generador limita la separación al alcance del salto a velocidad de fatiga. `Spike Prefab` reemplaza cada púa provisional y `Spike Floor Prefab` reemplaza el fondo y sus laterales. Los campos vacíos usan pirámides visibles y un fondo sólido. `Platform Prefab`, `Jump Takeoff Prefab` y `Jump Landing Prefab` controlan las plataformas y accesos; cada área también puede sobrescribir púas y fondo. Los prefabs visuales se alinean con la superficie real de sus colisiones.

`Tools/VerifyParkour.ps1` prueba 1.000 semillas para comprobar conectividad, reproducibilidad, una salida y huecos sin accesos laterales. `GameJam > Verificar parkour con puas` comprueba la geometría y el controlador real: correr sin saltar termina sobre las púas y los tres saltos se completan a 2,6 unidades/s, sin exigir sprint ni mejoras. Resultado verificado en `Logs/parkour-playmode-result.txt`. El límite global de caída a -3 queda como respaldo fuera de las zonas con púas.

La velocidad inicial del jugador es 4 unidades por segundo y correr aplica un multiplicador de 1,35. Cada 60 segundos pierde 0,35 de velocidad, hasta un mínimo de 2,6. Recoger una caja de antidepresivos restaura la velocidad inicial y reinicia el contador de fatiga. Este efecto es una decisión provisional para la beta.

El boss empieza a 2,2 unidades por segundo, espera 5 segundos antes de perseguir y aumenta 0,1 cada 30 segundos desde el comienzo de la partida. La pausa detiene los tiempos de juego. Se distribuyen hasta 12 cajas en celdas diferentes con suelo seguro, evitando inicio, boss, salidas y obstáculos.

## Campaña: tres áreas de tres niveles

Hay nueve laberintos consecutivos, agrupados en Oficinas (1–3), Archivo (4–6) y Patio (7–9). Cada nivel usa una semilla derivada de la semilla de campaña, de modo que sus mapas son distintos y reproducibles. La pantalla indica área y nivel. Escapar habilita «Siguiente nivel» o «Continuar a la siguiente área»; al terminar el noveno ofrece iniciar una campaña nueva. Reintentar conserva el nivel actual y su ambientación. Vida, monedas, mejoras, tiempo y puntuación se reinician al entrar en cada nivel.

En `Beta Game - Configuracion > Areas`, cada elemento permite cambiar nombre, prefabs de paredes, suelos, núcleo y cubierta de pared, plataforma de salida, marcador, precipicio, plataforma de salto, bordes de salto, silla, señora sentada, terminal de tienda y rótulos. Los campos vacíos heredan los prefabs globales. Las paredes siguen usando Wall_3M, Wall_2M y Wall_Lamp; los pisos usan OfficeSmoothFloor, ArchiveSmoothFloor y CourtyardSmoothFloor, con baldosas planas y tonos propios por área. Los materiales de los prefabs se conservan.

`Start Level` (1–9) permite probar cualquier nivel al iniciar Play. `Minimum Exit Distance Ratio` ajusta la separación mínima respecto al inicio (0,75 por defecto, rango 0,5–1). `Alternative Routes` conserva el control de los caminos interiores; no aumenta el número de salidas. La escena beta ya incluye las referencias de los tres kits.

`Tools/VerifyCampaign.ps1` comprueba 1.000 campañas completas y sus semillas. `Tools/VerifyMaze.ps1` incluye 1.000 mapas con salida única distante y rutas alternativas. En Unity, `GameJam > Verificar campana` prueba el cruce real de la salida, los nueve niveles, los prefabs por área, reintentos y fin de campaña; deja el editor pausado y escribe `Logs/campaign-playmode-result.txt`.

## Configuración

Selecciona `Beta Game - Configuracion` en la escena para cambiar dimensiones, distancia mínima de la salida, semilla, tamaño de celda, velocidades, intervalos, cantidad de cajas, obstáculos, altura del salto, altura de caída mortal y cámara. Desactiva `Random Seed` para reproducir la semilla del Inspector. La semilla efectiva se muestra en pantalla. Los saltos están dimensionados para las celdas y velocidades predeterminadas; al cambiar escalas extremas hay que ajustar también el salto.

La cámara sigue desde detrás del jugador, a 2,6 unidades, apuntando a una altura de 1,3 y con una inclinación inicial de 18 grados. El campo de visión es de 65 grados y el personaje permanece visible con sus animaciones. El ratón o stick derecho giran la vista y orientan el cuerpo; WASD permite avanzar, retroceder y desplazarse lateralmente. Una comprobación esférica acerca la cámara ante paredes y la devuelve suavemente a su distancia al despejarse el camino. Solo si queda pegada al personaje oculta temporalmente el cuerpo para evitar recortes. El ratón se libera en pausa, tienda, fin de partida o al perder el foco. `Camera Target Height`, `Camera Distance`, `Mouse Sensitivity` y `Camera Field Of View` son configurables.

`Wall Prefab` y `Floor Prefab` usan las piezas de 3 metros del Maze Modular Kit. Se normalizan sus pivotes y dimensiones; colisionadores propios dan grosor a los pisos e impiden atravesar las paredes. `Player Prefab` referencia el prefab Player y el jugador gira hacia donde se mueve.

El jugador se instancia desde `03_Prefabs/Player.prefab`: incluye `CharacterController`, el script `Player` y un hijo `Character Visual` con el modelo `rigAnimated.fbx` mostrado en la referencia. También se puede arrastrar este prefab a una escena con cámara y suelo para probar movimiento y salto. En Play se ajusta la altura visual a 1,8 y se desactiva root motion para que el controlador gestione el desplazamiento. `rigged character low poly.fbx` y su animación de reposo se conservan como recursos alternativos; no se superponen dos cuerpos al jugador.

El prefab incluye `PlayerLimbAnimator`. El FBX proporcionado tiene mallas separadas y carece de huesos o clips de locomoción; al iniciar se crea un esqueleto procedural de caderas, torso, brazos, antebrazos, muslos y rodillas. Se generan copias de sus mallas con pesos suaves para animación GPU, sin modificar las mallas originales. Read/Write está habilitado en el importador de rigAnimated para construir esas copias. Las formas de control `cs_` se ocultan y se excluyen del cálculo de altura.

Los brazos y piernas se alternan según el desplazamiento real: contra una pared no sigue caminando. Correr y el power-up de velocidad aceleran el ciclo. En el aire flexiona rodillas y adelanta brazos; al caer cambia de pose y al aterrizar comprime ligeramente el cuerpo. Al caminar, la cadera y el pecho giran en sentidos opuestos al ritmo de las pisadas, con balanceo lateral y vertical del cuerpo. Al correr aumenta ese movimiento y aparece una inclinación frontal leve; el torso conserva una postura erguida en lugar de copiar la flexión excesiva del clip importado. `Torso Twist`, `Running Lean` y `Hip Sway` se ajustan en el prefab. Reposo y cambios de estado se mezclan suavemente. Pausa, tienda y fin de partida congelan la pose. `Stride Length`, `Leg Swing`, `Arm Swing` y `Blend Speed` se ajustan en el prefab. Es una animación provisional por pesos espaciales, pensada para esta beta; los clips artísticos pueden sustituirla más adelante.

El idle adapta los giros del archivo proporcionado `rigged character low poly@Breathing Idle.fbx` al rig procedural. `Animations/BreathingIdle.json` contiene diez pistas aditivas, muestreadas a 30 Hz, con un cierre suave del ciclo de 9,93 segundos. Se activa al estar quieto y en el suelo y se desvanece al caminar o saltar. `Idle Strength` ajusta su intensidad. Se conserva el mismo modelo del jugador, sin superponer el cuerpo del archivo Mixamo. `python Tools/ExtractIdleMotion.py` regenera los datos desde el FBX original sin modificarlo.

`Running.fbx` y `Jump.fbx` están importados en `03_Prefabs` y adaptados al mismo rig mediante `Animations/Running.json` y `Animations/Jump.json`. La carrera usa un ciclo de 0,63 s, ajustando su reproducción al desplazamiento real y a `Run Reference Speed` (4 por defecto). El salto usa un clip no cíclico de 1 s: sus fases de despegue, ápice y aterrizaje se sincronizan con la velocidad vertical del controlador. Se mezcla el final del salto con idle/carrera al tocar suelo. No se aplica el desplazamiento de raíz del FBX, por lo que las animaciones no cambian las velocidades ni duplican la altura del salto.

`Run Motion` y `Jump Motion` están asignados en `Player.prefab`; el idle se conserva al detenerse. `python -B Tools/ExtractIdleMotion.py --all` regenera los clips adaptados, usando el idle como referencia común y convirtiendo los ejes de los huesos Mixamo a los del rig de Unity. Las animaciones cíclicas cierran suavemente y Jump conserva su último fotograma sin volver a empezar en el aire.

Para reducir deformaciones, las mallas se convierten a un espacio común antes de animarlas, incluyendo normales, tangentes y sentido de los triángulos en piezas con escala negativa. El abrigo usa pesos propios: su parte inferior no sigue las rodillas. Pantalones y botas no reciben pesos de brazos; pelo y piezas de la cara permanecen rígidos. La manga y la piel comparten los pesos en el codo, con una transición concentrada alrededor de su pivote. Los antebrazos flexionan sobre un eje de bisagra limitado a 30 grados por sentido para evitar la torsión importada. Las rotaciones se limitan por articulación y `Motion Strength` vale 0,65 por defecto. La animación conserva el estilo low-poly y sigue siendo una adaptación provisional, ya que el FBX visual no incluye un rig de piel original.

La cámara usa antialiasing SMAA en calidad alta y resolución dinámica desactivada. El perfil PC mantiene su escala de renderizado de 1 (100 %). Esto suaviza los bordes sin cambiar la geometría ni el estilo de los materiales.

`Platform Prefab` usa `03_Prefabs/JumpPlatform.prefab`, una plataforma simulada a partir del piso del kit, con colisión y tamaño adaptados al tramo. El boss usa `03_Prefabs/SolicitudTrabajo.fbx`, copiado del archivo proporcionado. Este FBX referencia una imagen externa `Solicitud.jpeg` que no se recibió: mientras falte, se aplica un material y un rótulo provisionales al modelo. Para recuperar su apariencia original hay que importar esa textura y asignarla a su material.

`Pill Visual Prefab` admite un futuro modelo o prefab con sprite; mientras está vacío se muestran cajas geométricas. Los recursos visuales se ajustan y se desactivan sus colisionadores para que la lógica use los del juego. Estos campos son solo para presentación, no deben contener scripts de comportamiento.

## Algoritmos y alcance

### Enemigos, monedas y tienda

La beta añade hasta seis Spade y cuatro torretas, además del boss. `03_Prefabs/SpadeEnemy.prefab` contiene un hijo visible `Pala.prefab`, que incorpora el modelo `Spade.fbx` proporcionado. La pala se mantiene vertical y su tamaño en juego se ajusta a 1,25 m, elevado 0,15 m sobre el suelo; `Visual Height` permite cambiarlo. Se reutiliza ese hijo al iniciar, evitando duplicar modelos. Cada Spade espera dos segundos y después persigue al jugador a 2,6 unidades por segundo. Recalcula una ruta BFS al llegar al centro de cada celda, respetando paredes y evitando precipicios y torretas. Si no puede alcanzar al jugador por suelo seguro, se acerca hasta la celda accesible más próxima y espera. Su esfera sólida bloquea el paso y hace un punto de daño al contacto. La pausa y la tienda detienen la persecución. El FBX referencia `texture/spade_texture.png`, que no estaba entre los archivos disponibles; se conserva la malla con un material metálico gris provisional.

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

DFS aleatorio iterativo conecta todas las celdas. Después se abren paredes interiores adicionales para crear cruces, bucles y caminos alternativos. `Alternative Routes` vale 0,25 en la beta: se intenta añadir un número de conexiones igual al 25 % de las conexiones del árbol base, redondeado hacia arriba y limitado por las paredes disponibles. Con cero se conserva el árbol original. BFS elige la candidata con mayor distancia por pasillos después de filtrar el perímetro por distancia física al spawn. Se abre un único borde exterior. La generación y los obstáculos usan semillas reproducibles. El boss también usa BFS y cambia de pasillo desde los centros de las celdas para evitar recortar esquinas a través de paredes. No necesita hornear un NavMesh.

Esta beta genera el laberinto al comenzar cada partida. Todavía no transforma las paredes durante la persecución, no exige llaves y usa animación procedural y arte provisional. La pantalla de victoria muestra tiempo y puntuación. El balance de dificultad queda abierto para la jam.

## Verificación

`pwsh -File Tools/VerifyMaze.ps1` prueba el algoritmo sin abrir Unity: 1000 semillas, dimensiones hasta 40, conectividad, simetría de pasillos, una o varias salidas, ausencia de ciclos con conexiones extra desactivadas, bucles con caminos alternativos, cuatro salidas alcanzables, rutas BFS que evitan celdas bloqueadas, aproximación cuando el destino es inaccesible, salida lejana, reproducibilidad, variación y selección segura de obstáculos no adyacentes.

`pwsh -File Tools/VerifyRunFeatures.ps1` comprueba saldos, compras sin fondos, precios, curación, escudo, duración de velocidad, daño e invulnerabilidad, puntuación, reinicio y 1000 distribuciones reproducibles de enemigos/monedas. Ejecuta cada script en un proceso independiente de PowerShell para evitar duplicar los tipos de prueba.

`pwsh -File Tools/VerifyPlayerRig.ps1` comprueba 65409 muestras de pesos del personaje, incluida la continuidad entre manga y brazo: pesos normalizados y no negativos, índices de huesos válidos, simetría y asignación de pies, manos y torso. Las pruebas Play Mode también verifican que el rig se inicialice y flexione las rodillas durante el salto.

Los scripts de juego y de editor pueden compilarse con el compilador y las referencias de Unity. `MazeBetaVerification.Run` prepara pruebas automáticas en Play Mode de movimiento, colisiones, objetos recogibles, fatiga, persecución, aceleración, salto y aterrizaje, huecos reales, caída mortal, todas las salidas, derrota y reintentos. También comprueba compras y pausa de tienda, recogida de moneda, persecución de las palas y su pausa/reanudación en la tienda, impactos de bala y línea de visión de torretas; los récords se desactivan durante la prueba. Ejecutar desde terminal, sin `-quit`:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe' -batchmode -projectPath 'D:\Descargas\GameJam\GameJam-' -executeMethod MazeBetaVerification.Run -logFile 'D:\Descargas\GameJam\GameJam-\Logs\beta-verification.log'
```

El verificador finaliza automáticamente el editor: usarlo sin trabajo sin guardar. La ejecución inicial de Play Mode quedó pendiente porque Unity se cerró antes de cargar el proyecto; el registro indicó un fallo de validación del servicio de licencias. La compilación estática y las pruebas de topología se verifican por separado y no sustituyen una prueba visual y jugable.


`pwsh -File Tools/VerifyRoll.ps1` comprueba activación, duración completa, entrada antes de aterrizar, tolerancia a una pérdida transitoria de contacto, recarga, ausencia de repetición automática, cancelación por caída y reinicio a 30, 60 y 144 FPS.


La prueba enfocada `GameJam > Verificar voltereta` se ejecutó en la instancia abierta de Unity: C y Ctrl izquierdo activaron la acción, hubo desplazamiento y giro real del rig, la tienda congeló la acción y la voltereta finalizó. El resultado está en `Logs/roll-playmode-result.txt`. Esta prueba no cierra Unity; deja Play pausado para inspeccionar la pose. No sustituye la batería completa de la beta. Después de editar scripts con Play activo, detener Play, usar Assets > Refresh y esperar la compilación antes de iniciar otra partida. Hacer clic en la vista Game permite que reciba las teclas.


Los muros de BetaMaze tienen 0,75 m de grosor y 2,6 m de altura por defecto, configurables con `Wall Thickness` y `Wall Height`. El relieve del kit se conserva sobre un núcleo sólido, con tapa superior de ancho completo y tramos que se solapan en las esquinas. Los BoxCollider se ajustan al grosor y la altura; el ancho se limita para conservar pasillos transitables. Las sillas, la tienda y las torretas se separan de la cara interior de los muros; en celdas demasiado estrechas se omite el mobiliario que no cabe.


El rig del player incorpora una articulación lumbar `Spine` entre cadera y pecho (11 huesos en total; se conservan los índices de las diez pistas importadas). La respiración avanza durante reposo, carrera y salto, y recupera su ritmo gradualmente al parar. Pecho y brazos tienen pequeños movimientos asimétricos; la cadera cambia suavemente de apoyo. Columna y pecho responden con inercia al arrancar, frenar y girar. `Breathing Strength`, `Secondary Motion` y `Action Blend Time` se configuran en Player.prefab. Los saltos se mezclan durante 0,16 segundos por defecto, con recuperación al aterrizar y tolerancia a un fotograma sin apoyo. Pausa y tienda siguen congelando la animación. `GameJam > Verificar movimiento natural` comprueba movimiento de pecho, brazos y columna en reposo, transición de salto y continuidad después de aterrizar.

Se verificaron en Play Mode la continuidad de pecho, brazos y columna en reposo, la activación del salto, la transición sin saltos grandes de pose y el movimiento posterior al aterrizaje (`Logs/natural-motion-playmode-result.txt`). La prueba de voltereta también pasó de nuevo con el rig de 11 huesos.


Caminar y correr ahora son estados distintos. Sin Shift/L3 se usa la marcha procedural; mantener Shift/L3 mientras hay desplazamiento activa `Running` con zancada y elevación de rodillas mayores, balanceo de brazos y `Sprint Motion Strength` de 0,9. El estado se toma de la acción de sprint, por lo que sigue reproduciéndose con fatiga. Soltar Shift, detenerse o chocar con una pared mezcla la pose hacia caminar/reposo. El HUD muestra la velocidad real y `CORRIENDO` cuando está activo. `GameJam > Verificar carrera` comprueba selección del clip, velocidad, amplitud de piernas, salida del estado y carrera con fatiga.

La verificación de carrera pasó en Play Mode: caminar y correr seleccionan poses distintas, Shift activa Standard Run, sube la velocidad y la amplitud de piernas (41,2 grados observados), al soltar vuelve gradualmente al reposo y la fatiga no impide activar el clip. Resultado: `Logs/sprint-playmode-result.txt`.

## Cambiar prefabs desde el Inspector

Con Play detenido, selecciona `Beta Game - Configuracion` en la jerarquía de BetaMaze. Arrastra los prefabs del panel Project a los campos del componente `MazeBetaGame`. Guarda la escena y vuelve a iniciar Play para regenerar los objetos con tus elecciones.

| Grupo | Referencias disponibles |
| --- | --- |
| Laberinto | Wall Prefab, Floor Prefab |
| Piezas generadas del escenario | Wall Core Prefab, Wall Cap Prefab, Exit Floor Prefab, Exit Marker Prefab, Pit Visual Prefab, World Label Prefab, Camera Prefab |
| Jugador y recursos visuales opcionales | Player Prefab, Boss Prefab, Boss Visual Prefab, Pill Prefab, Pill Visual Prefab |
| Plataformas y salto | Platform Prefab, Jump Takeoff Prefab, Jump Landing Prefab |
| Enemigos y monedas | Spade Enemy Prefab, Turret Enemy Prefab, Projectile Prefab, Coin Prefab |
| Modelos de torretas, monedas y proyectiles | Turret Base Visual Prefab, Turret Barrel Visual Prefab, Coin Visual Prefab, Projectile Visual Prefab |
| Señoras sentadas y tienda | Seated Woman Prefab, Seated Woman Count (mínimo 3), Chair Prefab, Store Prefab |

Los campos visuales vacíos mantienen las figuras provisionales; los suelos de salida y bordes de salto vacíos usan Floor Prefab. Los prefabs completos de boss, pastillas, pala, torreta, moneda y proyectil conservan sus mallas; la lógica correspondiente se añade si falta. Player Prefab necesita el componente Player. World Label Prefab acepta un prefab con TextMesh y conserva su fuente, color y estilo. Camera Prefab se usa si la escena no tiene MainCamera. En TurretEnemy se puede asignar Barrel Root para que gire el cañón ya incluido en el prefab; en SpadeEnemy, Visual Root permite definir qué parte del modelo se normaliza. Los materiales de los prefabs personalizados se conservan, incluidas las plataformas. El comando de crear/abrir beta solo rellena las referencias vacías y respeta las elecciones ya asignadas.

Los contenedores del generador y los huesos internos del rig son estructuras de funcionamiento; los campos anteriores controlan los objetos visuales y de juego.

## Caminata

Walking.json es un ciclo propio de esta beta, creado con Tools/CreateWalkMotion.py: 33 fotogramas, 11 pistas y cierre continuo. No procede de un FBX externo. Incluye pasos alternados, flexión de rodillas, balanceo de brazos y rotación de cadera/columna/pecho. Se adapta a la dirección real para avanzar, retroceder y desplazarse lateralmente. Walk Motion, Walk Motion Strength y Walk Cycle Distance se ajustan en Player.prefab. Caminata y carrera comparten la fase de las pisadas para suavizar su transición; Shift/L3 selecciona Running.

Verificado en Play Mode: Walking.json se carga y mueve las piernas; la transición a Standard Run y la carrera con fatiga pasan. La sustitución de prefabs del boss, pastillas, palas, torretas, monedas, proyectiles y piezas del escenario conserva los modelos sin duplicar primitivas. Resultado en `Logs/sprint-playmode-result.txt`.


### Nueva carrera Running.fbx

`Player.prefab > Player Limb Animator > Run Motion` referencia `Animations/Running.json`, adaptado del nuevo `03_Prefabs/Running.fbx`. El archivo original se copia íntegro; Standard Run se conserva como alternativa. El ciclo contiene 19 muestras a 30 Hz (0,633 s), cierra en bucle y alinea las cuatro articulaciones de las piernas con la fase inicial de Walking para reducir el cambio de apoyo durante la mezcla. `python -B Tools/ExtractIdleMotion.py --all` lo regenera junto con las otras animaciones.

Se conserva el control de velocidad, salto y colisiones del player; el clip no aplica desplazamiento de raíz. El torso mantiene su postura erguida, respiración y balanceo; los codos conservan la bisagra limitada para evitar torsiones. La frecuencia de las pisadas sigue la velocidad real y Shift/L3 activa la carrera incluso con fatiga. `Run Reference Speed` y `Sprint Motion Strength` permiten ajustar su reproducción e intensidad desde el prefab.

`python -B Tools/VerifyRunningMotion.py` comprueba la copia íntegra del FBX, asignación en Player.prefab, las diez pistas, cuaterniones normalizados, continuidad entre muestras, extremidades animadas y reproducción determinista de la adaptación. Las verificaciones históricas de Standard Run anteriores a esta sección corresponden al clip previo.


### Postura erguida y marcha

El torso comparte una alineación erguida en reposo, caminata y carrera. El idle importado ya no determina la flexión de la espalda: se conserva su giro suave, y la respiración sigue animando columna, pecho y brazos. `Max Grounded Torso Tilt` limita a 4 grados la inclinación conjunta de cadera, columna y pecho, repartida entre sus articulaciones. `Running Lean` vale 1 grado y `Torso Twist` 6; la inclinación de aceleración es breve y suave. Estos valores se ajustan en `Player.prefab > Player Limb Animator`. Salto y voltereta conservan su flexión de acción.

La marcha tiene una fase de apoyo más larga, rodilla casi extendida durante el apoyo y flexión progresiva durante la recuperación del pie, con el brazo contrario acompañando cada paso. Se reduce el balanceo lateral de la cadera a 0,003. `Tools/CreateWalkMotion.py` regenera esta marcha; después se debe regenerar Running con la referencia Walking para mantener la alineación de ambas fases.

Las pruebas `GameJam > Verificar movimiento natural` y `GameJam > Verificar carrera` comprueban postura erguida real del pecho en idle, caminata y sprint; movimiento de extremidades; respiración; transición del salto; y selección de Running con Shift, incluida la fatiga.

Verificado en Play Mode: postura erguida en idle, caminata y Running; respiración y movimiento de brazos/columna; transición del salto sin salto brusco de pose; carrera con fatiga. Resultados: `Logs/natural-motion-playmode-result.txt` y `Logs/sprint-playmode-result.txt`.


### Corrección de la pose original del modelo

El FBX rigAnimated contiene la postura encorvada en sus propios vértices. `PlayerRestPosture` corrige esa silueta antes de generar el skin: alinea cabeza/torso con la pelvis, endereza el perfil de las piernas y relaja los brazos. Se modifica únicamente la copia de malla de cada jugador; el FBX original se conserva. Normales y tangentes reciben la misma transformación para mantener la iluminación. La cabeza se desplaza como una pieza rígida y se vuelven a colocar los pivotes del rig sobre la postura corregida.

`Rest Posture Strength` vale 1 en Player.prefab; 0 conserva la pose encorvada original. Reinicia Play después de cambiarlo. `GameJam > Verificar silueta del player` muestra el personaje de perfil en una plataforma de prueba y comprueba los vértices de la malla visible, además de los huesos. Las pruebas de movimiento y carrera también comprueban la alineación de cabeza/pelvis en la malla renderizada.

Verificado con la corrección de malla activa: idle y marcha con medición de vértices visibles, Running con Shift y fatiga, salto y voltereta. La vista lateral permitió detectar que la pose original del FBX estaba encorvada aunque los huesos estuvieran verticales.


### Voltereta adaptada a la pose erguida

La voltereta tiene límites y fuerza de articulación propios: recoge los muslos, flexiona las rodillas y redondea columna/pecho. Se conserva el giro completo de `Sprinting Forward Roll.fbx`, con preparación antes del giro y recuperación al terminar. El apoyo se obtiene de la malla skineada en su pose actual, incluyendo la flexión de las piernas, en vez de usar la caja del cuerpo estirado. Los datos de vértices y pesos se guardan al construir el rig y se reutilizan durante la acción.

Se conserva la duración de 1,17 s y los controles C/Ctrl. La vuelta se completa entre el 12 % y el 88 % del tiempo para dejar espacio al recogimiento y a la salida. Las flexiones del codo conservan su bisagra limitada; las poses de caminar, correr e idle mantienen la postura erguida.

Verificado en Play Mode: activación con C y Ctrl, muslos y rodillas recogidos, vuelta continua completa, congelación de pose y apoyo al abrir tienda, y recuperación de la postura visible erguida. Resultado: `Logs/roll-playmode-result.txt`.


### Running y Jump sobre la pose de pie

Los clips activos se recalculan desde las direcciones de los segmentos del esqueleto de sus FBX, descontando la flexión de rodilla y muslo de su pose de referencia. No se resta el primer fotograma del idle encorvado. Las rodillas usan una bisagra que flexiona en un solo sentido; el codo conserva su flexión limitada para evitar torsiones. Ambos clips incluyen ahora las 11 articulaciones, incluida la columna.

`Running.json` conserva el ciclo de Running.fbx (0,633 s), alineado con la caminata. `Run Reference Speed` vale 5,4 para reproducir ese ritmo con la velocidad de sprint predeterminada. `Sprint Motion Strength` vale 1. El clip aporta el giro del torso dentro del límite de postura erguida y tiene amplitudes propias de muslo, rodilla y brazos.

`Jump.json` conserva el salto de Jump.fbx y sincroniza sus poses con la velocidad vertical real. `Jump Motion Strength` vale 0,95; los límites de salto permiten flexionar las piernas y elevar brazos sin quedar restringidos a los de caminata. El torso acompaña el salto y el aterrizaje; al tocar suelo recupera la postura erguida. El estado de sprint se conserva durante el vuelo para recuperar Running al aterrizar si Shift sigue pulsado.

`python -B Tools/VerifyRunningMotion.py` comprueba ambos clips, sus fases, continuidad y bisagras, además de su regeneración determinista. `GameJam > Ver carrera adaptada` y `Ver salto adaptado` permiten inspeccionar una pose de perfil en Play Mode; al salir de Play se recupera la escena de juego.

Verificado en el editor con los clips adaptados: salto desde reposo, carrera con Shift y fatiga, salto corriendo, retorno a Running al aterrizar y recuperación de la postura erguida. La prueba de carrera mide el movimiento durante un ciclo sobre un suelo despejado. También pasó la prueba de voltereta con C/Ctrl y pausa en tienda. Se inspeccionaron Running de perfil y Jump en el ápice; los resultados de las pruebas quedan en `Logs/natural-motion-playmode-result.txt`, `Logs/sprint-playmode-result.txt` y `Logs/roll-playmode-result.txt`.


## Pisos planos y se�oras sentadas

`OfficeSmoothFloor`, `ArchiveSmoothFloor` y `CourtyardSmoothFloor` son prefabs planos, sin los bloques del piso anterior. Su shader `GameJam/Smooth Tiles` dibuja baldosas con juntas discretas, variaciones suaves de color y brillo tenue; no desplaza v�rtices ni usa relieve. Las superficies se alinean con la parte superior del collider, incluidas las plataformas. Los materiales y prefabs siguen siendo editables desde el Inspector.

La se�ora dej� de representar la tienda. Un terminal independiente aparece junto al inicio; B/Select y el bot�n de pantalla siguen abriendo la tienda y pausando la partida. `Store Prefab` permite reemplazar el terminal provisional.

Cada nivel genera al menos tres se�oras en suelo seguro, sobre `InterChair.prefab`. `SeatedWoman.prefab` usa una copia del OBJ proporcionado con postura sentada: rodillas hacia delante, piernas apoyadas y manos en el regazo. El original se conserva. La silla se orienta hacia el mismo frente que la figura y se ajusta a 0,82m de alto, dejando el asiento aproximadamente a la altura de la pelvis. `Seated Woman Count` tiene m�nimo 3; `Seated Woman Prefab`, `Chair Prefab` y sus variantes por �rea permiten cambiar los modelos.

Al aproximarse aparece el aviso de pasar rodando con C/Ctrl. Caminar, correr y saltar no atraviesan la zona; activar una voltereta permite cruzarla. La comprobaci�n del segmento completo de movimiento impide saltarse el l�mite con un fotograma largo o una mejora de velocidad. Fuera de la zona se recupera el movimiento habitual. Las se�oras se reservan antes de colocar precipicios y quedan separadas de monedas, pastillas y enemigos.

Verificaciones ejecutadas: compilaci�n de scripts de juego/editor; 1.000 distribuciones de se�oras, l�mites de proximidad en ambos sentidos, regresiones de generaci�n/parkour y temporizaci�n de la voltereta. Se revis� la malla sentada mediante una proyecci�n del modelo fuera del editor. `GameJam > Verificar senoras y piso` prepara la comprobaci�n real de los nueve niveles, el shader y el paso caminando/corriendo/saltando frente a la voltereta. La prueba pas� en la instancia abierta de Unity: nueve niveles con al menos tres se�oras y pisos planos, shader sin errores, bloqueo de marcha/carrera/salto, cruce mediante voltereta y pausa/reanudaci�n de la tienda. Resultado: `Logs/residents-playmode-result.txt`. La revisi�n de perfil permiti� corregir la orientaci�n relativa y colocar la cadera sobre la silla; `Seated Woman Yaw`, `Seated Chair Yaw` y `Seated Woman Offset` permiten ajustarlo al sustituir modelos.

### Canvas, objetos recogibles y derrotas

La escena BetaMaze conserva el Canvas UI y su panel Stats. MazeCanvasHUD usa heart_icon.png para los tres corazones (se ocultan al recibir daño y reaparecen al curarse) e icon_coins.png con el saldo a su lado. El tiempo, score, nivel, efectos, avisos, pausa, victoria y tienda usan ese mismo Canvas. Se eliminó toda la interfaz OnGUI anterior. Las referencias y objetos UI se pueden editar desde el Inspector.

Una derrota muestra su causa y una cuenta atrás automática de 4 segundos, sin botones. La primera derrota permite repetir exactamente la misma semilla; la segunda genera una semilla nueva en el mismo nivel y reinicia el contador. Ganar y pasar de nivel también reinicia el contador. Cada intento restablece vida, monedas y mejoras. El panel informa permanentemente de la regla y confirma cuándo se genera otro laberinto. Defeat Restart Delay permite ajustar la espera.

Los cuatro ItemData suministrados se asignaron al generador. Sus prefabs conservan las mallas originales y ahora apuntan a materiales URP Lit con las texturas TGA del paquete. También se corrigió el material ausente del efecto de escudo con transparencia. Los ítems evitan precipicios, parkour y zonas reservadas a señoras. GameJam > Configurar Canvas e items permite rehacer la configuración usando UI/Stats existente, sin crear otro Canvas. GameJam > Verificar Canvas items y derrotas prueba la integración en Play; resultado en Logs/canvas-playmode-result.txt.

Verificación final: PASS en Unity. Comprobados los corazones con daño/curación, recogida física de monedas, compras y efectos, materiales de los cuatro ítems, reintentos primero/segundo/tercero, ausencia de botones al perder y avance al siguiente nivel. HUD y aviso de derrota revisados visualmente en Full HD. La espera de producción permanece en 4 segundos.

### Escena de inicio

Inicio.unity adapta la escena y la ilustración de Inicio.unitypackage. Se importaron esos recursos y los scripts del menú, sin reemplazar las copias del juego incluidas en el paquete. El panel se situó a la izquierda, con colores del HUD y del personaje, textos en español, transición breve, fondo que conserva su proporción y ajustes para diferentes resoluciones. Incluye nueva partida, continuar, controles, reinicio con confirmación y salir en la versión compilada. El menú se puede usar con ratón, teclado y mando mediante el EventSystem del Canvas.

Inicio es la primera escena de Build Profiles; Nueva partida abre BetaMaze. Continuar recupera el comienzo del nivel y su semilla, incluida la semilla nueva tras dos derrotas. Ganar guarda el siguiente nivel y completar los nueve termina la campaña guardada. Monedas, vida y mejoras se reinician al cargar el comienzo del nivel. La pausa y la victoria del Canvas tienen Volver al inicio; la derrota mantiene su reinicio automático sin botones. Las pruebas abiertas directamente en BetaMaze desde el editor no reemplazan la partida guardada desde el menú.

La integración resolvió marcadores de fusión que impedían compilar los scripts, materiales y escena, conservando la implementación reciente de Canvas, parkour, señoras sentadas y doble salto. Se conservaron copias de esos archivos antes de resolverlos en Logs/BeforeStartupMerge. No se completó ni confirmó la fusión de Git.

GameJam > Configurar menu en Inicio rehace el diseño. GameJam > Verificar menu de inicio prueba nueva partida, checkpoints y navegación, conservando/restaurando las preferencias del jugador. Resultado: Logs/startup-playmode-result.txt.

Resultado final: PASS en la instancia abierta de Unity para nueva partida, continuar por nivel/semilla, confirmaciones, avance y fin de campaña, guardado de laberintos regenerados y regreso desde el Canvas. Se restauraron las preferencias originales tras la prueba. Menú y guía de controles revisados visualmente en Full HD; el botón Cómo jugar se comprobó también con el ratón.
