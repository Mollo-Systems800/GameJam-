"""Bake supplied Mixamo FBX animations into motion for the beta's procedural rig.

Uses only the Python standard library. Keeps the source FBX unchanged.
"""
import bisect
import json
import math
import pathlib
import struct
import zlib


def read_fbx(path):
    with open(path, "rb") as stream:
        stream.seek(23)
        version = struct.unpack("<I", stream.read(4))[0]
        wide = version >= 7500
        null_size = 25 if wide else 13

        def node():
            end, count, _ = struct.unpack("<QQQ" if wide else "<III", stream.read(24 if wide else 12))
            size = stream.read(1)[0]
            if not end:
                return None
            name = stream.read(size).decode()
            values = []
            for _ in range(count):
                kind = stream.read(1).decode()
                if kind in "YCFDIL":
                    fmt = {"Y": "h", "C": "?", "F": "f", "D": "d", "I": "i", "L": "q"}[kind]
                    values.append(struct.unpack("<" + fmt, stream.read(struct.calcsize(fmt)))[0])
                elif kind in "SR":
                    data = stream.read(struct.unpack("<I", stream.read(4))[0])
                    values.append(data.decode(errors="replace") if kind == "S" else data)
                else:
                    length, encoding, byte_count = struct.unpack("<III", stream.read(12))
                    data = stream.read(byte_count)
                    if encoding:
                        data = zlib.decompress(data)
                    fmt = {"d": "d", "f": "f", "l": "q", "i": "i", "b": "?", "c": "b"}[kind]
                    values.append(struct.unpack("<" + fmt * length, data))
            children = []
            while stream.tell() < end - null_size:
                child = node()
                if child is None:
                    break
                children.append(child)
            stream.seek(end)
            return name, values, children

        roots = []
        while True:
            item = node()
            if item is None:
                return roots
            roots.append(item)


def quaternion(angles):
    x, y, z = [math.radians(value) * 0.5 for value in angles]
    cx, cy, cz = math.cos(x), math.cos(y), math.cos(z)
    sx, sy, sz = math.sin(x), math.sin(y), math.sin(z)
    # FBX XYZ Euler order: Rz * Ry * Rx.
    return (sx * cy * cz - cx * sy * sz, cx * sy * cz + sx * cy * sz,
            cx * cy * sz - sx * sy * cz, cx * cy * cz + sx * sy * sz)


def multiply(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def motion_channels(source):
    roots = read_fbx(source)
    objects = next(item[2] for item in roots if item[0] == "Objects")
    connections = next(item[2] for item in roots if item[0] == "Connections")
    models = {item[1][0]: item[1][1].split("\0")[0].split(":")[-1]
              for item in objects if item[0] == "Model"}
    curves = {}
    for kind, values, children in objects:
        if kind == "AnimationCurve":
            fields = {name: data[0] for name, data, _ in children if data}
            if "KeyTime" in fields and "KeyValueFloat" in fields:
                curves[values[0]] = ([value / 46186158000 for value in fields["KeyTime"]], fields["KeyValueFloat"])
    targets, channels = {}, {}
    for _, values, _ in connections:
        if values[0] == "OP" and values[2] in models:
            targets[values[1]] = (models[values[2]], values[3])
        elif values[0] == "OP" and values[1] in curves:
            channels[(values[2], values[3].split("|")[-1])] = curves[values[1]]
    rotation, translation = {}, {}
    for node_id, (name, property_name) in targets.items():
        if property_name == "Lcl Rotation":
            rotation[name] = [channels.get((node_id, axis)) for axis in "XYZ"]
        elif property_name == "Lcl Translation":
            translation[name] = [channels.get((node_id, axis)) for axis in "XYZ"]
    return objects, connections, curves, rotation, translation


def sample(channel, time, angular=False):
    if channel is None:
        return 0
    times, values = channel
    right = bisect.bisect_right(times, time)
    if right == 0:
        return values[0]
    if right >= len(times):
        return values[-1]
    left = right - 1
    blend = (time - times[left]) / max(1e-9, times[right] - times[left])
    difference = values[right] - values[left]
    if angular:
        difference = (difference + 180) % 360 - 180
    return values[left] + difference * blend


def inverse(value):
    return (-value[0], -value[1], -value[2], value[3])


def reference_bases(objects, connections, rotations, time):
    models = {p[0]: (p[1].split("\0")[0].split(":")[-1], children)
              for name, p, children in objects if name == "Model"}
    parents = {p[1]: p[2] for _, p, _ in connections if p[0] == "OO" and p[1] in models and p[2] in models}
    cache = {}

    def world(model):
        if model in cache:
            return cache[model]
        name, children = models[model]
        props = {p[0]: p[4:] for n, _, items in children if n == "Properties70" for _, p, _ in items}
        angles = [sample(c, time, True) for c in rotations[name]] if name in rotations else props.get("Lcl Rotation", (0, 0, 0))
        pre = quaternion(props.get("PreRotation", (0, 0, 0)))
        post = inverse(quaternion(props.get("PostRotation", (0, 0, 0))))
        local = multiply(multiply(pre, quaternion(angles)), post)
        cache[model] = multiply(world(parents[model]), local) if model in parents else local
        return cache[model]
    return {name: world(model) for model, (name, _) in models.items()}


def bake(source, destination, mode="idle", reference_source=None):
    objects, connections, curves, rotation, translation = motion_channels(source)
    mapping = {"Hips": 0, "Spine2": 1, "LeftUpLeg": 2, "LeftLeg": 3, "RightUpLeg": 4,
               "RightLeg": 5, "LeftArm": 6, "LeftForeArm": 7, "RightArm": 8, "RightForeArm": 9}
    if not all(name in rotation for name in mapping):
        raise ValueError("The source animation does not contain all required limb tracks")
    start = min(times[0] for times, _ in curves.values())
    end = max(times[-1] for times, _ in curves.values())
    duration = end - start
    if duration <= 0:
        raise ValueError("Animation has no duration")
    loop = mode in ("idle", "run")
    frame_count = max(2, math.ceil(duration * 30) + (0 if loop else 1))
    sample_rate = (frame_count if loop else frame_count - 1) / duration
    if reference_source:
        ref_objects, ref_connections, ref_curves, ref_rotation, _ = motion_channels(reference_source)
        reference_time = min(times[0] for times, _ in ref_curves.values())
    else:
        ref_objects, ref_connections, ref_rotation, reference_time = objects, connections, rotation, start
    bases = reference_bases(ref_objects, ref_connections, ref_rotation, reference_time)

    tracks = []
    for name, bone in mapping.items():
        axes = rotation[name]
        reference = quaternion([sample(channel, reference_time, True) for channel in ref_rotation[name]])
        basis = bases[name]

        def delta_at(time):
            local_delta = multiply(inverse(reference), quaternion([sample(channel, time, True) for channel in axes]))
            delta = multiply(multiply(basis, local_delta), inverse(basis))
            # Reflect FBX X to match Unity's model import handedness.
            delta = (delta[0], -delta[1], -delta[2], delta[3])
            return tuple(-c for c in delta) if delta[3] < 0 else delta

        first_delta = delta_at(start)
        frames = []
        for frame in range(frame_count):
            delta = delta_at(start + frame / sample_rate)
            remaining = duration - frame / sample_rate
            closure = min(0.4 if mode == "idle" else 0.08, duration * 0.2)
            if loop and remaining < closure:
                blend = 1 - remaining / closure
                blend = blend * blend * (3 - 2 * blend)
                delta = tuple(component * (1 - blend) + first_delta[index] * blend
                              for index, component in enumerate(delta))
                length = math.sqrt(sum(component * component for component in delta))
                delta = tuple(component / length for component in delta)
            frames.append(dict(zip("xyzw", [round(component, 7) for component in delta])))
        tracks.append({"bone": bone, "rotations": frames})
    result = {"source": source.name, "duration": duration, "sampleRate": sample_rate, "loop": loop, "tracks": tracks,
              "takeoff": 0, "apex": 0.5, "landing": 1}
    if mode == "roll":
        # Keep the complete revolution outside the limited joint poses. Unwrap Euler
        # pitch before normalising to one turn, so quaternion shortest paths cannot
        # turn a 360-degree roll into a small hip wobble.
        pitch = [sample(rotation["Hips"][0], start + i / sample_rate, True) for i in range(frame_count)]
        unwrapped = [pitch[0]]
        for previous, current in zip(pitch, pitch[1:]):
            unwrapped.append(unwrapped[-1] + (current - previous + 180) % 360 - 180)
        turn = unwrapped[-1] - unwrapped[0]
        if abs(turn) < 270:
            raise ValueError("Roll source does not contain a complete forward turn")
        result["rollDegrees"] = [round((value - unwrapped[0]) * 360 / turn, 6) for value in unwrapped]
    if mode == "jump" and "Hips" in translation and translation["Hips"][1]:
        heights = [sample(translation["Hips"][1], start + i / sample_rate) for i in range(frame_count)]
        apex = max(range(frame_count), key=heights.__getitem__)
        threshold = heights[0] + (heights[apex] - heights[0]) * 0.08
        above = [i for i, height in enumerate(heights) if height > threshold]
        if above:
            result.update(takeoff=max(0, above[0] - 1) / (frame_count - 1), apex=apex / (frame_count - 1),
                          landing=min(frame_count - 1, above[-1] + 1) / (frame_count - 1))
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(result, separators=(",", ":")), encoding="utf-8")
    print(f"Baked {mode}: {len(tracks)} tracks, {frame_count} frames, {duration:.2f}s into {destination.name}")


if __name__ == "__main__":
    project = pathlib.Path(__file__).resolve().parent.parent
    bake(project / "Assets/03_Prefabs/rigged character low poly@Breathing Idle.fbx",
         project / "Assets/03_Prefabs/Animations/BreathingIdle.json")
    import sys
    if "--all" in sys.argv:
        reference = project / "Assets/03_Prefabs/rigged character low poly@Breathing Idle.fbx"
        bake(project / "Assets/03_Prefabs/Standard Run.fbx", project / "Assets/03_Prefabs/Animations/StandardRun.json", "run", reference)
        bake(project / "Assets/03_Prefabs/Jump.fbx", project / "Assets/03_Prefabs/Animations/Jump.json", "jump", reference)
        bake(project / "Assets/03_Prefabs/Sprinting Forward Roll.fbx", project / "Assets/03_Prefabs/Animations/ForwardRoll.json", "roll", reference)
