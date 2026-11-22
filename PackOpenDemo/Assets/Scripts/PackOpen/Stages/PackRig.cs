using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PackOpen.Stages
{
    /// <summary>
    /// The pack visuals: a root rect with three Images (Pack_Full, Body_Open, Strip) sharing one shader.
    /// Shared by PackAppearStage and PackTearStage. Restructure the hierarchy freely, just keep the references.
    /// Each Image gets its own material instance at runtime so tweening never edits the material asset.
    /// </summary>
    public class PackRig : MonoBehaviour
    {
        public RectTransform root;
        public Image full;
        public Image bodyOpen;
        public Image strip;
        [Tooltip("Strip pivot: 1 keeps the right end attached so the left end peels and curls.")]
        public float stripPivotX = 1f;

        public Vector3 BaseScale { get; private set; } = Vector3.one;
        public Vector3 StripHome { get; private set; }
        public Material FullMat { get; private set; }
        public Material BodyMat { get; private set; }
        public Material StripMat { get; private set; }

        static readonly string[] AnimProps =
            { "_Skew", "_SkewY", "_BendX", "_BendY", "_WaveAmpX", "_WaveAmpY", "_WavePhase" };

        void Awake()
        {
            BaseScale = root.localScale;
            StripHome = strip.rectTransform.anchoredPosition3D;
            FullMat = Instance(full);
            BodyMat = Instance(bodyOpen);
            StripMat = Instance(strip);
            if (StripMat != null) StripMat.SetFloat("_PivotX", stripPivotX);
            HideAll();
        }

        /// <summary>Swaps the three pack sprites from PackData (null entries keep the prefab's sprite).</summary>
        public void Apply(PackData pack)
        {
            if (pack == null) return;
            if (pack.packFull != null) full.sprite = pack.packFull;
            if (pack.packOpen != null) bodyOpen.sprite = pack.packOpen;
            if (pack.packStrip != null) strip.sprite = pack.packStrip;
        }

        void OnDestroy() { DOTween.Kill(this); }

        static Material Instance(Image img)
        {
            if (img.material == null || img.material == img.defaultMaterial) return null;
            var m = new Material(img.material);
            img.material = m;
            return m;
        }

        public void HideAll()
        {
            full.gameObject.SetActive(false);
            bodyOpen.gameObject.SetActive(false);
            strip.gameObject.SetActive(false);
        }

        /// <summary>Back to the untouched, full pack at the root's base scale. Kills running tweens.</summary>
        public void ResetToFull()
        {
            DOTween.Kill(this);
            root.localScale = BaseScale;
            root.localRotation = Quaternion.identity;
            full.gameObject.SetActive(true);
            bodyOpen.gameObject.SetActive(false);
            strip.gameObject.SetActive(false);
            bodyOpen.rectTransform.localScale = Vector3.one;
            ResetStrip();
            foreach (var m in new[] { FullMat, BodyMat, StripMat })
                ClearAnim(m);
            SetAlpha(StripMat, 1f);
        }

        public void ResetStrip()
        {
            var rt = strip.rectTransform;
            rt.anchoredPosition3D = StripHome;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        public static void ClearAnim(Material m)
        {
            if (m == null) return;
            foreach (var p in AnimProps) m.SetFloat(p, 0f);
        }

        public static void SetAlpha(Material m, float a)
        {
            if (m == null) return;
            var c = m.color; c.a = a; m.color = c;
        }

        // ---- tween helpers (explicit start values, so they behave the same inside any Sequence) ----

        public static Tweener Float(Material m, string prop, float from, float to, float duration) =>
            DOTween.To(v => { if (m != null) m.SetFloat(prop, v); }, from, to, duration);

        public static Tweener Alpha(Material m, float from, float to, float duration) =>
            DOTween.To(a => SetAlpha(m, a), from, to, duration);

        /// <summary>Waits for a tween, finishing it instantly if the player skips.</summary>
        public static IEnumerator Await(Tween t, PackOpenContext ctx)
        {
            while (t.IsActive() && !t.IsComplete())
            {
                if (ctx.Skip) { t.Complete(); yield break; }
                yield return null;
            }
        }
    }
}
