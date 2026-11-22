using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace PackOpen.Stages
{
    /// <summary>
    /// Wobble with squash and shader skew, swap to the torn body at the tear moment, strip peels off.
    /// All timings and amounts are tunable. Shader values are in the pack's local pixels (860 wide).
    /// </summary>
    public class PackTearStage : PackOpenStageBase
    {
        [SerializeField] PackRig rig;

        [Header("VFX")]
        [SerializeField] UiVfx idleVfx;
        [SerializeField] UiVfx tearVfx;
        [Tooltip("Canvas pixels from the strip's pivot (its top centre) to where the sparkle sits.")]
        [SerializeField] Vector2 tearOffset = new Vector2(0f, -40f);
        [SerializeField] float idleStopLead = 0.3f;
        [Tooltip("Seconds after the tear moment when the tear VFX stops emitting. Independent of the strip's flight time.")]
        [SerializeField] float vfxStopTime = 1.0f;
        [SerializeField] float vfxPopTime = 0.25f;
        [SerializeField, Range(0.1f, 1f)] float vfxPopFrom = 0.6f;
        [SerializeField, Range(0.1f, 1f)] float vfxFadeFraction = 0.4f;

        [Header("Wobble (before the tear)")]
        [SerializeField] float wobbleTime = 0.9f;
        [SerializeField] float wobbleAngle = 9f;
        [SerializeField, Range(0f, 0.3f)] float squash = 0.07f;
        [SerializeField] float skew = 36f;

        [Header("Tear (swap moment)")]
        [SerializeField] float bodyKickSkew = 28f;
        [SerializeField] float bodySettleTime = 0.65f;
        [SerializeField] float bodyPunch = 0.06f;
        public UnityEvent onTear;

        [Header("Strip flight: phase A, lateral rip along the tear line")]
        [SerializeField] float ripTime = 0.6f;
        [Tooltip("Share of the final X distance covered during the rip.")]
        [SerializeField, Range(0.2f, 0.9f)] float ripFraction = 0.65f;
        [Tooltip("Pixels the strip rises during the rip (keep small: the rip is mostly sideways).")]
        [SerializeField] float ripLift = 40f;
        [SerializeField] float ripRotation = 12f;
        [SerializeField] float ripCurl = 60f;
        [SerializeField] Ease ripXEase = Ease.InOutQuad;

        [Header("Strip flight: phase B, lift-off")]
        [SerializeField] float liftTime = 0.8f;
        [Tooltip("Final position of the strip relative to its home (x is also the end of the rip).")]
        [SerializeField] Vector2 flyOffset = new Vector2(-760f, 640f);
        [SerializeField] float flyRotation = 55f;
        [SerializeField] float curl = 170f;
        [SerializeField] float flutterAmp = 40f;
        [SerializeField] float flutterTravel = 2.2f;
        [SerializeField] Ease liftXEase = Ease.OutQuad;
        [SerializeField] Ease liftYEase = Ease.InQuad;
        [SerializeField, Range(0.1f, 1f)] float stripFadeFraction = 0.35f;

        /// <summary>Total strip flight time (rip + lift-off).</summary>
        public float FlightTime => ripTime + liftTime;

        public override IEnumerator Play(PackOpenContext ctx)
        {
            var root = rig.root;
            var s0 = rig.BaseScale;

            // Make sure we start from a clean full pack (also if this stage is run on its own).
            if (!rig.full.gameObject.activeSelf && !rig.bodyOpen.gameObject.activeSelf) rig.ResetToFull();
            rig.ResetStrip();
            PackRig.ClearAnim(rig.FullMat);
            root.localScale = s0;

            if (ctx.Skip)
            {
                Swap();
                rig.strip.gameObject.SetActive(false);
                if (idleVfx != null) idleVfx.Hide();
                if (tearVfx != null) tearVfx.Hide();
                yield break;
            }

            float T = wobbleTime;
            var seq = DOTween.Sequence().SetTarget(rig);

            // 1. wobble: rotation punch + squash/stretch steps + shader skew pulses
            seq.Insert(0f, root.DOPunchRotation(new Vector3(0, 0, wobbleAngle), T, 8, 0.6f));
            Vector3 wide = Vector3.Scale(s0, new Vector3(1f + squash, 1f - squash, 1f));
            Vector3 tall = Vector3.Scale(s0, new Vector3(1f - squash * 0.7f, 1f + squash * 0.7f, 1f));
            Vector3 wide2 = Vector3.Scale(s0, new Vector3(1f + squash * 0.5f, 1f - squash * 0.5f, 1f));
            seq.Insert(0f, root.DOScale(wide, T * 0.20f).SetEase(Ease.OutQuad));
            seq.Insert(T * 0.20f, root.DOScale(tall, T * 0.25f).SetEase(Ease.InOutQuad));
            seq.Insert(T * 0.45f, root.DOScale(wide2, T * 0.20f).SetEase(Ease.InOutQuad));
            seq.Insert(T * 0.65f, root.DOScale(s0, T * 0.35f).SetEase(Ease.OutBack));

            seq.Insert(0f, PackRig.Float(rig.FullMat, "_Skew", 0f, skew, T * 0.22f).SetEase(Ease.OutQuad));
            seq.Insert(T * 0.22f, PackRig.Float(rig.FullMat, "_Skew", skew, -skew * 0.75f, T * 0.28f).SetEase(Ease.InOutSine));
            seq.Insert(T * 0.50f, PackRig.Float(rig.FullMat, "_Skew", -skew * 0.75f, skew * 0.4f, T * 0.25f).SetEase(Ease.InOutSine));
            seq.Insert(T * 0.75f, PackRig.Float(rig.FullMat, "_Skew", skew * 0.4f, 0f, T * 0.25f).SetEase(Ease.InOutSine));

            // 2. tear: swap sprites, fire the VFX hook
            seq.InsertCallback(T, () => { Swap(); onTear?.Invoke(); });

            // idle sparkle winds down just before the tear; the tear sparkle rides the strip
            if (idleVfx != null) seq.InsertCallback(Mathf.Max(0f, T - idleStopLead), () => idleVfx.Stop());
            if (tearVfx != null)
            {
                seq.InsertCallback(T, () => { tearVfx.Follow(rig.strip.transform, tearOffset); tearVfx.Play(); });
                seq.Insert(T, DOTween.To(k => tearVfx.SetPop(k), vfxPopFrom, 1f, vfxPopTime).SetEase(Ease.OutBack));
                float fade = vfxStopTime * vfxFadeFraction;
                seq.Insert(T + vfxStopTime - fade, DOTween.To(a => tearVfx.SetAlpha(a), 1f, 0f, fade).SetEase(Ease.InQuad));
                seq.InsertCallback(T + vfxStopTime, () => tearVfx.Stop());
            }

            // body jolts then settles
            seq.Insert(T, PackRig.Float(rig.BodyMat, "_Skew", bodyKickSkew, 0f, bodySettleTime).SetEase(Ease.OutElastic));
            seq.Insert(T, rig.bodyOpen.rectTransform.DOPunchScale(Vector3.one * bodyPunch, bodySettleTime, 6, 0.6f));

            // 3. strip: phase A rips sideways along the tear line, phase B lifts off
            var rt = rig.strip.rectTransform;
            Vector3 home = rig.StripHome;
            Vector3 away = home + new Vector3(flyOffset.x, flyOffset.y, 0f);
            float ripX = home.x + (away.x - home.x) * ripFraction;
            float ripY = home.y + ripLift;
            float tB = T + ripTime;                 // lift-off start
            float end = T + FlightTime;

            seq.Insert(T, StripX(rt, home.x, ripX, ripTime, ripXEase));
            seq.Insert(T, StripY(rt, home.y, ripY, ripTime, Ease.OutSine));
            seq.Insert(tB, StripX(rt, ripX, away.x, liftTime, liftXEase));
            seq.Insert(tB, StripY(rt, ripY, away.y, liftTime, liftYEase));

            seq.Insert(T, StripRot(rt, 0f, ripRotation, ripTime, Ease.InOutSine));
            seq.Insert(tB, StripRot(rt, ripRotation, flyRotation, liftTime, Ease.OutQuad));

            seq.Insert(T, PackRig.Float(rig.StripMat, "_BendY", 0f, ripCurl, ripTime).SetEase(Ease.OutQuad));
            seq.Insert(tB, PackRig.Float(rig.StripMat, "_BendY", ripCurl, curl, liftTime * 0.7f).SetEase(Ease.OutQuad));
            seq.Insert(T, PackRig.Float(rig.StripMat, "_WaveAmpY", 0f, flutterAmp * 0.4f, ripTime).SetEase(Ease.OutQuad));
            seq.Insert(tB, PackRig.Float(rig.StripMat, "_WaveAmpY", flutterAmp * 0.4f, flutterAmp, liftTime * 0.5f).SetEase(Ease.OutQuad));
            seq.Insert(T, PackRig.Float(rig.StripMat, "_WavePhase", 0f, flutterTravel, FlightTime).SetEase(Ease.Linear));

            float stripFade = liftTime * stripFadeFraction;
            seq.Insert(end - stripFade, PackRig.Alpha(rig.StripMat, 1f, 0f, stripFade).SetEase(Ease.InQuad));
            seq.InsertCallback(end, () => rig.strip.gameObject.SetActive(false));

            yield return PackRig.Await(seq, ctx);
        }

        static Tweener StripX(RectTransform rt, float from, float to, float dur, Ease ease) =>
            DOTween.To(k => { var p = rt.anchoredPosition3D; p.x = Mathf.LerpUnclamped(from, to, k); rt.anchoredPosition3D = p; }, 0f, 1f, dur).SetEase(ease);

        static Tweener StripY(RectTransform rt, float from, float to, float dur, Ease ease) =>
            DOTween.To(k => { var p = rt.anchoredPosition3D; p.y = Mathf.LerpUnclamped(from, to, k); rt.anchoredPosition3D = p; }, 0f, 1f, dur).SetEase(ease);

        static Tweener StripRot(RectTransform rt, float from, float to, float dur, Ease ease) =>
            DOTween.To(a => rt.localRotation = Quaternion.Euler(0, 0, a), from, to, dur).SetEase(ease);

        void Swap()
        {
            rig.full.gameObject.SetActive(false);
            rig.bodyOpen.gameObject.SetActive(true);
            rig.strip.gameObject.SetActive(true);
            PackRig.ClearAnim(rig.BodyMat);
            PackRig.SetAlpha(rig.StripMat, 1f);
            rig.ResetStrip();
        }
    }
}
