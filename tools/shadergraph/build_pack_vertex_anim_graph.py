#!/usr/bin/env python3
"""Generate a readable Shader Graph version of PackOpen/UI/PackVertexAnim.

Output: PackOpenDemo/Assets/Test/ShaderGraph/PackVertexAnim_SG.shadergraph (+ .meta)

How it works
- Shader Graph files are MultiJson (several JSON objects separated by blank lines).
- The base is Unity's own "2D Sprite Unlit" graph template (Sprite Unlit target, which exposes
  the vertex Position block; the Canvas target has no vertex blocks). Its fragment part is kept.
- Every node/property/group/note is cloned from a real serialized example found in the installed
  Shader Graph / URP packages, with fresh ids, so the field layout always matches this Unity version.
- Nodes are placed in labelled groups, left to right, mirroring the HLSL vertex function.

Needs the Unity project's Library/PackageCache to exist (open the project once).
Re-running regenerates the same graph (ids are derived from a seeded counter, so diffs stay small).
"""
import copy
import glob
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PKG = ROOT / "PackOpenDemo/Library/PackageCache"
OUT_DIR = ROOT / "PackOpenDemo/Assets/Test/ShaderGraph"
GRAPH_NAME = "PackVertexAnim_SG"
GRAPH_GUID = "5a0c3f2e7b1d4c9a8e6f1d2b3c4a5e60"     # fixed so materials can reference it before import
SG = "UnityEditor.ShaderGraph."


# ----------------------------------------------------------------------------- loading
def load_multijson(path):
    raw = Path(path).read_text(encoding="utf-8")
    dec, i, objs = json.JSONDecoder(), 0, []
    while i < len(raw):
        while i < len(raw) and raw[i].isspace():
            i += 1
        if i >= len(raw):
            break
        o, i = dec.raw_decode(raw, i)
        objs.append(o)
    return objs


_cache = {}


def all_graphs():
    if not _cache:
        pats = ["com.unity.shadergraph*/**/*.shadergraph", "com.unity.render-pipelines.universal*/**/*.shadergraph"]
        for pat in pats:
            for f in sorted(glob.glob(str(PKG / pat), recursive=True)):
                try:
                    _cache[f] = load_multijson(f)
                except Exception:
                    pass
        if not _cache:
            sys.exit(f"No shader graphs found under {PKG}. Open the Unity project once so packages are cached.")
    return _cache


def find_template(type_name, pred=None):
    """Return (main_obj, [slot_objs]) of the first example of this node type."""
    for _, objs in all_graphs().items():
        by = {o["m_ObjectId"]: o for o in objs}
        for o in objs:
            if o["m_Type"] == type_name and (pred is None or pred(o, by)):
                slots = [by[s["m_Id"]] for s in o.get("m_Slots", [])]
                return copy.deepcopy(o), copy.deepcopy(slots)
    sys.exit(f"No template found for {type_name}")


def base_graph():
    for f, objs in all_graphs().items():
        if f.endswith("1_2D Sprite Unlit.shadergraph"):
            return copy.deepcopy(objs)
    sys.exit("Base template '1_2D Sprite Unlit.shadergraph' not found in URP package")


# ----------------------------------------------------------------------------- builder
class Node:
    def __init__(self, obj, slots):
        self.obj, self.slots = obj, slots

    @property
    def id(self):
        return self.obj["m_ObjectId"]

    def slot(self, name, direction):
        want = 0 if direction == "in" else 1
        for s in self.slots:
            if s["m_SlotType"] == want and s["m_ShaderOutputName"] == name:
                return s["m_Id"]
        raise KeyError(f"{self.obj['m_Name']} has no {direction} slot '{name}'")

    def set_slot_value(self, name, direction, value):
        want = 0 if direction == "in" else 1
        for s in self.slots:
            if s["m_SlotType"] == want and s["m_ShaderOutputName"] == name:
                s["m_Value"] = value


class GraphBuilder:
    def __init__(self):
        self.objs = {o["m_ObjectId"]: o for o in base_graph()}
        self.graph = next(o for o in self.objs.values() if o["m_Type"] == SG + "GraphData")
        self.category = next(o for o in self.objs.values() if o["m_Type"] == SG + "CategoryData")
        self._n = 0
        self._tpl = {}

    # deterministic 32-hex ids
    def new_id(self, tag=""):
        self._n += 1
        return hashlib.md5(f"{GRAPH_NAME}:{self._n}:{tag}".encode()).hexdigest()

    def new_guid_str(self, tag=""):
        h = hashlib.md5(f"guid:{GRAPH_NAME}:{self._n}:{tag}".encode()).hexdigest()
        return f"{h[:8]}-{h[8:12]}-{h[12:16]}-{h[16:20]}-{h[20:32]}"

    def template(self, key, type_name, pred=None):
        if key not in self._tpl:
            self._tpl[key] = find_template(type_name, pred)
        return copy.deepcopy(self._tpl[key][0]), copy.deepcopy(self._tpl[key][1])

    # ---- nodes
    def add_node(self, key, type_name, name, x, y, group=None, pred=None, **fields):
        obj, slots = self.template(key, type_name, pred)
        obj["m_ObjectId"] = self.new_id(name)
        obj["m_Name"] = name
        obj["m_Group"] = {"m_Id": group or ""}
        pos = obj["m_DrawState"]["m_Position"]
        pos["x"], pos["y"] = float(x), float(y)
        obj["m_Slots"] = []
        for s in slots:
            s["m_ObjectId"] = self.new_id(name + "slot")
            obj["m_Slots"].append({"m_Id": s["m_ObjectId"]})
            self.objs[s["m_ObjectId"]] = s
        obj.update(fields)
        self.objs[obj["m_ObjectId"]] = obj
        self.graph["m_Nodes"].append({"m_Id": obj["m_ObjectId"]})
        return Node(obj, slots)

    def op(self, kind, name, x, y, group):
        tname = {"Add": "AddNode", "Subtract": "SubtractNode", "Multiply": "MultiplyNode", "Sine": "SineNode"}[kind]
        node = self.add_node(kind, SG + tname, name, x, y, group)
        for s in node.slots:                      # clean literal defaults, everything of interest is wired
            if s["m_SlotType"] == 0 and isinstance(s.get("m_Value"), dict):
                s["m_Value"] = {k: 0.0 for k in s["m_Value"]}
        return node

    # ---- properties
    def add_float_property(self, name, default, slider=None):
        obj, _ = self.template("floatprop", SG + "Internal.Vector1ShaderProperty")
        obj["m_ObjectId"] = self.new_id("prop" + name)
        obj["m_Guid"] = {"m_GuidSerialized": self.new_guid_str(name)}
        obj["m_Name"] = name
        obj["m_RefNameGeneratedByDisplayName"] = name
        obj["m_DefaultReferenceName"] = "_" + name
        obj["m_OverrideReferenceName"] = ""
        obj["m_Value"] = float(default)
        if slider:
            obj["m_FloatType"] = 1
            obj["m_RangeValues"] = {"x": slider[0], "y": slider[1]}
        else:
            obj["m_FloatType"] = 0
        self.objs[obj["m_ObjectId"]] = obj
        self.graph["m_Properties"].append({"m_Id": obj["m_ObjectId"]})
        self.category["m_ChildObjectList"].append({"m_Id": obj["m_ObjectId"]})
        return obj["m_ObjectId"]

    def prop_node(self, prop_id, label, x, y, group):
        pred = lambda o, by: by[o["m_Property"]["m_Id"]]["m_Type"].endswith("Vector1ShaderProperty")
        node = self.add_node("floatpropnode", SG + "PropertyNode", "Property", x, y, group, pred=pred)
        node.obj["m_Property"] = {"m_Id": prop_id}
        node.slots[0]["m_DisplayName"] = label
        node.slots[0]["m_ShaderOutputName"] = "Out"
        return node

    # ---- groups / notes
    def add_group(self, title, x, y):
        obj, _ = self.template("group", SG + "GroupData")
        obj["m_ObjectId"] = self.new_id("group" + title)
        obj["m_Title"] = title
        obj["m_Position"] = {"x": float(x), "y": float(y)}
        self.objs[obj["m_ObjectId"]] = obj
        self.graph["m_GroupDatas"].append({"m_Id": obj["m_ObjectId"]})
        return obj["m_ObjectId"]

    def add_note(self, title, text, x, y, w=330, h=140, theme=0):
        obj, _ = self.template("note", SG + "StickyNoteData")
        obj["m_ObjectId"] = self.new_id("note" + title)
        obj["m_Title"], obj["m_Content"], obj["m_Theme"], obj["m_TextSize"] = title, text, theme, 0
        obj["m_Position"] = {"serializedVersion": "2", "x": float(x), "y": float(y), "width": float(w), "height": float(h)}
        obj["m_Group"] = {"m_Id": ""}
        self.objs[obj["m_ObjectId"]] = obj
        self.graph["m_StickyNoteDatas"].append({"m_Id": obj["m_ObjectId"]})

    # ---- wiring
    def connect(self, a, a_slot, b, b_slot):
        """a, b are Node or (object_id, slot_id) tuples."""
        def ref(n, slot, direction):
            if isinstance(n, Node):
                return {"m_Node": {"m_Id": n.id}, "m_SlotId": n.slot(slot, direction)}
            return {"m_Node": {"m_Id": n}, "m_SlotId": slot}
        self.graph["m_Edges"].append({"m_OutputSlot": ref(a, a_slot, "out"), "m_InputSlot": ref(b, b_slot, "in")})

    def serialize(self):
        order = [self.graph] + [o for k, o in self.objs.items() if o is not self.graph]
        return "".join(json.dumps(o, indent=4) + "\n\n" for o in order)


# ----------------------------------------------------------------------------- the graph
def build():
    b = GraphBuilder()
    g = b.graph
    g["m_Path"] = "PackOpen"

    # Keep Unity's fragment path (Sprite Texture * Vertex Color -> Base Color / Alpha), grouped and labelled.
    frag_group = b.add_group("Fragment (from Unity 2D Sprite Unlit template)", -930, 130)
    keep = {"Sampler State", "Swizzle", "Multiply", "Property", "Use Color", "Split", "Sample Texture 2D", "Vertex Color"}
    for o in b.objs.values():
        if o.get("m_Name") in keep and "m_DrawState" in o and o["m_Type"] != SG + "BlockNode":
            o["m_Group"] = {"m_Id": frag_group}
    b.add_note("Fragment",
               "Unchanged from Unity's template: Sprite Texture x Vertex Color feeds Base Color and Alpha. "
               "The HLSL _Color tint is not reproduced here.", -930, 20, 330, 90)

    # ---- properties (names/defaults match the HLSL shader)
    P = {}
    P["PivotX"] = b.add_float_property("PivotX", 0.5, (0, 1))
    P["PivotY"] = b.add_float_property("PivotY", 0.0, (0, 1))
    for n in ["Skew", "SkewY", "BendX", "BendY", "WaveAmpX", "WaveAmpY"]:
        P[n] = b.add_float_property(n, 0.0)
    P["WaveFreq"] = b.add_float_property("WaveFreq", 2.0)
    P["WavePhase"] = b.add_float_property("WavePhase", 0.0)

    X0 = -2900  # leftmost column
    # ---------------- group 1: inputs
    gIn = b.add_group("1. Inputs: UV and pivot", X0, -1250)
    uv = b.add_node("uv", SG + "UVNode", "UV", X0 + 20, -1180, gIn)
    uv.obj["m_OutputChannel"] = 0
    split = b.add_node("split", SG + "SplitNode", "Split (u, v)", X0 + 220, -1180, gIn)
    b.connect(uv, "Out", split, "In")
    pivX = b.prop_node(P["PivotX"], "PivotX", X0 + 20, -1030, gIn)
    pivY = b.prop_node(P["PivotY"], "PivotY", X0 + 20, -970, gIn)
    b.add_note("1. Inputs", "uv = mesh UV (0..1 across the Image). The pivot is the part of the mesh that stays attached "
               "(UV space).", X0, -1400, 360, 120)

    # ---------------- group 2: deltas
    gDel = b.add_group("2. Deltas from the pivot", X0 + 480, -1250)
    dX = b.op("Subtract", "dX = u - PivotX", X0 + 500, -1180, gDel)
    dY = b.op("Subtract", "dY = v - PivotY", X0 + 500, -1040, gDel)
    b.connect(split, "R", dX, "A"); b.connect(pivX, "Out", dX, "B")
    b.connect(split, "G", dY, "A"); b.connect(pivY, "Out", dY, "B")
    b.add_note("2. Deltas", "dX and dY are how far this vertex is from the pivot. Every effect below is scaled by them, "
               "so the pivot never moves.", X0 + 480, -1400, 360, 120)

    # term columns
    XT = X0 + 900

    def term_group(title, y, note):
        gid = b.add_group(title, XT, y - 70)
        b.add_note(title, note, XT, y - 230, 420, 110)
        return gid

    # ---------------- Skew: x += Skew * dY
    y = -1180
    gS = term_group("3a. Skew:  x += Skew * dY", y, "Horizontal shear. The top of a pack slides sideways, the bottom stays put.")
    pSkew = b.prop_node(P["Skew"], "Skew", XT + 20, y, gS)
    mSkew = b.op("Multiply", "Skew * dY", XT + 220, y, gS)
    b.connect(pSkew, "Out", mSkew, "A"); b.connect(dY, "Out", mSkew, "B")
    skew_x = mSkew

    # ---------------- SkewY: y += SkewY * dX
    y = -890
    gSY = term_group("3b. SkewY:  y += SkewY * dX", y, "Vertical shear, the same idea along the other axis.")
    pSkewY = b.prop_node(P["SkewY"], "SkewY", XT + 20, y, gSY)
    mSkewY = b.op("Multiply", "SkewY * dX", XT + 220, y, gSY)
    b.connect(pSkewY, "Out", mSkewY, "A"); b.connect(dX, "Out", mSkewY, "B")
    skew_y = mSkewY

    # ---------------- BendX: x += BendX * dY^2
    y = -600
    gBX = term_group("3c. BendX:  x += BendX * dY^2", y, "Quadratic bend along the height: the farther from the pivot, the more it curves.")
    sqY = b.op("Multiply", "dY * dY", XT + 20, y, gBX)
    b.connect(dY, "Out", sqY, "A"); b.connect(dY, "Out", sqY, "B")
    pBendX = b.prop_node(P["BendX"], "BendX", XT + 20, y + 100, gBX)
    mBendX = b.op("Multiply", "BendX * dY^2", XT + 220, y, gBX)
    b.connect(pBendX, "Out", mBendX, "A"); b.connect(sqY, "Out", mBendX, "B")
    bend_x = mBendX

    # ---------------- BendY: y += BendY * dX^2
    y = -310
    gBY = term_group("3d. BendY:  y += BendY * dX^2", y, "Bend along the length. This is the peel curl of the torn strip.")
    sqX = b.op("Multiply", "dX * dX", XT + 20, y, gBY)
    b.connect(dX, "Out", sqX, "A"); b.connect(dX, "Out", sqX, "B")
    pBendY = b.prop_node(P["BendY"], "BendY", XT + 20, y + 100, gBY)
    mBendY = b.op("Multiply", "BendY * dX^2", XT + 220, y, gBY)
    b.connect(pBendY, "Out", mBendY, "A"); b.connect(sqX, "Out", mBendY, "B")
    bend_y = mBendY

    def wave(title, y, coord_src, amp_name, delta, note):
        """amp * sin(TAU * (coord * Freq + Phase)) * delta"""
        gid = term_group(title, y, note)
        x = XT
        coord_x_freq = b.op("Multiply", "coord * Freq", x + 20, y, gid)
        b.connect(split, coord_src, coord_x_freq, "A")
        pFreq = b.prop_node(P["WaveFreq"], "WaveFreq", x + 20, y + 100, gid)
        b.connect(pFreq, "Out", coord_x_freq, "B")
        plus_phase = b.op("Add", "+ Phase", x + 220, y, gid)
        pPhase = b.prop_node(P["WavePhase"], "WavePhase", x + 220, y + 100, gid)
        b.connect(coord_x_freq, "Out", plus_phase, "A"); b.connect(pPhase, "Out", plus_phase, "B")
        tau = b.add_node("float", SG + "Vector1Node", "TAU", x + 420, y + 100, gid)
        tau.set_slot_value("X", "in", 6.2831853)
        tau.obj["m_Value"] = 6.2831853
        times_tau = b.op("Multiply", "TAU * (...)", x + 420, y, gid)
        b.connect(tau, "Out", times_tau, "A"); b.connect(plus_phase, "Out", times_tau, "B")
        sine = b.op("Sine", "sin", x + 620, y, gid)
        b.connect(times_tau, "Out", sine, "In")
        pAmp = b.prop_node(P[amp_name], amp_name, x + 620, y + 100, gid)
        times_amp = b.op("Multiply", "amp * sin", x + 800, y, gid)
        b.connect(pAmp, "Out", times_amp, "A"); b.connect(sine, "Out", times_amp, "B")
        times_delta = b.op("Multiply", "* delta", x + 980, y, gid)
        b.connect(times_amp, "Out", times_delta, "A"); b.connect(delta, "Out", times_delta, "B")
        return times_delta

    wave_x = wave("3e. WaveX:  x += WaveAmpX * sin(TAU*(v*Freq+Phase)) * dY", y=-20,
                  coord_src="G", amp_name="WaveAmpX", delta=dY,
                  note="Flutter along the height. Multiplying by dY keeps the pivot edge still; Phase makes the wave travel.")
    wave_y = wave("3f. WaveY:  y += WaveAmpY * sin(TAU*(u*Freq+Phase)) * dX", y=270,
                  coord_src="R", amp_name="WaveAmpY", delta=dX,
                  note="Flutter along the length (the strip flapping as it flies off).")

    # ---------------- group 4: sum
    XS = XT + 1400
    gSum = b.add_group("4. Add the terms per axis", XS, -1250)
    b.add_note("4. Sum", "x offset = Skew + BendX + WaveX.   y offset = SkewY + BendY + WaveY.", XS, -1400, 360, 100)
    sx1 = b.op("Add", "Skew + BendX", XS + 20, -1180, gSum)
    b.connect(skew_x, "Out", sx1, "A"); b.connect(bend_x, "Out", sx1, "B")
    sx2 = b.op("Add", "x offset", XS + 220, -1180, gSum)
    b.connect(sx1, "Out", sx2, "A"); b.connect(wave_x, "Out", sx2, "B")
    sy1 = b.op("Add", "SkewY + BendY", XS + 20, -1000, gSum)
    b.connect(skew_y, "Out", sy1, "A"); b.connect(bend_y, "Out", sy1, "B")
    sy2 = b.op("Add", "y offset", XS + 220, -1000, gSum)
    b.connect(sy1, "Out", sy2, "A"); b.connect(wave_y, "Out", sy2, "B")

    # ---------------- group 5: apply to the vertex
    XA = XS + 480
    gOut = b.add_group("5. Move the vertex", XA, -1250)
    b.add_note("5. Vertex output", "offset (x, y, 0) is added to the object-space vertex position and written to the "
               "Vertex Position block. This is `p = v.vertex; p.x += ...; p.y += ...` in the HLSL.", XA, -1400, 420, 120)
    combine = b.add_node("combine", SG + "CombineNode", "Combine (x, y, 0)", XA + 20, -1180, gOut)
    b.connect(sx2, "Out", combine, "R"); b.connect(sy2, "Out", combine, "G")
    combine.set_slot_value("B", "in", 0.0); combine.set_slot_value("A", "in", 0.0)
    pos = b.add_node("position", SG + "PositionNode", "Position (Object)", XA + 20, -1000, gOut)
    pos.obj["m_Space"] = 0
    add = b.op("Add", "position + offset", XA + 260, -1100, gOut)
    b.connect(pos, "Out", add, "A"); b.connect(combine, "RGB", add, "B")

    # vertex position block (input slot id 0)
    block = next(o for o in b.objs.values() if o.get("m_SerializedDescriptor") == "VertexDescription.Position")
    block_slot = b.objs[block["m_Slots"][0]["m_Id"]]["m_Id"]
    b.connect(add, "Out", block["m_ObjectId"], block_slot)

    b.add_note("How to read this graph",
               "Left to right: UV and pivot -> deltas -> six independent terms (skew, bend, wave) -> sum per axis -> "
               "added to the vertex position. Compare with Assets/Shaders/PackVertexAnim.shader (vert function). "
               "Needs UIMeshSubdivide on the Image for bend and wave. Differences: no stencil or clip-rect, "
               "no _Color tint.", X0, -1650, 640, 140, theme=1)
    return b


def main():
    b = build()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUT_DIR / f"{GRAPH_NAME}.shadergraph").write_text(b.serialize(), encoding="utf-8")
    meta = (
        "fileFormatVersion: 2\n"
        f"guid: {GRAPH_GUID}\n"
        "ScriptedImporter:\n"
        "  internalIDToNameTable: []\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 2\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
        "  script: {fileID: 11500000, guid: 625f186215c104763be7675aa2d941aa, type: 3}\n"
    )
    (OUT_DIR / f"{GRAPH_NAME}.shadergraph.meta").write_text(meta, encoding="utf-8")
    nodes = len(b.graph["m_Nodes"]); edges = len(b.graph["m_Edges"])
    print(f"wrote {OUT_DIR / (GRAPH_NAME + '.shadergraph')}  nodes={nodes} edges={edges} groups={len(b.graph['m_GroupDatas'])}")


if __name__ == "__main__":
    main()
