# Ranking y recursos visuales

La sección **Los que salieron** de Inicio muestra las tres primeras posiciones y abre el **Ranking** completo. Guarda a todos los que completan los nueve niveles en orden, incluidos los tiempos más lentos y empatados, ordenados por menor tiempo y con páginas de diez participantes. Al terminar aparece el formulario de nombre; acepta hasta veinte caracteres y evita nombres vacíos y el doble guardado de una misma finalización. Los récords anteriores se conservan. El ranking es local al equipo.

El tiempo suma los nueve niveles y todos sus intentos. La cinemática inicial, pausa, tienda y espera automática tras una derrota no lo incrementan. Continuar conserva el tiempo acumulado; volver al inicio o cerrar el juego guarda el checkpoint. Los guardados anteriores sin datos de tiempo permiten continuar, pero requieren una nueva campaña desde el primer nivel para entrar al ranking. La confirmación de Borrar progreso también incluye el ranking.

Los campos Boss Visual Prefab y Pill Visual Prefab de Beta Game usan `EmploymentApplicationBoss.prefab` y `MedicineBox.prefab`, en `Assets/03_Prefabs`. La solicitud tiene altura de 1,8 m y orienta su impresión hacia la cámara. La caja usa la fotografía original distribuida por sus caras mediante UV de perspectiva, con volumen y sombras. Las fotografías completas están en `Assets/02_Sprites`. La pala conserva su modelo y usa los materiales SpadeWood y SpadeSteel, editables en `Assets/04_Materials`.

Se retiraron los rótulos flotantes de salida, salto, parkour, pastillas y empleo. Se conservan el marcador de salida y los avisos de pasar rodando.

`AndersonSeatedWithChair.prefab` reúne los recursos suministrados de Anderson y la silla Inter, con sus materiales, en una pose sentada. Está asignado a las tres áreas mediante **Seated Resident Prefab**. Puede sustituirse como conjunto desde el Inspector; al dejar ese campo vacío se utilizan los campos individuales de señora y silla. Se mantienen al menos tres residentes por nivel y el paso rodando.

La conversión de Anderson corrige el orden invertido de los triángulos y conserva las superficies de doble cara declaradas por el GLB. Esto evita mostrar el interior de la cara, ropa y manos. La cabeza y cada mano mantienen su forma al sentarse; los hombros y codos se ajustaron para situar los antebrazos próximos al regazo. `Tools/VerifySeatedModel.py` comprueba la orientación de la malla, UV y rigidez de cabeza y dedos.

`StartupRankingView` construye el ranking al arrancar si Unity mantiene cargada una versión antigua de Inicio sin sus botones. El generador de la escena y el juego usan el mismo constructor. `ResidentMenuRepair` permite aplicar y revisar los recursos en el editor abierto mediante una solicitud local, conservando escenas con cambios sin guardar.

El modelo del player incluye una pareja duplicada de cejas desplazada sobre el pelo (`Cube.008`). Se excluye del render del personaje animado y del retrato de Inicio después de calcular los límites de calibración del rig. Las cejas originales (`Cube.004`) se conservan unidas a la cabeza, sin cambiar las animaciones.

Validación: `Tools/VerifyCampaignRanking.ps1`, mil laberintos con al menos tres residentes, compilación y Play en Unity completando nueve niveles, más pruebas de persistencia de diecinueve participantes, orden, empates, nombres y formulario para un tiempo más lento que el récord. Se comprueba la proximidad de las cejas a la cabeza en 305 muestras de idle, caminata, carrera, salto y voltereta. Las verificaciones del ranking usan claves temporales. Se revisaron renders nativos del menú, rostro y abuela sentada con silla. Resultados: `Logs/finalists-residents-play-result.txt` y `Logs/player-eyebrow-animation-result.txt`.
