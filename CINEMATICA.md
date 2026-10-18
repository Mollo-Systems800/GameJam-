# Cinemáticas de The Maze of work

Nueva partida reproduce una secuencia de nueve segundos: al protagonista no le gusta trabajar, le aterra rellenar una solicitud y el documento cobra vida para perseguirlo. La cámara presenta la oficina, su reacción de miedo y el papel que se aproxima. Debe escapar de las tres áreas sin firmar.

Al completar Oficinas o Archivo (niveles 3 y 6), se reproduce una escena de siete segundos con alivio, vista del área superada y presentación del siguiente departamento. Al superar Patio (nivel 9), se reproduce el final de once segundos: el protagonista respira, la solicitud cae y queda libre. Después aparece el resultado y el registro de nombre del ranking para quienes completaron los nueve niveles.

Cada derrota reproduce una escena de seis segundos. Si el golpe mortal viene del boss, se muestra el miedo ante la solicitud y la contratación forzosa. Si se pierde toda la vida por palas, formularios, púas o caídas, se muestra agotamiento y desconsuelo, junto a la causa de la derrota. Las escenas funcionan en cualquiera de los nueve niveles. Al terminar empieza la cuenta del reintento automático: la primera derrota conserva el laberinto y la segunda lo regenera.

Se puede saltar con Espacio, Esc, Enter, A/Start del mando o el botón de la pantalla. Durante la secuencia se bloquean movimiento, enemigos, tienda y daño; el tiempo y el score no avanzan. El HUD original se oculta temporalmente y reaparece al terminar. La secuencia se detiene si el juego pierde el foco.

Continuar, avanzar de nivel y reintentar una derrota no reproducen la introducción. Iniciar otra campaña tras completarla sí la reproduce. En el componente Beta Game se puede desactivar **Play New Game Intro** o **Play Outcome Cinematics** y ajustar las duraciones de introducción, área, victoria y derrota entre cinco y veinte segundos.

La presentación utiliza el Canvas existente y los modelos reales del proyecto. La utilería de la solicitud no tiene colisiones ni IA y se elimina al terminar o cancelar. Las poses suaves usan el rig del player con respiración y codos de bisagra. Se conservan las animaciones de correr, saltar y rodar al volver al juego. Código: `NewGameCinematic.cs`, `NewGameCinematic.Story.cs`, `MazeBetaGame.Cinematic.cs`; la procedencia nueva/continuada se consume una sola vez en `MazeCampaignSave`.

Las torretas son impresoras de RR. HH. sobre archivadores con ruedas, escáner, bandeja, pila de solicitudes, luz roja y retroceso del cabezal al disparar. Lanzan formularios con líneas y sello rojo; las paredes siguen bloqueando la detección y los proyectiles. Prefabs reemplazables: `Assets/03_Prefabs/TurretEnemy.prefab` y `EnemyProjectile.prefab`. La carcasa, el cabezal y la boca de salida también tienen referencias en el inspector. Reconstrucción: Tools → Maze Beta → Build office printer turrets.

Verificación nativa: `Logs/story-campaign-verification-result.txt`; solicitud de prueba `Logs/verify-story-campaign.request`. Capturas `Logs/story-*.png` y `Logs/office-printer-turret.png`. La prueba no guarda datos en el ranking ni en el checkpoint del jugador.
