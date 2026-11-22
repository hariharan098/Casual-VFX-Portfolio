using System.Collections.Generic;
using System.IO;
using PackOpen.Stages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PackOpen.Editor
{
    /// <summary>
    /// One-shot, idempotent scaffolding. Menu: PackOpen/Setup Project.
    /// Creates URP assets, mock data, and a minimal scene. Never touches Art/Final, Prefabs, Shaders, VFX, Anim.
    /// </summary>
    public static class PackOpenProjectSetup
    {
        const string Data = "Assets/Data";
        const string Gen = "Assets/Art/Generated";
        const string ScenePath = "Assets/Scenes/PackOpenDemo.unity";

        static readonly (string id, string name, int rarity)[] Cards =
        {
            ("c01", "Mushroom Cottage", 1), ("c02", "Potion Bottle", 1), ("c03", "Slime Buddy", 1), ("c04", "Lucky Acorn", 1),
            ("c05", "Tiny Wizard", 2), ("c06", "Mimic Chest", 2), ("c07", "Baby Griffin", 2), ("c08", "Moon Lantern", 2),
            ("c09", "Golden Dragon", 3), ("c10", "Crystal Heart", 3), ("c11", "Phoenix Egg", 3), ("c12", "Starlight Unicorn", 3),
        };

        [MenuItem("PackOpen/Setup Project")]
        public static void Run()
        {
            EnsureFolders();
            SetupRenderPipeline();
            SetupPlayerSettings();
            var pool = CreateCards();
            RemoveOldAssets();
            var blue = CreatePack("blue", "Blue Pack", 3, new[] { 45f, 40f, 15f }, pool);
            WireArt();
            CreateScene(blue);
            AssetDatabase.SaveAssets();
            PackRigSetup.Run();
            Debug.Log("[PackOpen] Setup complete.");
        }

        /// <summary>Assigns sprites in Art/Generated to cards/packs by file name. Re-run after generating art.</summary>
        [MenuItem("PackOpen/Wire Generated Art")]
        public static void WireArt()
        {
            AssetDatabase.Refresh();
            foreach (var c in Cards)
            {
                var card = AssetDatabase.LoadAssetAtPath<CardData>($"{Data}/Cards/Card_{c.id}.asset");
                if (card == null) continue;
                card.face = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Final/Card/art_{c.id}.png") ?? LoadSprite($"card_{c.id}");
                EditorUtility.SetDirty(card);
            }
            foreach (var pid in new[] { "blue" })
            {
                var pack = AssetDatabase.LoadAssetAtPath<PackData>($"{Data}/Packs/Pack_{pid}.asset");
                if (pack == null) continue;
                pack.wrapper = LoadSprite($"pack_{pid}");
                pack.tearStrip = LoadSprite("foil_strip");
                pack.cardBack = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Final/Card/card_back.png") ?? LoadSprite("card_back");
                pack.packFull = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Final/Pack/pack_blue_full.png");
                pack.packOpen = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Final/Pack/pack_blue_open.png");
                pack.packStrip = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Final/Pack/pack_blue_strip.png");
                EditorUtility.SetDirty(pack);
            }
            AssetDatabase.SaveAssets();
        }

        static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Gen}/{name}.png");

        static void EnsureFolders()
        {
            foreach (var f in new[] { Data, Data + "/Cards", Data + "/Packs", "Assets/Settings", "Assets/Scenes", Gen })
                if (!Directory.Exists(f)) Directory.CreateDirectory(f);
            AssetDatabase.Refresh();
        }

        static void SetupRenderPipeline()
        {
            const string rendererPath = "Assets/Settings/URP_Renderer.asset";
            const string assetPath = "Assets/Settings/URP_Mobile.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rendererPath);
                asset = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
        }

        static void SetupPlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
        }

        static void RemoveOldAssets()
        {
            AssetDatabase.DeleteAsset($"{Data}/Packs/Pack_green.asset");
        }

        static List<CardData> CreateCards()
        {
            var list = new List<CardData>();
            foreach (var c in Cards)
            {
                string path = $"{Data}/Cards/Card_{c.id}.asset";
                var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card == null)
                {
                    card = ScriptableObject.CreateInstance<CardData>();
                    AssetDatabase.CreateAsset(card, path);
                }
                card.id = c.id; card.displayName = c.name; card.rarity = c.rarity;
                EditorUtility.SetDirty(card);
                list.Add(card);
            }
            return list;
        }

        static PackData CreatePack(string id, string name, int count, float[] weights, List<CardData> pool)
        {
            string path = $"{Data}/Packs/Pack_{id}.asset";
            var pack = AssetDatabase.LoadAssetAtPath<PackData>(path);
            if (pack == null)
            {
                pack = ScriptableObject.CreateInstance<PackData>();
                AssetDatabase.CreateAsset(pack, path);
            }
            pack.id = id; pack.displayName = name; pack.cardCount = count;
            pack.rarityWeights = weights; pack.pool = new List<CardData>(pool);
            EditorUtility.SetDirty(pack);
            return pack;
        }

        // ---------------- Scene ----------------
        static void CreateScene(PackData blue)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.16f);
            cam.orthographic = true;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<UniversalAdditionalCameraData>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var canvasRt = (RectTransform)canvasGo.transform;

            // Placeholder track screen
            var track = NewRect("TrackScreen", canvasRt, Vector2.zero, Vector2.zero);
            Stretch(track);
            var bg = track.gameObject.AddComponent<Image>();
            bg.color = new Color(0.16f, 0.18f, 0.3f);
            bg.raycastTarget = false;

            var blueTile = NewTile("Tile_Pack", track, new Vector2(0, 300), blue, new Color(0.3f, 0.5f, 0.9f), "Tap to\nOpen Pack");

            var wallet = NewRect("Wallet", track, new Vector2(0, -820), new Vector2(260, 140));
            var walletImg = wallet.gameObject.AddComponent<Image>();
            walletImg.color = new Color(0.9f, 0.7f, 0.2f);
            walletImg.raycastTarget = false;
            AddLabel(wallet, "Wallet");

            // Anchors (empty transforms, yours to move)
            var anchorsRoot = NewRect("Anchors", canvasRt, Vector2.zero, Vector2.zero);
            Stretch(anchorsRoot);
            var anchors = anchorsRoot.gameObject.AddComponent<PackOpenAnchors>();
            anchors.packOrigin = NewRect("PackOrigin", anchorsRoot, new Vector2(0, 300), Vector2.zero);
            anchors.packCenter = NewRect("PackCenter", anchorsRoot, new Vector2(0, 100), Vector2.zero);
            anchors.cardSlots = new Transform[]
            {
                NewRect("CardSlot_0", anchorsRoot, new Vector2(-320, 50), Vector2.zero),
                NewRect("CardSlot_1", anchorsRoot, new Vector2(0, 50), Vector2.zero),
                NewRect("CardSlot_2", anchorsRoot, new Vector2(320, 50), Vector2.zero),
            };
            anchors.packExit = NewRect("PackExit", anchorsRoot, new Vector2(0, -1200), Vector2.zero);
            anchors.walletTarget = wallet;

            // Sequence
            var seqGo = new GameObject("PackOpenSequence");
            var tap = seqGo.AddComponent<TapInput>();
            var seq = seqGo.AddComponent<PackOpenSequence>();
            var slots = new List<PackOpenSequence.StageSlot>();
            var stagesGo = new GameObject("Stages");
            stagesGo.transform.SetParent(seqGo.transform);
            foreach (PackOpenStageId id in System.Enum.GetValues(typeof(PackOpenStageId)))
            {
                var child = new GameObject(id.ToString());
                child.transform.SetParent(stagesGo.transform);
                var stage = child.AddComponent<PlaceholderStage>();
                if (id == PackOpenStageId.Hold)
                {
                    var so = new SerializedObject(stage);
                    so.FindProperty("duration").floatValue = 0.8f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                slots.Add(new PackOpenSequence.StageSlot { id = id, stage = stage });
            }
            seq.EditorSetup(anchors, tap, slots);

            var dbg = seqGo.AddComponent<PackOpenDebugPanel>();
            var dso = new SerializedObject(dbg);
            dso.FindProperty("sequence").objectReferenceValue = seq;
            dso.FindProperty("pack").objectReferenceValue = blue;
            dso.ApplyModifiedPropertiesWithoutUndo();

            blueTile.GetComponent<PackTile>().sequence = seq;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static RectTransform NewTile(string name, RectTransform parent, Vector2 pos, PackData pack, Color color, string label)
        {
            var rt = NewRect(name, parent, pos, new Vector2(380, 520));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var tile = rt.gameObject.AddComponent<PackTile>();
            tile.pack = pack;
            AddLabel(rt, label);
            return rt;
        }

        static void AddLabel(RectTransform parent, string text)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 44;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
