# Beta del laberinto

Abre `Assets/00_Scenes/BetaMaze.unity` en Unity 6000.5.6f1 y pulsa Play. La escena beta es la primera de Build Settings. El escenario se construye al iniciar Play; en modo edición solo se ve su configuración. SampleScene se conserva.

## Controles

- WASD o flechas: desplazamiento relativo a la cámara.
- Shift izquierdo: correr.
- Espacio: saltar. Incluye una breve tolerancia al salir de un borde y al pulsar justo antes de aterrizar; no hay doble salto.
- Mando: stick izquierdo para moverse, L3 para correr.
- Botón inferior del mando (A en Xbox / cruz en PlayStation): saltar.
- Escape: pausar o continuar. Los botones de pausa y fin permiten repetir la misma semilla o generar otra.

## Reglas de esta versión

El jugador comienza en una esquina de un laberinto de 12 por 12 celdas. Hay tres salidas exteriores por defecto, todas marcadas SALIDA; cruzar cualquiera termina la partida. `Exit Count` permite configurar entre una y ocho. Se distribuyen en celdas distintas del perímetro, evitando el inicio. El cartel «SE BUSCA EMPLEO» persigue por los pasillos; si alcanza al jugador, causa derrota.

Hasta diez tramos rectos contienen precipicios con bordes de despegue/aterrizaje y plataformas elevadas. Los huecos no tienen suelo invisible debajo. Espacio permite saltar a la plataforma o cruzar el tramo; caer por debajo de -3 termina la partida. Los tramos se separan entre sí y evitan el inicio, las salidas y sus celdas adyacentes. El boss flotante cruza estos huecos siguiendo su ruta. El número efectivo puede ser menor en laberintos pequeños o con pocos pasillos rectos.

La velocidad inicial del jugador es 4 unidades por segundo y correr aplica un multiplicador de 1,35. Cada 60 segundos pierde 0,35 de velocidad, hasta un mínimo de 2,6. Recoger una caja de antidepresivos restaura la velocidad inicial y reinicia el contador de fatiga. Este efecto es una decisión provisional para la beta.

El boss empieza a 2,2 unidades por segundo, espera 5 segundos antes de perseguir y aumenta 0,1 cada 30 segundos desde el comienzo de la partida. La pausa detiene los tiempos de juego. Se distribuyen hasta 12 cajas en celdas diferentes con suelo seguro, evitando inicio, boss, salidas y obstáculos.

## Configuración

Selecciona `Beta Game - Configuracion` en la escena para cambiar dimensiones, cantidad de salidas, semilla, tamaño de celda, velocidades, intervalos, cantidad de cajas, obstáculos, altura del salto, altura de caída mortal y cámara. Desactiva `Random Seed` para reproducir la semilla del Inspector. La semilla efectiva se muestra en pantalla. Los saltos están dimensionados para las celdas y velocidades predeterminadas; al cambiar escalas extremas hay que ajustar también el salto.

`Wall Prefab` y `Floor Prefab` usan las piezas de 3 metros del Maze Modular Kit. Se normalizan sus pivotes y dimensiones; colisionadores propios dan grosor a los pisos e impiden atravesar las paredes. `Player Prefab` referencia el modelo rigAnimated. El jugador gira hacia donde se mueve; las animaciones de caminar/correr quedan pendientes de recibir clips adecuados.

El jugador se instancia desde `03_Prefabs/Player.prefab`: incluye `CharacterController`, el script `Player` y un hijo `Character Visual` con el modelo `rigAnimated.fbx` mostrado en la referencia. También se puede arrastrar este prefab a una escena con cámara y suelo para probar movimiento y salto. En Play se ajusta la altura visual a 1,8 y se desactiva root motion para que el controlador gestione el desplazamiento. `rigged character low poly.fbx` y su animación de reposo se conservan como recursos alternativos; no se superponen dos cuerpos al jugador.

`Platform Prefab` usa `03_Prefabs/JumpPlatform.prefab`, una plataforma simulada a partir del piso del kit, con colisión y tamaño adaptados al tramo. El boss usa `03_Prefabs/SolicitudTrabajo.fbx`, copiado del archivo proporcionado. Este FBX referencia una imagen externa `Solicitud.jpeg` que no se recibió: mientras falte, se aplica un material y un rótulo provisionales al modelo. Para recuperar su apariencia original hay que importar esa textura y asignarla a su material.

`Pill Visual Prefab` admite un futuro modelo o prefab con sprite; mientras está vacío se muestran cajas geométricas. Los recursos visuales se ajustan y se desactivan sus colisionadores para que la lógica use los del juego. Estos campos son solo para presentación, no deben contener scripts de comportamiento.

## Algoritmos y alcance

DFS aleatorio iterativo genera un árbol conectado: todas las celdas son accesibles y existe una ruta única entre cada par. BFS elige primero la celda más distante del inicio situada en el perímetro y después distribuye las otras salidas según su distancia al inicio y a las salidas ya elegidas. Cada salida abre un borde exterior. La generación y los obstáculos usan semillas reproducibles. El boss también usa BFS y cambia de pasillo desde los centros de las celdas para evitar recortar esquinas a través de paredes. No necesita hornear un NavMesh.

Esta beta genera el laberinto al comenzar cada partida. Todavía no transforma las paredes durante la persecución, no exige llaves y no incluye animaciones de locomoción ni arte final. La pantalla de victoria incluye una referencia provisional a la solicitud de empleo. El balance de dificultad queda abierto para la jam.

## Verificación

`pwsh -File Tools/VerifyMaze.ps1` prueba el algoritmo sin abrir Unity: 1000 semillas, dimensiones hasta 40, conectividad, simetría de pasillos, una o varias salidas, ausencia de ciclos, salida lejana, rutas BFS, reproducibilidad, variación y selección segura de obstáculos no adyacentes.

Los scripts de juego y de editor pueden compilarse con el compilador y las referencias de Unity. `MazeBetaVerification.Run` prepara pruebas automáticas en Play Mode de movimiento, colisiones, objetos recogibles, fatiga, persecución, aceleración, salto y aterrizaje, huecos reales, caída mortal, todas las salidas, derrota y reintentos. Ejecutar desde terminal, sin `-quit`:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe' -batchmode -projectPath 'D:\Descargas\GameJam\GameJam-' -executeMethod MazeBetaVerification.Run -logFile 'D:\Descargas\GameJam\GameJam-\Logs\beta-verification.log'
```

El verificador finaliza automáticamente el editor: usarlo sin trabajo sin guardar. La ejecución inicial de Play Mode quedó pendiente porque Unity se cerró antes de cargar el proyecto; el registro indicó un fallo de validación del servicio de licencias. La compilación estática y las pruebas de topología se verifican por separado y no sustituyen una prueba visual y jugable.
