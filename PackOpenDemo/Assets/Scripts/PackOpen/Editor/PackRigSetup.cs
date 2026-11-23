using System.IO;
using DG.Tweening;
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
        public static void Run() { Apply(false); }

        /// <summary>Same as Run but also saves the scene (used by Setup Project and batch tools).</summary>
        public static void RunAndSave() { Apply(true); }

        static void Apply(bool save)
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

            // Use the open scene so unsaved edits (VFX tweaks etc.) are kept.
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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
            EnsureCardRevealVfx();
            var cards = EnsureCardsRig(canvas, rig, cardPrefab);
            if (anchors != null && anchors.packExit != null)
            {
                var exit = (RectTransform)anchors.packExit;
                if (exit.anchoredPosition.y < -1000f) exit.anchoredPosition = new Vector2(0, -600f);
            }

            var idleVfx = EnsureVfx(canvas, rig, "VFX_CardPack_Idle", true);
            var tearVfx = EnsureVfx(canvas, rig, "VFX_CardPack_Fast", false);
            var glowVfx = EnsureVfx(canvas, rig, "VFX_Card_Glow", true);

            Wire(seq, PackOpenStageId.PackAppear, typeof(PackAppearStage), ("rig", rig), ("idleVfx", idleVfx));
            Wire(seq, PackOpenStageId.PackTear, typeof(PackTearStage), ("rig", rig), ("idleVfx", idleVfx), ("tearVfx", tearVfx));
            LayerVfx(canvas, rig, cards);
            MigrateAppear(seq);
            MigrateTear(seq);
            Wire(seq, PackOpenStageId.PackMoveDown, typeof(PackMoveDownStage), ("rig", rig));
            Wire(seq, PackOpenStageId.CardsMoveOut, typeof(CardsMoveOutStage), ("rig", rig), ("cards", cards), ("glowVfx", glowVfx));
            Wire(seq, PackOpenStageId.CardsFlip, typeof(CardsFlipStage), ("cards", cards));
            Wire(seq, PackOpenStageId.Hold, typeof(HoldStage));
            Wire(seq, PackOpenStageId.FlyToWallet, typeof(FlyToWalletStage), ("cards", cards));

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[PackOpen] Pack rig ready." + (save ? " (saved)" : " Save the scene (Cmd+S) to keep it."));
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
            // Ref gif frame 82: card ~23% of screen width (≈250px at 1080 reference), 2:3 aspect
            root.sizeDelta = new Vector2(250, 375);
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

        /// <summary>Adds UiVfx to nested card burst/idle and wires CardView on the Card prefab asset.</summary>
        static void EnsureCardRevealVfx()
        {
            var path = CardPrefabPath;
            if (!System.IO.File.Exists(path)) return;
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = contents.GetComponent<CardView>();
                if (view == null) return;
                UiVfx burst = null, idle = null;
                foreach (var t in contents.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "VFX_Card_Burst")
                    {
                        burst = t.GetComponent<UiVfx>();
                        if (burst == null) burst = t.gameObject.AddComponent<UiVfx>();
                    }
                    if (t.name == "VFX_CardPack_Idle")
                    {
                        idle = t.GetComponent<UiVfx>();
                        if (idle == null) idle = t.gameObject.AddComponent<UiVfx>();
                    }
                }
                var so = new SerializedObject(view);
                var burstP = so.FindProperty("burstVfx");
                var idleP = so.FindProperty("revealIdleVfx");
                if (burstP != null) burstP.objectReferenceValue = burst;
                if (idleP != null) idleP.objectReferenceValue = idle;
                so.ApplyModifiedPropertiesWithoutUndo();
                view.EnforceCanvasLayers();
                foreach (var cv in contents.GetComponentsInChildren<Canvas>(true))
                {
                    var cso = new SerializedObject(cv);
                    var over = cso.FindProperty("m_OverrideSorting");
                    if (over != null) over.boolValue = true;
                    cso.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
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

        const int AppearTuningVersion = 2;

        /// <summary>Applies the side-entry + bounce preset (reference frames 59-67) once, then marks the component.</summary>
        static void MigrateAppear(PackOpenSequence seq)
        {
            var appear = seq.GetComponentInChildren<PackAppearStage>(true);
            if (appear == null) return;
            var so = new SerializedObject(appear);
            var version = so.FindProperty("tuningVersion");
            if (version.intValue >= AppearTuningVersion) return;

            void F(string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; }
            void E(string n, Ease v) { var p = so.FindProperty(n); if (p != null) p.enumValueIndex = System.Array.IndexOf(p.enumNames, v.ToString()); }

            so.FindProperty("enterFromSide").boolValue = true;
            so.FindProperty("sideOffset").vector2Value = new Vector2(700f, -150f);
            F("flyTime", 0.45f); F("startScale", 0.55f); F("startTilt", -50f);
            E("xEase", Ease.OutCubic); E("yEase", Ease.OutBack); E("scaleEase", Ease.OutBack); E("tiltEase", Ease.OutBack);
            F("bounceTime", 0.4f); F("bounceHop", 28f); F("bounceSquash", 0.08f);
            version.intValue = AppearTuningVersion;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        const int TearTuningVersion = 2;

        /// <summary>
        /// Applies the reference-based tear preset (frames 64-71) once to scenes made before it, then marks the
        /// component so later hand-tuning is never overwritten.
        /// </summary>
        static void MigrateTear(PackOpenSequence seq)
        {
            var tear = seq.GetComponentInChildren<PackTearStage>(true);
            if (tear == null) return;
            var so = new SerializedObject(tear);
            var version = so.FindProperty("tuningVersion");
            if (version.intValue >= TearTuningVersion) return;

            void F(string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; }
            void E(string n, Ease v) { var p = so.FindProperty(n); if (p != null) p.enumValueIndex = System.Array.IndexOf(p.enumNames, v.ToString()); }
            void V(string n, Vector2 v) { var p = so.FindProperty(n); if (p != null) p.vector2Value = v; }

            F("wobbleTime", 0.45f); F("wobbleAngle", 4f); F("squash", 0.02f); F("skew", 10f);
            F("pullAngle", 7f); F("recoilTime", 0.45f);
            F("ripTime", 0.6f); F("ripFraction", 0.2f); F("ripLift", 30f); F("ripRotation", 25f); F("ripCurl", 110f);
            E("ripXEase", Ease.InQuad); E("ripYEase", Ease.InOutSine); E("ripRotEase", Ease.InQuad);
            F("liftTime", 0.4f); V("flyOffset", new Vector2(-820f, 1000f)); F("flyRotation", 75f); F("curl", 180f);
            F("flutterAmp", 40f); F("flutterTravel", 2.2f);
            E("liftXEase", Ease.InQuad); E("liftYEase", Ease.InCubic); F("stripFadeFraction", 0.15f);
            V("tearOffset", new Vector2(180f, -30f)); F("vfxStopTime", 1.2f); F("vfxLingerTime", 0.25f); F("idleStopLead", 0.3f);
            F("bodySettleTime", 0.65f);
            version.intValue = TearTuningVersion;
            so.ApplyModifiedPropertiesWithoutUndo();

            // strip hinge on the left so the right corner lifts first
            var rigProp = new SerializedObject(tear).FindProperty("rig");
            var rig = rigProp != null ? rigProp.objectReferenceValue as PackRig : null;
            if (rig != null)
            {
                var rso = new SerializedObject(rig);
                rso.FindProperty("stripPivotX").floatValue = 0f;
                rso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        const string VfxDir = "Assets/VFX/Prefabs/Final";

        /// <summary>
        /// Instance of a UI VFX prefab, inactive, with UiVfx. An instance that already exists anywhere under the Canvas is reused
        /// (so hand-placed ones are kept). New ones go to Canvas/VFX_Back (behind cards and pack) or Canvas/VFX (in front of the pack).
        /// </summary>
        static UiVfx EnsureVfx(Canvas canvas, PackRig rig, string prefabName, bool behindCards)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{VfxDir}/{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[PackOpen] {prefabName}.prefab not found in {VfxDir}; skipping.");
                return null;
            }

            var canvasRt = (RectTransform)canvas.transform;
            Transform inst = null;
            foreach (var t in canvasRt.GetComponentsInChildren<Transform>(true))
                if (t.name == prefabName) { inst = t; break; }
            if (inst == null)
            {
                string containerName = behindCards ? "VFX_Back" : "VFX";
                var container = canvasRt.Find(containerName) as RectTransform;
                if (container == null)
                {
                    container = Child(containerName, canvasRt);
                    container.anchorMin = container.anchorMax = new Vector2(0.5f, 0.5f);
                    container.sizeDelta = Vector2.zero;
                }
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

        /// <summary>Canvas order (back to front): Scrim, Anchors, VFX_Back (idle, card glow), Cards, PackRig, VFX (tear), TapLayer.</summary>
        static void LayerVfx(Canvas canvas, PackRig rig, CardsRig cards)
        {
            var canvasRt = (RectTransform)canvas.transform;
            var back = canvasRt.Find("VFX_Back");
            var front = canvasRt.Find("VFX");
            var cardsT = cards != null ? FindTop(canvasRt, cards.container) : null;
            var rigT = FindTop(canvasRt, rig.transform);
            var order = new System.Collections.Generic.List<Transform>();
            foreach (var t in new[] { back, cardsT, rigT, front }) if (t != null) order.Add(t);
            int start = int.MaxValue;
            foreach (var t in order) start = Mathf.Min(start, t.GetSiblingIndex());
            for (int i = 0; i < order.Count; i++) order[i].SetSiblingIndex(start + i);
        }

        static Transform FindTop(Transform canvas, Transform t)
        {
            while (t != null && t.parent != canvas) t = t.parent;
            return t;
        }

        static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
