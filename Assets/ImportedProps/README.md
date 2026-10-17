# Recursos proporcionados para la beta

- `Anderson/Source/Anderson.glb`: copia sin modificar de `mrs-anderson-bonnies-mother/source/model (42).glb`.
- `Anderson/Anderson.obj`: conversión estática con transformaciones de nodos aplicadas, UV, normales y cuatro materiales. Sus tres texturas se extraen del GLB. Altura: 1,65 m; frente: +Z.
- `Chair/InterChair.fbx` y `Chair/Textures`: copias del recurso `silla-inter`. Materiales URP separados para tela, metal y tapones. La tela usa color y mapa normal. El mapa de rugosidad original queda conservado; el material usa una suavidad constante de 0,2.
- `Materials`: materiales URP Lit, asignados mediante remapeos de los importadores de modelos.

Los prefabs reutilizables están en `Assets/03_Prefabs/AndersonShopkeeper.prefab` y `Assets/03_Prefabs/InterChair.prefab`. Al terminar la primera importación, `MazeBetaSetup` resuelve sus referencias a los modelos importados y normaliza el tamaño para su vista previa. Después conserva los cambios manuales de los prefabs.

En BetaMaze el personaje representa visualmente la tienda cerca del inicio, con el cartel `TIENDA [B]`; la tecla B mantiene el acceso a la tienda desde cualquier lugar. Se distribuyen hasta diez sillas junto a paredes cerradas, sin ocupar los centros de los pasillos, salidas, precipicios ni posiciones iniciales de enemigos o monedas. Los objetos se reconstruyen con cada partida y usan la semilla del laberinto. La selección puede producir menos sillas si faltan celdas disponibles.

El personaje proporcionado carece de esqueleto y animaciones: se integra como NPC estático, conservando al jugador actual. `Tools/ImportSuppliedProps.py` permite repetir la conversión y la copia desde las carpetas originales; regenera los prefabs y materiales con GUID deterministas.
