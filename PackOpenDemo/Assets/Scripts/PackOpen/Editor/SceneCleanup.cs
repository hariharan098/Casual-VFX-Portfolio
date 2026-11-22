using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PackOpen.Editor
{
    /// <summary>
    /// Makes the demo scene presentable, in place (keeps your edits): dark scrim, full-screen tap layer with a
    /// bottom 'Tap to Open' label, no placeholder tile or wallet box. Idempotent. Menu: PackOpen/Clean Scene.
    /// </summary>
    public static class SceneCleanup
    {
        const string ScenePath = "Assets/Scenes/PackOpenDemo.unity";
        const string PackPath = "Assets/Data/Packs/Pack_blue.asset";

        [MenuItem("PackOpen/Clean Scene")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var seq = Object.FindFirstObjectByType<PackOpenSequence>();
            var anchors = Object.FindFirstObjectByType<PackOpenAnchors>();
            if (canvas == null || seq == null || anchors == null)
            {
                Debug.LogError("[PackOpen] Clean Scene needs Canvas, PackOpenSequence and PackOpenAnchors. Run PackOpen > Setup Project first.");
                return;
            }
            var canvasRt = (RectTransform)canvas.transform;
            var pack = AssetDatabase.LoadAssetAtPath<PackData>(PackPath);

            // remove the placeholder tile (remember its pack)
            foreach (var tile in Object.FindObjectsByType<PackTile>(FindObjectsSortMode.None))
            {
                if (tile.pack != null) pack = tile.pack;
                Object.DestroyImmediate(tile.gameObject);
            }

            // wallet: keep only an invisible anchor, reparented out of the old track screen
            var wallet = anchors.walletTarget as RectTransform;
            if (wallet != null)
            {
                for (int i = wallet.childCount - 1; i >= 0; i--) Object.DestroyImmediate(wallet.GetChild(i).gameObject);
                var selfGraphic = wallet.GetComponent<Graphic>();
                if (selfGraphic != null) Object.DestroyImmediate(selfGraphic);
                var anchorsRt = (RectTransform)anchors.transform;
                wallet.SetParent(anchorsRt, true);
                wallet.name = "WalletTarget";
                wallet.sizeDelta = Vector2.zero;
            }

            // scrim
            var scrimRt = FindChild(canvasRt, "Scrim") != null ? FindChild(canvasRt, "Scrim") : FindChild(canvasRt, "TrackScreen");
            if (scrimRt == null) scrimRt = NewRect("Scrim", canvasRt);
            scrimRt.name = "Scrim";
            Stretch(scrimRt);
            var scrim = GetOrAdd<Image>(scrimRt);
            scrim.color = new Color(0f, 0f, 0f, 0.75f);
            scrim.raycastTarget = false;
            scrimRt.SetAsFirstSibling();

            var cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (cam != null) cam.backgroundColor = new Color(0.07f, 0.1f, 0.22f);

            // tap layer + label
            var layerRt = FindChild(canvasRt, "TapLayer") != null ? FindChild(canvasRt, "TapLayer") : NewRect("TapLayer", canvasRt);
            Stretch(layerRt);
            var layerImg = GetOrAdd<Image>(layerRt);
            layerImg.color = new Color(0, 0, 0, 0);
            layerImg.raycastTarget = true;

            var labelRt = FindChild(layerRt, "Label");
            if (labelRt == null)
            {
                labelRt = NewRect("Label", layerRt);
                labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0f);
                labelRt.pivot = new Vector2(0.5f, 0.5f);
                labelRt.anchoredPosition = new Vector2(0, 220);
                labelRt.sizeDelta = new Vector2(700, 90);
                var t = labelRt.gameObject.AddComponent<Text>();
                t.text = "Tap to Open";
                t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                t.fontSize = 56; t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter;
                t.color = Color.white; t.raycastTarget = false;
                var ol = labelRt.gameObject.AddComponent<Outline>();
                ol.effectColor = new Color(0f, 0f, 0f, 0.6f); ol.effectDistance = new Vector2(3, -3);
            }
            var group = GetOrAdd<CanvasGroup>(labelRt);
            group.interactable = false; group.blocksRaycasts = false;

            var tap = GetOrAdd<TapToOpen>(layerRt);
            tap.sequence = seq;
            tap.pack = pack != null ? pack : tap.pack;
            tap.origin = anchors.packOrigin;
            tap.label = group;
            layerRt.SetAsLastSibling();
            EditorUtility.SetDirty(tap);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PackOpen] Scene cleaned: scrim, tap-anywhere layer, bottom label.");
        }

        static T GetOrAdd<T>(Component c) where T : Component
        {
            var t = c.GetComponent<T>();
            return t != null ? t : c.gameObject.AddComponent<T>();
        }

        static RectTransform FindChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            return t != null ? (RectTransform)t : null;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
