using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PackOpen.Editor
{
    /// <summary>
    /// Helpers for the Shader Graph preview in Assets/Test/ShaderGraph.
    /// Create Material: makes PackVertexAnim_SG.mat from the generated graph.
    /// Validate: logs shader compile messages and compares property names with the hand-written HLSL shader.
    /// </summary>
    public static class ShaderGraphTestSetup
    {
        const string Dir = "Assets/Test/ShaderGraph";
        const string GraphPath = Dir + "/PackVertexAnim_SG.shadergraph";
        const string MatPath = Dir + "/PackVertexAnim_SG.mat";
        const string HlslShaderName = "PackOpen/UI/PackVertexAnim";

        static readonly string[] AnimProps =
        {
            "_PivotX", "_PivotY", "_Skew", "_SkewY", "_BendX", "_BendY",
            "_WaveAmpX", "_WaveAmpY", "_WaveFreq", "_WavePhase"
        };

        [MenuItem("PackOpen/Shader Graph Test/Create Material")]
        public static void CreateMaterial()
        {
            AssetDatabase.Refresh();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            if (shader == null)
            {
                Debug.LogError("[PackOpen] Shader graph not imported yet: " + GraphPath);
                return;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            else mat.shader = shader;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("[PackOpen] Shader graph test material ready: " + MatPath);
        }

        [MenuItem("PackOpen/Shader Graph Test/Validate")]
        public static void Validate()
        {
            AssetDatabase.Refresh();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            if (shader == null)
            {
                Debug.LogError("[PackOpen] SG VALIDATE: graph did not import as a shader (see import errors above).");
                return;
            }

            int errors = 0;
            foreach (var m in ShaderUtil.GetShaderMessages(shader))
            {
                if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                {
                    errors++;
                    Debug.LogError($"[PackOpen] SG VALIDATE shader error: {m.message} (line {m.line})");
                }
            }

            var have = new HashSet<string>();
            for (int i = 0; i < shader.GetPropertyCount(); i++) have.Add(shader.GetPropertyName(i));
            var missing = new List<string>();
            foreach (var p in AnimProps) if (!have.Contains(p)) missing.Add(p);

            var hlsl = Shader.Find(HlslShaderName);
            string hlslNote = hlsl == null ? "hand-written shader not found" : "hand-written shader found";

            Debug.Log($"[PackOpen] SG VALIDATE: name='{shader.name}' supported={shader.isSupported} compileErrors={errors} " +
                      $"properties={shader.GetPropertyCount()} missingAnimProps=[{string.Join(",", missing)}] ({hlslNote})");
        }
    }
}
