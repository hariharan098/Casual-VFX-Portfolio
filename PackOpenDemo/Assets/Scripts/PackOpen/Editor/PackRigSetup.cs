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
            PackOpenProjectSetup.WireArt();

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

            var rig = EnsurePackPrefab(canvas, anchors, mat, layout, sFull, sOpen, sStrip);
            var cardPrefab = EnsureCardPrefab();
            var cards = EnsureCardsRig(canvas, rig, cardPrefab);
            if (anchors != null && anchors.packExit != null)
            {
                var exit = (RectTransform)anchors.packExit;
                if (exit.anchoredPosition.y < -1000f) exit.anchoredPosition = new Vector2(0, -600f);
            }

            var idleVfx = EnsureVfx(canvas, rig, "VFX_CardPack_Idle");
            var tearVfx = EnsureVfx(canvas, rig, "VFX_CardPack_Fast");

            Wire(seq, PackOpenStageId.PackAppear, typeof(PackAppearStage), ("rig", rig), ("idleVfx", idleVfx));
            Wire(seq, PackOpenStageId.PackTear, typeof(PackTearStage), ("rig", rig), ("idleVfx", idleVfx), ("tearVfx", tearVfx));
            Wire(seq, PackOpenStageId.PackMoveDown, typeof(PackMoveDownStage), ("rig", rig));
            Wire(seq, PackOpenStageId.CardsMoveOut, typeof(CardsMoveOutStage), ("rig", rig), ("cards", cards));
            Wire(seq, PackOpenStageId.CardsFlip, typeof(CardsFlipStage), ("cards", cards));
            Wire(seq, PackOpenStageId.Hold, typeof(HoldStage));
            Wire(seq, PackOpenStageId.FlyToWallet, typeof(FlyToWalletStage), ("cards", cards));

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

        static void Wire(PackOpenSequence seq, PackOpenStageId id, System.Type stageType, params (string field, Object value)[] fields)
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
                foreach (var (field, value) in fields)
                    tso.FindProperty(field).objectReferenceValue = value;
                tso.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- prefabs ----------------

        const string PrefabDir = "Assets/Prefabs";
        const string PackPrefabPath = PrefabDir + "/Pack_Blue.prefab";
        const string CardPrefabPath = PrefabDir + "/Card.prefab";
        const string CardArtDir = "Assets/Art/Final/Card";

        static PackRig EnsurePackPrefab(Canvas canvas, PackOpenAnchors anchors, Material mat, Layout l,
                                        Sprite sFull, Sprite sOpen, Sprite sStrip)
        {
            Directory.CreateDirectory(PrefabDir);
            var rig = Object.FindFirstObjectByType<PackRig>();
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefabPath);

            if (rig == null)
            {
                if (existing != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(existing, canvas.transform);
                    rig = inst.GetComponent<PackRig>();
                    if (anchors != null && anchors.packCenter != null) rig.root.position = anchors.packCenter.position;
                    return rig;
                }
                rig = BuildRig(canvas, anchors, mat, l, sFull, sOpen, sStrip);
            }
            if (existing == null && !PrefabUtility.IsPartOfPrefabInstance(rig.gameObject))
                PrefabUtility.SaveAsPrefabAssetAndConnect(rig.gameObject, PackPrefabPath, InteractionMode.AutomatedAction);
            return rig;
        }

        static CardView EnsureCardPrefab()
        {
            Directory.CreateDirectory(PrefabDir);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (existing != null) return existing.GetComponent<CardView>();

            var frame = AssetDatabase.LoadAssetAtPath<Sprite>($"{CardArtDir}/card_frame_front.png");
            var mask = AssetDatabase.LoadAssetAtPath<Sprite>($"{CardArtDir}/card_mask.png");
            var back = AssetDatabase.LoadAssetAtPath<Sprite>($"{CardArtDir}/card_back.png");
            var star = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/star.png");
            if (frame == null || mask == null || back == null)
            {
                Debug.LogError("[PackOpen] Card art missing in Art/Final/Card (run tools/art/make_card_art.py).");
                return null;
            }

            var go = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
            var root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(300, 450);
            var view = go.AddComponent<CardView>();
            view.root = root;

            var front = Child("Front", root); Stretch(front);
            var artMask = Child("ArtMask", front); Stretch(artMask);
            var maskImg = artMask.gameObject.AddComponent<Image>();
            maskImg.sprite = mask; maskImg.raycastTarget = false;
            artMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var art = Child("Art", artMask); Stretch(art);
            var artImg = art.gameObject.AddComponent<Image>(); artImg.raycastTarget = false;
            var fr = Child("Frame", front); Stretch(fr);
            var frImg = fr.gameObject.AddComponent<Image>(); frImg.sprite = frame; frImg.raycastTarget = false;

            var stars = Child("Stars", front);
            stars.anchorMin = stars.anchorMax = new Vector2(0.5f, 0f);
            stars.pivot = new Vector2(0.5f, 0.5f);
            stars.anchoredPosition = new Vector2(0, 48);
            stars.sizeDelta = new Vector2(200, 40);
            var hlg = stars.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 6;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            var starImgs = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var s = Child("Star_" + i, stars);
                s.sizeDelta = new Vector2(38, 38);
                starImgs[i] = s.gameObject.AddComponent<Image>();
                starImgs[i].sprite = star; starImgs[i].raycastTarget = false;
            }

            var nameRt = Child("Name", front);
            nameRt.anchorMin = new Vector2(0, 1); nameRt.anchorMax = new Vector2(1, 1);
            nameRt.pivot = new Vector2(0.5f, 1); nameRt.sizeDelta = new Vector2(-30, 48);
            nameRt.anchoredPosition = new Vector2(0, -26);
            var txt = nameRt.gameObject.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 28; txt.fontStyle = FontStyle.Bold; txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white; txt.raycastTarget = false; txt.text = "Card Name";
            var ol = nameRt.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0.1f, 0.12f, 0.3f, 1f); ol.effectDistance = new Vector2(2, -2);

            var backRt = Child("Back", root); Stretch(backRt);
            var backImg = backRt.gameObject.AddComponent<Image>(); backImg.sprite = back; backImg.raycastTarget = false;

            view.front = front.gameObject; view.back = backRt.gameObject;
            view.art = artImg; view.backImage = backImg; view.stars = starImgs; view.nameLabel = txt;
            view.ShowBack();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, CardPrefabPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<CardView>();
        }

        static CardsRig EnsureCardsRig(Canvas canvas, PackRig rig, CardView cardPrefab)
        {
            var cards = Object.FindFirstObjectByType<CardsRig>();
            if (cards == null)
            {
                var go = new GameObject("Cards", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(canvas.transform, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.zero;
                cards = go.AddComponent<CardsRig>();
                cards.container = rt;
            }
            // Cards sit behind the pack so they pop out from behind its top edge.
            cards.container.SetSiblingIndex(Mathf.Max(0, rig.transform.GetSiblingIndex()));
            if (cardPrefab != null) cards.cardPrefab = cardPrefab;
            EditorUtility.SetDirty(cards);
            return cards;
        }

        const string VfxDir = "Assets/VFX/Prefabs/Final";

        /// <summary>Instance of a UI VFX prefab under Canvas/VFX (after the pack, before the tap layer), inactive, with UiVfx.</summary>
        static UiVfx EnsureVfx(Canvas canvas, PackRig rig, string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{VfxDir}/{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[PackOpen] {prefabName}.prefab not found in {VfxDir}; skipping.");
                return null;
            }

            var canvasRt = (RectTransform)canvas.transform;
            var container = canvasRt.Find("VFX") as RectTransform;
            if (container == null)
            {
                container = Child("VFX", canvasRt);
                container.anchorMin = container.anchorMax = new Vector2(0.5f, 0.5f);
                container.sizeDelta = Vector2.zero;
            }
            container.SetSiblingIndex(Mathf.Min(canvasRt.childCount - 1, rig.transform.GetSiblingIndex() + 1));

            Transform inst = container.Find(prefabName);
            if (inst == null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                go.name = prefabName;
                go.SetActive(false);
                inst = go.transform;
            }
            var vfx = inst.GetComponent<UiVfx>();
            if (vfx == null) vfx = inst.gameObject.AddComponent<UiVfx>();
            EditorUtility.SetDirty(vfx);
            return vfx;
        }

        static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
