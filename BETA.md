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

La rotación completa del cuerpo se separa de las rotaciones limitadas de las articulaciones para conservar la vuelta de 360 grados sin retorcer el codo. La cámara y el controlador permanecen verticales. El apoyo visual se calcula con los límites del modelo para evitar hundir el cuerpo en el suelo. `python Tools/ExtractIdleMotion.py --all` regenera también esta animación. El verificador de Play Mode incluye activación, movimiento contra una pared, finalización y pausa de la voltereta; su ejecución sigue pendiente en el editor.

El jugador comienza en una esquina de un laberinto de 12 por 12 celdas. Cada nivel tiene una sola salida exterior, marcada SALIDA, y conserva cruces, bucles y varios caminos internos. La puerta se escoge entre celdas del perímetro cuya distancia Manhattan al inicio alcanza al menos el 75 % de la diagonal en celdas; entre esas candidatas, BFS elige la de mayor distancia por pasillos. En el mapa predeterminado esto exige al menos 17 pasos de separación física en la cuadrícula, además del recorrido por el laberinto. El boss «SE BUSCA EMPLEO» persigue por los pasillos; si alcanza al jugador, causa derrota.

Hasta diez tramos rectos contienen precipicios con bordes de despegue/aterrizaje y plataformas elevadas. Los huecos no tienen suelo invisible debajo. Espacio permite saltar a la plataforma o cruzar el tramo; caer por debajo de -3 termina la partida. Los tramos se separan entre sí y evitan el inicio, las salidas y sus celdas adyacentes. El boss flotante cruza estos huecos siguiendo su ruta. El número efectivo puede ser menor en laberintos pequeños o con pocos pasillos rectos.

La velocidad inicial del jugador es 4 unidades por segundo y correr aplica un multiplicador de 1,35. Cada 60 segundos pierde 0,35 de velocidad, hasta un mínimo de 2,6. Recoger una caja de antidepresivos restaura la velocidad inicial y reinicia el contador de fatiga. Este efecto es una decisión provisional para la beta.

El boss empieza a 2,2 unidades por segundo, espera 5 segundos antes de perseguir y aumenta 0,1 cada 30 segundos desde el comienzo de la partida. La pausa detiene los tiempos de juego. Se distribuyen hasta 12 cajas en celdas diferentes con suelo seguro, evitando inicio, boss, salidas y obstáculos.

## Campaña: tres áreas de tres niveles

Hay nueve laberintos consecutivos, agrupados en Oficinas (1–3), Archivo (4–6) y Patio (7–9). Cada nivel usa una semilla derivada de la semilla de campaña, de modo que sus mapas son distintos y reproducibles. La pantalla indica área y nivel. Escapar habilita «Siguiente nivel» o «Continuar a la siguiente área»; al terminar el noveno ofrece iniciar una campaña nueva. Reintentar conserva el nivel actual y su ambientación. Vida, monedas, mejoras, tiempo y puntuación se reinician al entrar en cada nivel.

En `Beta Game - Configuracion > Areas`, cada elemento permite cambiar nombre, prefabs de paredes, suelos, núcleo y cubierta de pared, plataforma de salida, marcador, precipicio, plataforma de salto, bordes de salto, silla, tienda y rótulos. Los campos vacíos heredan los prefabs globales. Oficinas hereda Wall_3M/Floor_3M, Archivo usa Wall_2M/Floor_2M y Patio usa Wall_Lamp/Floor_1M. Cada área también tiene colores propios para las piezas geométricas provisionales; los materiales de los prefabs se conservan.

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

`Standard Run.fbx` y `Jump.fbx` están importados en `03_Prefabs` y adaptados al mismo rig mediante `Animations/StandardRun.json` y `Animations/Jump.json`. La carrera usa un ciclo de 0,73 s, ajustando su reproducción al desplazamiento real y a `Run Reference Speed` (4 por defecto). El salto usa un clip no cíclico de 1 s: sus fases de despegue, ápice y aterrizaje se sincronizan con la velocidad vertical del controlador. Se mezcla el final del salto con idle/carrera al tocar suelo. No se aplica el desplazamiento de raíz del FBX, por lo que las animaciones no cambian las velocidades ni duplican la altura del salto.

`Run Motion` y `Jump Motion` están asignados en `Player.prefab`; el idle se conserva al detenerse. `python Tools/ExtractIdleMotion.py --all` regenera las tres animaciones, usando el idle como referencia común y convirtiendo los ejes de los huesos Mixamo a los del rig de Unity. Las animaciones cíclicas cierran suavemente y Jump conserva su último fotograma sin volver a empezar en el aire.

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


Caminar y correr ahora son estados distintos. Sin Shift/L3 se usa la marcha procedural; mantener Shift/L3 mientras hay desplazamiento activa `Standard Run` con zancada y elevación de rodillas mayores, balanceo de brazos y `Sprint Motion Strength` de 0,9. El estado se toma de la acción de sprint, por lo que sigue reproduciéndose con fatiga. Soltar Shift, detenerse o chocar con una pared mezcla la pose hacia caminar/reposo. El HUD muestra la velocidad real y `CORRIENDO` cuando está activo. `GameJam > Verificar carrera` comprueba selección del clip, velocidad, amplitud de piernas, salida del estado y carrera con fatiga.

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
| Ambientación y tienda | Shopkeeper Prefab, Chair Prefab |

Los campos visuales vacíos mantienen las figuras provisionales; los suelos de salida y bordes de salto vacíos usan Floor Prefab. Los prefabs completos de boss, pastillas, pala, torreta, moneda y proyectil conservan sus mallas; la lógica correspondiente se añade si falta. Player Prefab necesita el componente Player. World Label Prefab acepta un prefab con TextMesh y conserva su fuente, color y estilo. Camera Prefab se usa si la escena no tiene MainCamera. En TurretEnemy se puede asignar Barrel Root para que gire el cañón ya incluido en el prefab; en SpadeEnemy, Visual Root permite definir qué parte del modelo se normaliza. Los materiales de los prefabs personalizados se conservan, incluidas las plataformas. El comando de crear/abrir beta solo rellena las referencias vacías y respeta las elecciones ya asignadas.

Los contenedores del generador y los huesos internos del rig son estructuras de funcionamiento; los campos anteriores controlan los objetos visuales y de juego.

## Caminata

Walking.json es un ciclo propio de esta beta, creado con Tools/CreateWalkMotion.py: 33 fotogramas, 11 pistas y cierre continuo. No procede de un FBX externo. Incluye pasos alternados, flexión de rodillas, balanceo de brazos y rotación de cadera/columna/pecho. Se adapta a la dirección real para avanzar, retroceder y desplazarse lateralmente. Walk Motion, Walk Motion Strength y Walk Cycle Distance se ajustan en Player.prefab. Caminata y carrera comparten la fase de las pisadas para suavizar su transición; Shift/L3 sigue seleccionando Standard Run.

Verificado en Play Mode: Walking.json se carga y mueve las piernas; la transición a Standard Run y la carrera con fatiga pasan. La sustitución de prefabs del boss, pastillas, palas, torretas, monedas, proyectiles y piezas del escenario conserva los modelos sin duplicar primitivas. Resultado en `Logs/sprint-playmode-result.txt`.
