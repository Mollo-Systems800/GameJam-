"""Validate the supplied running asset, retargeted cycle and actual player wiring."""
import hashlib
import json
import math
import pathlib
import re

from ExtractIdleMotion import bake

ROOT = pathlib.Path(__file__).resolve().parents[1]
PREFABS = ROOT / 'Assets/03_Prefabs'


def angle(a, b):
    dot = abs(sum(a[k] * b[k] for k in 'xyzw'))
    return math.degrees(2 * math.acos(min(1, dot)))


def verify():
    source = PREFABS / 'Running.fbx'
    target = PREFABS / 'Animations/Running.json'
    data = json.loads(target.read_text(encoding='utf-8'))
    assert data['source'] == 'Running.fbx' and data['loop']
    assert abs(data['duration'] - 19 / 30) < 1e-8
    assert data['standingPose'] and {t['bone'] for t in data['tracks']} == set(range(11))
    assert data['phaseReference'] == 'Walking.json'
    walk = json.loads((PREFABS / 'Animations/Walking.json').read_text(encoding='utf-8'))
    walk_start = {t['bone']: t['rotations'][0] for t in walk['tracks']}
    legs = [t for t in data['tracks'] if t['bone'] in (2, 3, 4, 5)]
    errors = [sum(angle(t['rotations'][frame], walk_start[t['bone']]) ** 2 for t in legs)
              for frame in range(len(legs[0]['rotations']))]
    assert errors[0] <= min(errors) + 1e-5, 'Run and walk footfall phases are not aligned'
    for track in data['tracks']:
        frames = track['rotations']
        assert len(frames) == round(data['duration'] * data['sampleRate'])
        for frame in frames:
            assert all(math.isfinite(v) for v in frame.values())
            assert abs(sum(v * v for v in frame.values()) - 1) < 1e-5
        steps = [angle(a, b) for a, b in zip(frames, frames[1:] + frames[:1])]
        assert max(steps) < 50, ('Abrupt rotation in baked clip', track['bone'])
        if 2 <= track['bone'] <= 9:
            assert max(angle(frames[0], frame) for frame in frames) > (1 if track['bone'] in (7, 9) else 10), ('Frozen limb', track['bone'])
        if track['bone'] in (3, 5):
            bends = [math.degrees(2 * math.atan2(q['x'], q['w'])) for q in frames]
            assert min(bends) >= 0 and min(bends) < 25 and max(bends) > 80, 'Knee never extends or flexes'
            assert all(abs(q['y']) + abs(q['z']) < 1e-6 for q in frames), 'Knee twists instead of hinging'
    guid = re.search(r'^guid: (\w+)', target.with_suffix('.json.meta').read_text(), re.M)[1]
    prefab = (PREFABS / 'Player.prefab').read_text()
    assert re.search(r'runMotion: \{fileID: 4900000, guid: ' + guid, prefab)
    for field in ('idleMotion', 'walkMotion', 'jumpMotion', 'rollMotion'):
        assert re.search(field + r': \{fileID: 4900000, guid: [0-9a-f]{32}', prefab)
    repeated = ROOT / 'Logs/Running-verification.json'
    bake(source, repeated, 'run', PREFABS / 'rigged character low poly@Breathing Idle.fbx',
         PREFABS / 'Animations/Walking.json', standing_pose=True)
    assert repeated.read_bytes() == target.read_bytes(), 'Baking is not reproducible'
    supplied = pathlib.Path('D:/Descargas/Running.fbx')
    if supplied.exists():
        assert hashlib.sha256(source.read_bytes()).digest() == hashlib.sha256(supplied.read_bytes()).digest()
    jump = json.loads((PREFABS / 'Animations/Jump.json').read_text())
    assert jump['source'] == 'Jump.fbx' and jump['standingPose'] and not jump['loop']
    assert 0 <= jump['takeoff'] < jump['apex'] < jump['landing'] <= 1
    assert {t['bone'] for t in jump['tracks']} == set(range(11))
    for track in jump['tracks']:
        frames = track['rotations']
        assert all(abs(sum(v*v for v in q.values()) - 1) < 1e-5 for q in frames)
        assert max(angle(a, b) for a, b in zip(frames, frames[1:])) < 40, 'Jump pose discontinuity'
        if track['bone'] in (3, 5):
            assert all(q['x'] >= 0 and abs(q['y']) + abs(q['z']) < 1e-6 for q in frames), 'Jump knee hyperextends/twists'
    repeated_jump = ROOT / 'Logs/Jump-verification.json'
    bake(PREFABS / 'Jump.fbx', repeated_jump, 'jump', PREFABS / 'rigged character low poly@Breathing Idle.fbx', standing_pose=True)
    assert repeated_jump.read_bytes() == (PREFABS / 'Animations/Jump.json').read_bytes()
    print('PASS: Running and Jump retargeted to standing pose; 11 tracks, normalized continuous rotations, '
          'natural knee hinges, walking phase alignment, jump phases, deterministic bake and prefab wiring.')


if __name__ == '__main__':
    verify()
