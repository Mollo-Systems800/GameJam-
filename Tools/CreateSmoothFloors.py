"""Create flat, editable floor prefabs with subtle world-space ceramic tiles."""
import pathlib,re
from ImportSuppliedProps import guid
ROOT=pathlib.Path(__file__).resolve().parents[1]

def meta(path,kind,body='  externalObjects: {}\n'):
    path.with_name(path.name+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid(path.relative_to(ROOT).as_posix())+'\n'+kind+':\n'+body)

def main():
    shader=ROOT/'Assets/04_Materials/SmoothTiles.shader'
    meta(shader,'ShaderImporter')
    for name,color in [('Office',(0.69,0.70,0.65)),('Archive',(0.58,0.49,0.37)),('Courtyard',(0.40,0.49,0.47))]:
        material=ROOT/f'Assets/04_Materials/{name}SmoothFloor.mat'
        material.write_text('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_Name: '''+name+'''SmoothFloor
  m_Shader: {fileID: 4800000, guid: '''+guid(shader.relative_to(ROOT).as_posix())+''', type: 3}
  m_ValidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_CustomRenderQueue: -1
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _TileSize: 0.75
    m_Colors:
    - _BaseColor: {r: '''+str(color[0])+', g: '+str(color[1])+', b: '+str(color[2])+''', a: 1}
    - _GroutColor: {r: 0.28, g: 0.30, b: 0.29, a: 1}
''')
        meta(material,'NativeFormatImporter','  externalObjects: {}\n  mainObjectFileID: 2100000\n')
        source=(ROOT/'Assets/03_Prefabs/JumpPlatform.prefab').read_text()
        source=source.replace('m_Name: JumpPlatform','m_Name: '+name+'SmoothFloor')
        source=re.sub(r'm_Mesh: \{[^}]+\}', 'm_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}',source)
        source=re.sub(r'm_Size: \{[^}]+\}', 'm_Size: {x: 1, y: 1, z: 1}',source)
        source=re.sub(r'm_Center: \{[^}]+\}', 'm_Center: {x: 0, y: 0, z: 0}',source)
        source=re.sub(r'- \{fileID: 2100000, guid: [0-9a-f]+, type: 2\}', '- {fileID: 2100000, guid: '+guid(material.relative_to(ROOT).as_posix())+', type: 2}',source)
        prefab=ROOT/f'Assets/03_Prefabs/{name}SmoothFloor.prefab';prefab.write_text(source);meta(prefab,'PrefabImporter')
    print('Created flat ceramic floor prefabs for Office, Archive and Courtyard.')

if __name__=='__main__':main()
