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


def standing_tracks(objects, connections, rotations, start, sample_rate, frame_count, mode):
    """Retarget segment directions to the corrected standing rig, not an idle delta."""
    props = {p[1].split("\0")[0].split(":")[-1]:
             {v[0]: v[4:] for n, _, items in children if n == "Properties70" for _, v, _ in items}
             for name, p, children in objects if name == "Model"}
    tracks = [{"bone": bone, "rotations": []} for bone in range(11)]

    def clamp(value, low, high):
        return max(low, min(high, value))

    def rotate(q, v):
        return multiply(multiply(q, tuple(v) + (0,)), inverse(q))[:3]

    def normal(v):
        length = math.sqrt(sum(c * c for c in v))
        if length < 1e-8:
            raise ValueError("Source skeleton has a zero-length limb")
        return tuple(c / length for c in v)

    def pitch(v):
        return math.degrees(math.atan2(-v[2], -v[1]))

    def bend(a, b):
        return math.degrees(math.acos(clamp(sum(x * y for x, y in zip(a, b)), -1, 1)))

    bind_world = reference_bases(objects, connections, {}, 0)

    def direction(world, parent, child):
        v = rotate(world[parent], props[child]["Lcl Translation"])
        return normal((-v[0], v[1], v[2]))

    for frame in range(frame_count):
        world = reference_bases(objects, connections, rotations, start + frame / sample_rate)

        def segment(parent, child):
            return direction(world, parent, child)

        def unity_rotation(q):
            return (q[0], -q[1], -q[2], q[3])

        def yaw(q):
            forward = rotate(unity_rotation(q), (0, 0, 1))
            return math.degrees(math.atan2(forward[0], forward[2]))

        poses = {i: quaternion((0, 0, 0)) for i in range(11)}
        for side, thigh, shin, arm, forearm in (("Left", 2, 3, 6, 7), ("Right", 4, 5, 8, 9)):
            upper = segment(side + "UpLeg", side + "Leg")
            lower = segment(side + "Leg", side + "Foot")
            bind_upper = direction(bind_world, side + "UpLeg", side + "Leg")
            bind_lower = direction(bind_world, side + "Leg", side + "Foot")
            spread = math.degrees(math.atan2(upper[0], math.hypot(upper[1], upper[2])))
            bind_spread = math.degrees(math.atan2(bind_upper[0], math.hypot(bind_upper[1], bind_upper[2])))
            poses[thigh] = quaternion((clamp(pitch(upper) - pitch(bind_upper), -85, 65), 0, clamp(spread - bind_spread, -10, 10)))
            poses[shin] = quaternion((clamp(bend(upper, lower) - bend(bind_upper, bind_lower), 0, 125), 0, 0))
            upper = segment(side + "Arm", side + "ForeArm")
            lower = segment(side + "ForeArm", side + "Hand")
            spread = math.degrees(math.atan2(upper[0], math.hypot(upper[1], upper[2])))
            rest_spread = math.degrees(math.atan2(-0.04 if side == "Left" else 0.04, 0.17))
            poses[arm] = quaternion((clamp(pitch(upper) + 4, -85, 85), 0, clamp(spread - rest_spread, -12, 12)))
            # This generated low-poly elbow supports a small hinge, not Mixamo's
            # full axial rotation. Preserve the changing bend without twisting skin.
            elbow = math.radians(clamp(bend(upper, lower) * 0.3, 0, 30)) * -0.5
            axis = normal((0.17, -0.04 if side == "Left" else 0.04, 0))
            poses[forearm] = tuple(c * math.sin(elbow) for c in axis) + (math.cos(elbow),)
        hip_yaw = clamp(yaw(world["Hips"]), -6, 6)
        spine = multiply(inverse(world["Hips"]), world["Spine"])
        chest = multiply(inverse(world["Spine"]), world["Spine2"])
        if mode == "run":
            poses[0] = quaternion((0, hip_yaw, 0))
            poses[10] = quaternion((0, clamp(yaw(spine), -3, 3), 0))
            poses[1] = quaternion((0.5, clamp(yaw(chest), -5, 5), 0))
        else:
            def rotation_pitch(q):
                x, y, z, w = unity_rotation(q)
                return math.degrees(math.atan2(2 * (w * x + y * z), 1 - 2 * (x * x + y * y)))
            poses[0] = quaternion((clamp(rotation_pitch(world["Hips"]), -3, 5), hip_yaw, 0))
            bind_spine = multiply(inverse(bind_world["Hips"]), bind_world["Spine"])
            bind_chest = multiply(inverse(bind_world["Spine"]), bind_world["Spine2"])
            poses[10] = quaternion((clamp((rotation_pitch(spine) - rotation_pitch(bind_spine)) * 0.4, -4, 8), clamp(yaw(spine), -3, 3), 0))
            poses[1] = quaternion((clamp((rotation_pitch(chest) - rotation_pitch(bind_chest)) * 0.4, -4, 6), clamp(yaw(chest), -5, 5), 0))
        for bone, q in poses.items():
            if q[3] < 0:
                q = tuple(-v for v in q)
            tracks[bone]["rotations"].append(dict(zip("xyzw", [round(v, 7) for v in q])))
    return tracks


def bake(source, destination, mode="idle", reference_source=None, phase_reference=None, standing_pose=False):
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
    if standing_pose:
        tracks = standing_tracks(objects, connections, rotation, start, sample_rate, frame_count, mode)
        if loop:
            closure = min(0.08, duration * 0.2)
            for track in tracks:
                first = track["rotations"][0]
                for frame, q in enumerate(track["rotations"]):
                    remaining = duration - frame / sample_rate
                    if remaining >= closure:
                        continue
                    t = 1 - remaining / closure
                    t = t * t * (3 - 2 * t)
                    values = [q[k] * (1 - t) + first[k] * t for k in "xyzw"]
                    length = math.sqrt(sum(v * v for v in values))
                    track["rotations"][frame] = dict(zip("xyzw", [round(v / length, 7) for v in values]))
    result = {"source": source.name, "duration": duration, "sampleRate": sample_rate, "loop": loop, "tracks": tracks,
              "takeoff": 0, "apex": 0.5, "landing": 1, "standingPose": standing_pose}
    if phase_reference:
        if not loop:
            raise ValueError("Phase alignment requires a looping animation")
        walking = json.loads(phase_reference.read_text(encoding="utf-8"))
        reference_tracks = {track["bone"]: track["rotations"][0] for track in walking["tracks"]}
        legs = [track for track in tracks if track["bone"] in (2, 3, 4, 5)]

        def pose_error(frame):
            error = 0
            for track in legs:
                a, b = track["rotations"][frame], reference_tracks[track["bone"]]
                dot = min(1, abs(sum(a[axis] * b[axis] for axis in "xyzw")))
                error += (2 * math.acos(dot)) ** 2
            return error

        offset = min(range(frame_count), key=pose_error)
        for track in tracks:
            frames = track["rotations"]
            track["rotations"] = frames[offset:] + frames[:offset]
        result["phaseOffset"] = offset / sample_rate
        result["phaseReference"] = phase_reference.name
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
        from CreateWalkMotion import bake_walk
        bake_walk(project / "Assets/03_Prefabs/Animations/Walking.json")
        reference = project / "Assets/03_Prefabs/rigged character low poly@Breathing Idle.fbx"
        bake(project / "Assets/03_Prefabs/Standard Run.fbx", project / "Assets/03_Prefabs/Animations/StandardRun.json", "run", reference)
        bake(project / "Assets/03_Prefabs/Running.fbx", project / "Assets/03_Prefabs/Animations/Running.json", "run", reference,
             project / "Assets/03_Prefabs/Animations/Walking.json", standing_pose=True)
        bake(project / "Assets/03_Prefabs/Jump.fbx", project / "Assets/03_Prefabs/Animations/Jump.json", "jump", reference, standing_pose=True)
        bake(project / "Assets/03_Prefabs/Sprinting Forward Roll.fbx", project / "Assets/03_Prefabs/Animations/ForwardRoll.json", "roll", reference)
