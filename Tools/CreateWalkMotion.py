"""Author a looping walking motion for the beta rig, without external assets."""
import json
import math
import pathlib
from ExtractIdleMotion import quaternion


def curve(keys, phase):
    frame = (phase % 1) * len(keys)
    index, t = int(frame), frame % 1
    a, b, c, d = [keys[(index + offset) % len(keys)] for offset in (-1, 0, 1, 2)]
    return 0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t
                  + (-a + 3 * b - 3 * c + d) * t * t * t)


def bake_walk(destination):
    tracks = []
    frames = 33
    for bone in range(11):
        rotations = []
        for frame in range(frames):
            phase = frame / frames
            cycle = math.sin(phase * math.tau)
            pitch = yaw = roll = 0
            if bone == 0:
                yaw, roll = -cycle * 2, cycle * 1.2
            elif bone == 1:
                pitch, yaw, roll = 0.7, cycle * 3, -cycle
            elif bone == 10:
                pitch, yaw = 0.4, cycle
            elif bone in (2, 4):
                pitch = curve((-24, 0, 20, 0), phase + (0.5 if bone == 4 else 0))
            elif bone in (3, 5):
                pitch = max(0, curve((6, 3, 16, 40), phase + (0.5 if bone == 5 else 0)))
            elif bone in (6, 8):
                pitch = math.cos((phase + (0.5 if bone == 8 else 0)) * math.tau) * 16
                roll = -2 if bone == 6 else 2
            elif bone in (7, 9):
                pitch = -10 - 5 * (1 - math.cos((phase + (0.5 if bone == 9 else 0)) * math.tau))
            rotations.append(dict(zip('xyzw', [round(v, 7) for v in quaternion((pitch, yaw, roll))])))
        tracks.append({'bone': bone, 'rotations': rotations})
    result = {'source': 'Authored beta walking cycle', 'duration': 1.1, 'sampleRate': 30,
              'loop': True, 'tracks': tracks}
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(result, separators=(',', ':')), encoding='utf-8')
    print('Walking: 11 tracks, 33 frames, continuous loop')


if __name__ == '__main__':
    bake_walk(pathlib.Path(__file__).resolve().parents[1] / 'Assets/03_Prefabs/Animations/Walking.json')
