"""Import the supplied static props without requiring a glTF Unity package.

The GLB is static (no skin or animations); bake node matrices into a textured OBJ.
Keep the original sources and build repeatable Unity metadata/materials/prefabs.
"""
import json
import math
import pathlib
import shutil
import struct
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[1]
DEST = ROOT / 'Assets/ImportedProps'


def guid(name):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'gamejam/supplied-props/' + name).hex


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8')


def meta(path, kind='DefaultImporter', body=''):
    write(path.with_name(path.name + '.meta'),
          f'fileFormatVersion: 2\nguid: {guid(path.relative_to(ROOT).as_posix())}\n{kind}:\n' + body)


def material(name, texture=None, normal=None, color=(1, 1, 1), metallic=0):
    path = DEST / 'Materials' / (name + '.mat')
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n'
    text += f'  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_Name: {name}\n'
    text += '  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}\n'
    text += '  m_ValidKeywords: ' + ('[_NORMALMAP]' if normal else '[]') + '\n'
    text += '  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 1\n  m_CustomRenderQueue: -1\n'
    text += '  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs:\n'
    for prop, image in [('_BaseMap', texture), ('_MainTex', texture), ('_BumpMap', normal)]:
        ref = '{fileID: 0}' if not image else '{fileID: 2800000, guid: ' + guid(image.relative_to(ROOT).as_posix()) + ', type: 3}'
        text += f'    - {prop}:\n        m_Texture: {ref}\n        m_Scale: {{x: 1, y: 1}}\n        m_Offset: {{x: 0, y: 0}}\n'
    text += f'    m_Ints: []\n    m_Floats:\n    - _Surface: 0\n    - _Cull: 2\n    - _ZWrite: 1\n    - _SrcBlend: 1\n    - _DstBlend: 0\n    - _Smoothness: 0.2\n    - _Metallic: {metallic}\n    - _BumpScale: 0.6\n'
    text += f'    m_Colors:\n    - _BaseColor: {{r: {color[0]}, g: {color[1]}, b: {color[2]}, a: 1}}\n    - _Color: {{r: {color[0]}, g: {color[1]}, b: {color[2]}, a: 1}}\n'
    write(path, text)
    meta(path, 'NativeFormatImporter', '  externalObjects: {}\n  mainObjectFileID: 2100000\n')
    return path


def model_meta(path, remaps):
    template = (ROOT / 'Assets/03_Prefabs/Spade.fbx.meta').read_text()
    template = template.replace('048e09aa40e647d4bd5f133b92ce070b', guid(path.relative_to(ROOT).as_posix()))
    entries = 'externalObjects:\n'
    for name, mat in remaps.items():
        entries += f'  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n      name: {name}\n    second: {{fileID: 2100000, guid: {guid(mat.relative_to(ROOT).as_posix())}, type: 2}}\n'
    template = template.replace('externalObjects: {}\n', entries)
    template = template.replace('fileIdsGeneration: 2', 'fileIdsGeneration: 0')
    write(path.with_name(path.name + '.meta'), template)


def prefab(name, model):
    # Legacy model file IDs keep the root transform stable in the nested prefab.
    template = (ROOT / 'Assets/03_Prefabs/Pala.prefab').read_text()
    template = template.replace('m_Name: Pala', 'm_Name: ' + name)
    template = template.replace('048e09aa40e647d4bd5f133b92ce070b', guid(model.relative_to(ROOT).as_posix()))
    template = template.replace('{x: 0, y: 0.294, z: -0.0726}', '{x: 0, y: 0, z: 0}')
    template = template.replace('{x: 1.808, y: 1.808, z: 1.808}', '{x: 1, y: 1, z: 1}')
    path = ROOT / 'Assets/03_Prefabs' / (name + '.prefab')
    write(path, template)
    meta(path, 'PrefabImporter', '  externalObjects: {}\n')


def convert_glb(source):
    blob = source.read_bytes()
    length = struct.unpack_from('<I', blob, 12)[0]
    data = json.loads(blob[20:20 + length])
    assert not data.get('skins') and not data.get('animations'), 'Only static props are supported'
    offset = 20 + length
    size, tag = struct.unpack_from('<II', blob, offset)
    assert tag == 0x004e4942
    binary = blob[offset + 8:offset + 8 + size]
    textures = []
    for i, image in enumerate(data['images']):
        view = data['bufferViews'][image['bufferView']]
        start = view.get('byteOffset', 0)
        texture = DEST / 'Anderson/Textures' / f'Anderson_{i}.png'
        texture.parent.mkdir(parents=True, exist_ok=True)
        texture.write_bytes(binary[start:start + view['byteLength']])
        textures.append(texture)
    mats = {}
    for i, mat in enumerate(data['materials']):
        texture_index = mat['pbrMetallicRoughness']['baseColorTexture']['index']
        image_index = data['textures'][texture_index]['source']
        mats[f'Anderson_{i}'] = material(f'Anderson_{i}', textures[image_index])

    def accessor(index):
        a = data['accessors'][index]
        view = data['bufferViews'][a['bufferView']]
        fmt = {5126: 'f', 5125: 'I', 5123: 'H', 5121: 'B'}[a['componentType']]
        count = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3}[a['type']]
        step = view.get('byteStride', struct.calcsize(fmt) * count)
        start = view.get('byteOffset', 0) + a.get('byteOffset', 0)
        return [struct.unpack_from('<' + fmt * count, binary, start + n * step) for n in range(a['count'])]

    identity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]

    def multiply(a, b):
        return [sum(a[k * 4 + r] * b[c * 4 + k] for k in range(4)) for c in range(4) for r in range(4)]

    def transform(m, p, direction=False):
        return tuple(sum(m[k * 4 + r] * p[k] for k in range(3)) + (0 if direction else m[12 + r]) for r in range(3))

    primitives = []

    def visit(index, parent):
        node = data['nodes'][index]
        # The supplied file uses matrices throughout; fail explicitly for other transforms.
        assert not any(key in node for key in ('translation', 'rotation', 'scale'))
        matrix = multiply(parent, node.get('matrix', identity))
        if 'mesh' in node:
            for primitive in data['meshes'][node['mesh']]['primitives']:
                assert primitive.get('mode', 4) == 4
                attrs = primitive['attributes']
                verts = [transform(matrix, p) for p in accessor(attrs['POSITION'])]
                normals = [transform(matrix, p, True) for p in accessor(attrs['NORMAL'])]
                # Source matrices are rotations with uniform scale, so normal directions remain valid.
                normals = [tuple(v / max(1e-9, math.sqrt(sum(n * n for n in p))) for v in p) for p in normals]
                primitives.append((verts, normals, accessor(attrs['TEXCOORD_0']),
                                   accessor(primitive['indices']), primitive['material']))
        for child in node.get('children', []):
            visit(child, matrix)

    for root in data['scenes'][data.get('scene', 0)]['nodes']:
        visit(root, identity)
    vertices = [p for primitive in primitives for p in primitive[0]]
    low = [min(p[d] for p in vertices) for d in range(3)]
    high = [max(p[d] for p in vertices) for d in range(3)]
    scale = 1.65 / (high[1] - low[1])
    center = [(high[0] + low[0]) / 2, low[1], (high[2] + low[2]) / 2]
    lines = ['# Static GLB conversion; node transforms baked; height 1.65 m', 'mtllib Anderson.mtl']
    base = 1
    triangles = 0
    for i, (verts, normals, uvs, indices, mat) in enumerate(primitives):
        lines.extend([f'o Anderson_Part_{i}', f'usemtl Anderson_{mat}'])
        # This source faces -Z after its node transforms; turn it to Unity's +Z forward.
        lines.extend('v ' + ' '.join(f'{(p[d] - center[d]) * scale * (-1 if d != 1 else 1):.8g}' for d in range(3)) for p in verts)
        lines.extend(f'vt {p[0]:.8g} {1 - p[1]:.8g}' for p in uvs)
        lines.extend('vn ' + ' '.join(f'{v * (-1 if d != 1 else 1):.8g}' for d, v in enumerate(p)) for p in normals)
        flat = [p[0] + base for p in indices]
        assert len(flat) % 3 == 0
        for n in range(0, len(flat), 3):
            # This supplied GLB's winding opposes its normals; Unity otherwise
            # renders the inside of the face, clothes and fingers.
            lines.append('f ' + ' '.join(f'{j}/{j}/{j}' for j in (flat[n], flat[n + 2], flat[n + 1])))
        base += len(verts)
        triangles += len(flat) // 3
    model = DEST / 'Anderson/Anderson.obj'
    write(model, '\n'.join(lines) + '\n')
    write(model.with_suffix('.mtl'), '\n'.join(f'newmtl Anderson_{i}\nKd 1 1 1\nmap_Kd Textures/Anderson_{data["textures"][mat["pbrMetallicRoughness"]["baseColorTexture"]["index"]]["source"]}.png\n' for i, mat in enumerate(data['materials'])))
    model_meta(model, mats)
    prefab('AndersonShopkeeper', model)
    print(f'Anderson: {len(vertices)} vertices, {triangles} triangles, {len(mats)} materials, height 1.65m')


def main():
    source = pathlib.Path('D:/Descargas/mrs-anderson-bonnies-mother/source/model (42).glb')
    original = DEST / 'Anderson/Source/Anderson.glb'
    original.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, original)
    convert_glb(source)
    chair_source = pathlib.Path('D:/Descargas/silla-inter')
    chair = DEST / 'Chair/InterChair.fbx'
    chair.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(chair_source / 'source/inter.fbx', chair)
    texture_dir = chair.parent / 'Textures'
    texture_dir.mkdir(exist_ok=True)
    for image in (chair_source / 'textures').glob('*.png'):
        shutil.copyfile(image, texture_dir / image.name)
    fabric = material('ChairFabric', texture_dir / 'Fabric036_2K_Color_CHOCOLATE.png', texture_dir / 'Fabric036_2K_Normal.png')
    metal = material('ChairMetal', color=(0.06, 0.06, 0.06), metallic=0.65)
    rubber = material('ChairRubber', color=(0.025, 0.025, 0.025))
    model_meta(chair, {'TELA': fabric, 'METAL NEGRO': metal, 'TAPONES': rubber})
    prefab('InterChair', chair)
    for image in DEST.rglob('*.png'):
        normal = 'Normal' in image.name
        body = f'  externalObjects: {{}}\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 1\n    sRGBTexture: {0 if normal or "Roughness" in image.name else 1}\n  maxTextureSize: 2048\n  textureType: {1 if normal else 0}\n  textureShape: 1\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 2\n    aniso: 4\n    wrapU: 0\n    wrapV: 0\n    wrapW: 0\n'
        meta(image, 'TextureImporter', body)
    for folder in sorted([DEST] + [p for p in DEST.rglob('*') if p.is_dir()]):
        write(folder.with_name(folder.name + '.meta'), f'fileFormatVersion: 2\nguid: {guid(folder.relative_to(ROOT).as_posix())}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n')
    print('Created reusable prefabs: AndersonShopkeeper, InterChair')


if __name__ == '__main__':
    main()
