using System.IO;
using PackOpen.Stages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PackOpen.Editor
{
    /// <summary>
    /// Adds the PackRig (full pack, opened body, tear strip) to the demo scene and puts the DOTween
    /// PackAppear / PackTear stages in their slots. Idempotent and non-destructive: existing objects
    /// are reused, nothing else in the scene is touched. Menu: PackOpen/Setup Pack Rig.
    /// </summary>
    public static class PackRigSetup
    {
        const string ScenePath = "Assets/Scenes/PackOpenDemo.unity";
        const string ArtDir = "Assets/Art/Final/Pack";
        const string MaterialPath = "Assets/Materials/PackVertexAnim.mat";
        const string ShaderName = "PackOpen/UI/PackVertexAnim";

        [System.Serializable]
        class Layout
        {
            public int[] fullSize;
            public int openTopOffset;
            public int[] openSize;
            public int stripTopOffset;
            public int[] stripSize;
        }

        [MenuItem("PackOpen/Setup Pack Rig")]
        public static void Run()
        {
            AssetDatabase.Refresh();

            var shader = Shader.Find(ShaderName);
            var sFull = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/pack_blue_full.png");
            var sOpen = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/pack_blue_open.png");
            var sStrip = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/pack_blue_strip.png");
            string layoutPath = $"{ArtDir}/pack_blue_layout.json";
            if (shader == null || sFull == null || sOpen == null || sStrip == null || !File.Exists(layoutPath))
            {
                Debug.LogError("[PackOpen] Pack rig setup needs the shader and Final/Pack art (run tools/art/make_pack_blue.py, then let Unity import).");
                return;
            }
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(layoutPath));

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var seq = Object.FindFirstObjectByType<PackOpenSequence>();
            var anchors = Object.FindFirstObjectByType<PackOpenAnchors>();
            if (canvas == null || seq == null)
            {
                Debug.LogError("[PackOpen] Scene needs a Canvas and a PackOpenSequence. Run PackOpen > Setup Project first.");
                return;
            }

            var rig = Object.FindFirstObjectByType<PackRig>();
            if (rig == null) rig = BuildRig(canvas, anchors, mat, layout, sFull, sOpen, sStrip);

            WireStage(seq, PackOpenStageId.PackAppear, typeof(PackAppearStage), rig);
            WireStage(seq, PackOpenStageId.PackTear, typeof(PackTearStage), rig);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[PackOpen] Pack rig ready.");
        }

        static PackRig BuildRig(Canvas canvas, PackOpenAnchors anchors, Material mat, Layout l,
                                Sprite sFull, Sprite sOpen, Sprite sStrip)
        {
            var go = new GameObject("PackRig", typeof(RectTransform));
            var root = (RectTransform)go.transform;
            root.SetParent(canvas.transform, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(l.fullSize[0], l.fullSize[1]);
            root.localScale = Vector3.one * 0.5f;
            if (anchors != null && anchors.packCenter != null) root.position = anchors.packCenter.position;

            var body = NewImage("Body_Open", root, sOpen, mat, 16, 16);
            SetTopAnchored(body.rectTransform, l.openSize[1], l.openTopOffset);
            var full = NewImage("Pack_Full", root, sFull, mat, 16, 16);
            Stretch(full.rectTransform);
            var strip = NewImage("Strip", root, sStrip, mat, 32, 4);
            SetTopAnchored(strip.rectTransform, l.stripSize[1], l.stripTopOffset);

            var rig = go.AddComponent<PackRig>();
            rig.root = root;
            rig.full = full;
            rig.bodyOpen = body;
            rig.strip = strip;
            return rig;
        }

        static Image NewImage(string name, RectTransform parent, Sprite sprite, Material mat, int cols, int rows)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.material = mat;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            go.AddComponent<UIMeshSubdivide>().EditorSetup(cols, rows);
            go.SetActive(false);
            return img;
        }

        static void SetTopAnchored(RectTransform rt, float height, float topOffset)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = new Vector2(0, -topOffset);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void WireStage(PackOpenSequence seq, PackOpenStageId id, System.Type stageType, PackRig rig)
        {
            var so = new SerializedObject(seq);
            var slots = so.FindProperty("slots");
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                if (slot.FindPropertyRelative("id").enumValueIndex != (int)id) continue;

                var stageProp = slot.FindPropertyRelative("stage");
                var current = stageProp.objectReferenceValue as Component;
                Component target;
                if (current != null && current.GetType() == stageType)
                {
                    target = current;
                }
                else
                {
                    var host = current != null ? current.gameObject : new GameObject(id.ToString());
                    if (current != null) Object.DestroyImmediate(current);
                    target = host.AddComponent(stageType);
                    stageProp.objectReferenceValue = target;
                }

                var tso = new SerializedObject(target);
                tso.FindProperty("rig").objectReferenceValue = rig;
                tso.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
