from pathlib import Path
from ExtractIdleMotion import bake

root = Path(__file__).resolve().parent.parent
for name in ('Victory', 'Writing'):
    bake(root / f'Assets/03_Prefabs/{name}.fbx',
         root / f'Assets/Resources/{name}Motion.json', 'outcome',
         root / 'Assets/03_Prefabs/rigged character low poly@Breathing Idle.fbx',
         standing_pose=True)
