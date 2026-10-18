"""Check imported topology and rigid seated head/fingers, using real OBJ data."""
from pathlib import Path
import math

root = Path(__file__).resolve().parents[1]
def read(name):
    lines = (root / 'Assets/ImportedProps/Anderson' / name).read_text().splitlines()
    values = lambda prefix: [tuple(map(float,l.split()[1:])) for l in lines if l.startswith(prefix)]
    return values('v '), values('vn '), values('vt '), [[int(t.split('/')[0])-1 for t in l.split()[1:]] for l in lines if l.startswith('f ')]

source, normals, uv, faces = read('Anderson.obj')
seated, _, seated_uv, seated_faces = read('AndersonSeated.obj')
assert len(source) == len(seated) and uv == seated_uv and faces == seated_faces
assert all(math.isfinite(c) for p in seated for c in p)
outward = inward = 0
for a,b,c in faces:
    u = [source[b][k]-source[a][k] for k in range(3)]
    v = [source[c][k]-source[a][k] for k in range(3)]
    cross = (u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
    dot = sum(cross[k]*normals[a][k] for k in range(3))
    outward += dot > 0; inward += dot < 0
assert outward > inward * 100, (outward,inward)
for label, test in [('head',lambda p:p[1]>=1.30),('left hand',lambda p:p[0]>.52),('right hand',lambda p:p[0]<-.52)]:
    ids = [i for i,p in enumerate(source) if test(p)]
    anchor = ids[0]
    error = max(abs(math.dist(source[i],source[anchor])-math.dist(seated[i],seated[anchor])) for i in ids)
    assert error < 0.000002, (label,error)
    print(f'PASS: {label}, {len(ids)} vertices preserve shape; maximum distance error {error:.8f} m')
print(f'PASS: {outward} outward triangles; UVs and topology preserved; seated vertices finite.')
