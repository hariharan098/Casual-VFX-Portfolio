# Shader Graph preview: `PackVertexAnim_SG`

A read-only study copy of the hand-written `PackOpen/UI/PackVertexAnim` shader, rebuilt as a Shader Graph so you can see how the nodes connect. The runtime sequence does not use it.

- Graph: `Assets/Test/ShaderGraph/PackVertexAnim_SG.shadergraph`
- Material: `Assets/Test/ShaderGraph/PackVertexAnim_SG.mat`
- Generator: `tools/shadergraph/build_pack_vertex_anim_graph.py` (rerun it to regenerate the graph; ids are deterministic)
- Validation: `PackOpen > Shader Graph Test > Validate` (checks import, compile errors and property names)

## How to read it

Open the graph. Every step is a labelled group with a sticky note, left to right:

1. **Inputs**: object UV (split into U, V) and the Pivot X/Y properties.
2. **Deltas**: `dX = U - PivotX`, `dY = V - PivotY` (distance from the pivot).
3. **Terms** (one group each): Skew, SkewY, BendX, BendY, WaveX, WaveY. Each is the amplitude property times the delta math. The wave terms use `sin(2π · freq · delta + phase)`.
4. **Sum**: all X terms are added, and all Y terms are added.
5. **Move the vertex**: Combine (X, Y, 0) is added to the Object-space Position and goes to the `Vertex > Position` block.
6. **Fragment**: texture sample times vertex colour to Base Color and Alpha, as in the Sprite Unlit template.

Property names match the HLSL shader: `_PivotX _PivotY _Skew _SkewY _BendX _BendY _WaveAmpX _WaveAmpY _WaveFreq _WavePhase`.

## Differences from the HLSL shader

| Topic | HLSL shader | Shader Graph |
|---|---|---|
| Target | UI (Canvas) | Sprite Unlit. The Canvas target has no vertex blocks, so the vertex stage can't be edited there. |
| Stencil / RectMask2D clip | Supported | Not included |
| `_Color` tint | Supported | Vertex colour is used, so the Image colour still tints, but there is no separate `_Color` property |
| Offset units | Object pixels scaled by UV distance | Same maths, in object space |
| Mesh subdivision | Needs `UIMeshSubdivide` | Same. Vertex movement only looks smooth if the mesh has a grid. |

To preview it on a UI Image, assign `PackVertexAnim_SG.mat` to the Image's material and keep `UIMeshSubdivide` on it. Sprite Unlit has no Canvas stencil, so masking won't work in this preview.
