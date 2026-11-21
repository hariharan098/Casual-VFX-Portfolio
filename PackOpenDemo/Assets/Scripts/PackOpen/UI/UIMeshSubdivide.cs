using UnityEngine;
using UnityEngine.UI;

namespace PackOpen
{
    /// <summary>
    /// Splits a simple Image quad into a columns x rows grid (UV and colour interpolated),
    /// so vertex shaders that bend or wave the image have vertices to move.
    /// Only works on plain 4-vertex quads (Image type Simple).
    /// </summary>
    [AddComponentMenu("PackOpen/UI Mesh Subdivide")]
    public class UIMeshSubdivide : BaseMeshEffect
    {
        [SerializeField, Range(1, 64)] int columns = 16;
        [SerializeField, Range(1, 64)] int rows = 16;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount != 4) return;

            // Image quad order: 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right.
            var q = new UIVertex[4];
            for (int i = 0; i < 4; i++) vh.PopulateUIVertex(ref q[i], i);
            vh.Clear();

            int cols = Mathf.Max(1, columns), rws = Mathf.Max(1, rows);
            for (int y = 0; y <= rws; y++)
            {
                float ty = (float)y / rws;
                for (int x = 0; x <= cols; x++)
                {
                    float tx = (float)x / cols;
                    UIVertex v = q[0];
                    v.position = Bilerp(q[0].position, q[3].position, q[1].position, q[2].position, tx, ty);
                    v.uv0 = Bilerp(q[0].uv0, q[3].uv0, q[1].uv0, q[2].uv0, tx, ty);
                    vh.AddVert(v);
                }
            }

            int stride = cols + 1;
            for (int y = 0; y < rws; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int i0 = y * stride + x;
                    vh.AddTriangle(i0, i0 + stride, i0 + stride + 1);
                    vh.AddTriangle(i0, i0 + stride + 1, i0 + 1);
                }
            }
        }

        // bl = (0,0), br = (1,0), tl = (0,1), tr = (1,1)
        static Vector3 Bilerp(Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, float tx, float ty) =>
            Vector3.Lerp(Vector3.Lerp(bl, br, tx), Vector3.Lerp(tl, tr, tx), ty);

        static Vector4 Bilerp(Vector4 bl, Vector4 br, Vector4 tl, Vector4 tr, float tx, float ty) =>
            Vector4.Lerp(Vector4.Lerp(bl, br, tx), Vector4.Lerp(tl, tr, tx), ty);

#if UNITY_EDITOR
        public void EditorSetup(int c, int r) { columns = c; rows = r; }
#endif
    }
}
