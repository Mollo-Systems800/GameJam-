"""Derive a seated static pose from the supplied T-pose, preserving UVs/materials."""
import math
from pathlib import Path
from ImportSuppliedProps import guid

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Assets/ImportedProps/Anderson/Anderson.obj'
TARGET = SOURCE.with_name('AndersonSeated.obj')


def smooth(low, high, v):
    t = max(0, min(1, (v-low)/(high-low)))
    return t*t*(3-2*t)


def mix(a,b,t):
    return tuple(x+(y-x)*t for x,y in zip(a,b))


def rotate_x(v, degrees):
    c,s=math.cos(math.radians(degrees)),math.sin(math.radians(degrees))
    return (v[0],c*v[1]-s*v[2],s*v[1]+c*v[2])


def rotate_z(v, degrees):
    c,s=math.cos(math.radians(degrees)),math.sin(math.radians(degrees))
    return (c*v[0]-s*v[1],s*v[0]+c*v[1],v[2])


def posed(p):
    # The supplied OBJ actually faces -Z. Turn the entire figure toward +Z
    # before bending, so knees and hands extend in the same direction as her face.
    x,y,z=-p[0],p[1],-p[2]
    canonical=(x,y,z)
    hip,knee,seat=.84,.48,.5
    torso=(x,y+seat-hip,z)
    # Bend the thighs forward; leave shins vertical and feet resting on the floor.
    thigh=(x,seat+z,hip-y)
    shin=(x,y+seat-knee,z+hip-knee)
    legs=mix(shin,thigh,smooth(knee-.045,knee+.045,y))
    body=mix(legs,torso,smooth(hip-.09,hip+.06,y))
    # Relax the T-pose shoulders and bring forearms forward onto the lap.
    side=1 if x>=0 else -1
    root=(side*.17,1.18,-.02)
    arm=rotate_z(tuple(canonical[i]-root[i] for i in range(3)),-side*75)
    elbow=rotate_z((side*.26,-.10,0),-side*75)
    fore=rotate_x(tuple(arm[i]-elbow[i] for i in range(3)),-100)
    fore=tuple(fore[i]+elbow[i] for i in range(3))
    arm=mix(arm,fore,smooth(.37,.49,abs(x)))
    arm=(arm[0]+root[0],arm[1]+root[1]+seat-hip,arm[2]+root[2])
    return mix(body,arm,smooth(.17,.26,abs(x))*smooth(.86,.96,y))


def normal(p,n):
    # Inverse-transpose Jacobian through the same deformation as the vertices.
    e=.0001
    cols=[]
    for i in range(3):
        a=list(p);b=list(p);a[i]+=e;b[i]-=e
        cols.append(tuple((x-y)/(2*e) for x,y in zip(posed(a),posed(b))))
    def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
    cof=[cross(cols[1],cols[2]),cross(cols[2],cols[0]),cross(cols[0],cols[1])]
    det=sum(cols[0][i]*cof[0][i] for i in range(3))
    out=tuple(sum(cof[j][i]*n[j] for j in range(3))*(1 if det>=0 else -1) for i in range(3))
    length=math.sqrt(sum(v*v for v in out))
    return tuple(v/max(1e-8,length) for v in out)


def main():
    lines=SOURCE.read_text().splitlines()
    vertices=[tuple(map(float,l.split()[1:4])) for l in lines if l.startswith('v ')]
    normals=[tuple(map(float,l.split()[1:4])) for l in lines if l.startswith('vn ')]
    assert len(vertices)==len(normals)
    output=[];vi=ni=0
    for l in lines:
        if l.startswith('v '):
            l='v '+' '.join(f'{v:.8g}' for v in posed(vertices[vi]));vi+=1
        elif l.startswith('vn '):
            l='vn '+' '.join(f'{v:.8g}' for v in normal(vertices[ni],normals[ni]));ni+=1
        elif l.startswith('# '):l='# Seated derivative of supplied Anderson; original model unchanged'
        output.append(l)
    TARGET.write_text('\n'.join(output)+'\n')
    meta=SOURCE.with_suffix('.obj.meta').read_text()
    meta=meta.replace('84e6c6183a7f5d4399badef04cb42f95',guid(TARGET.relative_to(ROOT).as_posix()))
    TARGET.with_suffix('.obj.meta').write_text(meta)
    source_prefab=ROOT/'Assets/03_Prefabs/AndersonShopkeeper.prefab'
    prefab=ROOT/'Assets/03_Prefabs/SeatedWoman.prefab'
    prefab.write_text(source_prefab.read_text().replace('AndersonShopkeeper','SeatedWoman')
                      .replace('84e6c6183a7f5d4399badef04cb42f95',guid(TARGET.relative_to(ROOT).as_posix())))
    prefab.with_suffix('.prefab.meta').write_text('fileFormatVersion: 2\nguid: '+guid(prefab.relative_to(ROOT).as_posix())+'\nPrefabImporter:\n  externalObjects: {}\n')
    print('Seated woman:',len(vertices),'vertices; seated hips at 0.5m; UVs, materials and source preserved.')


if __name__=='__main__':main()
