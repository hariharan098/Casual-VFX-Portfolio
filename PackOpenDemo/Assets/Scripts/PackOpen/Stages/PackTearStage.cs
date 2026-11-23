using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace PackOpen.Stages
{
    /// <summary>
    /// Modelled on the reference (frames 64-71): the pack leans slightly, the swap happens, the strip peels from the
    /// right corner with force building slowly (the pack is pulled the other way), then is thrown up and to the left
    /// off screen while the pack recoils to upright. All timings and amounts are tunable.
    /// Shader values are in the pack's local pixels (860 wide).
    /// </summary>
    public class PackTearStage : PackOpenStageBase
    {
        [SerializeField] PackRig rig;

        [Header("VFX")]
        [SerializeField] UiVfx idleVfx;
        [SerializeField] UiVfx tearVfx;
        [Tooltip("Canvas pixels from the strip's pivot (its top centre) to where the sparkle sits (top-right corner).")]
        [SerializeField] Vector2 tearOffset = new Vector2(180f, -30f);
        [SerializeField] float idleStopLead = 0.3f;
        [Tooltip("Seconds after the tear moment when the tear VFX stops emitting. Used only when Detach On Throw is off.")]
        [SerializeField] float vfxStopTime = 1.2f;
        [SerializeField] float vfxPopTime = 0.25f;
        [SerializeField, Range(0.1f, 1f)] float vfxPopFrom = 0.6f;
        [Tooltip("After emission stops, the remaining sparks stay visible for this long, then fade out together.")]
        [SerializeField] float vfxLingerTime = 0.35f;
        [Tooltip("The VFX follows the strip while it peels, then stays at the pack's top edge when the throw starts.")]
        [SerializeField] bool vfxDetachOnThrow = true;
        [Tooltip("Extra follow time after the throw phase starts before the VFX stops tracking the strip (only if Detach On Throw).")]
        [SerializeField] float vfxDetachDelay = 0.1f;

        [Header("Lean (before the tear)")]
        [SerializeField] float wobbleTime = 0.45f;
        [Tooltip("Degrees the pack leans (clockwise) before the tear.")]
        [SerializeField] float wobbleAngle = 4f;
        [SerializeField, Range(0f, 0.3f)] float squash = 0.02f;
        [SerializeField] float skew = 10f;

        [Header("Tear (swap moment)")]
        [SerializeField] float bodyKickSkew = 28f;
        [SerializeField] float bodySettleTime = 0.65f;
        [SerializeField] float bodyPunch = 0.06f;
        public UnityEvent onTear;

        [Header("Pack pull and recoil")]
        [Tooltip("Degrees the pack is pulled the other way (counter-clockwise) while the strip peels.")]
        [SerializeField] float pullAngle = 7f;
        [SerializeField] float recoilTime = 0.45f;

        [Header("Strip flight: phase A, slow peel from the right corner")]
        [SerializeField] float ripTime = 0.6f;
        [Tooltip("Share of the final X distance covered during the peel.")]
        [SerializeField, Range(0.2f, 0.9f)] float ripFraction = 0.2f;
        [Tooltip("Pixels the strip rises during the peel.")]
        [SerializeField] float ripLift = 30f;
        [SerializeField] float ripRotation = 25f;
        [SerializeField] float ripCurl = 110f;
        [SerializeField] Ease ripXEase = Ease.InQuad;
        [SerializeField] Ease ripYEase = Ease.InOutSine;
        [SerializeField] Ease ripRotEase = Ease.InQuad;

        [Header("Strip flight: phase B, throw off screen")]
        [SerializeField] float liftTime = 0.4f;
        [Tooltip("Final position of the strip relative to its home. Keep it beyond the screen edge.")]
        [SerializeField] Vector2 flyOffset = new Vector2(-820f, 1000f);
        [SerializeField] float flyRotation = 75f;
        [SerializeField] float curl = 180f;
        [SerializeField] float flutterAmp = 40f;
        [SerializeField] float flutterTravel = 2.2f;
        [SerializeField] Ease liftXEase = Ease.InQuad;
        [SerializeField] Ease liftYEase = Ease.InCubic;
        [SerializeField, Range(0.05f, 1f)] float stripFadeFraction = 0.15f;

        // 0 = scene made before the reference-based preset; PackRigSetup applies the preset once and sets this to 2.
        [SerializeField, HideInInspector] int tuningVersion = 0;

        /// <summary>Total strip flight time (peel + throw).</summary>
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
                root.localRotation = Quaternion.identity;
                if (idleVfx != null) idleVfx.Hide();
                if (tearVfx != null) tearVfx.Hide();
                yield break;
            }

            float T = wobbleTime;
            float tB = T + ripTime;                 // throw start
            float end = T + FlightTime;
            var seq = DOTween.Sequence().SetTarget(rig);

            // 1. lean: the pack tips slightly, a hint of squash and skew for tension
            root.localRotation = Quaternion.identity;
            seq.Insert(0f, CardsRig.RotZ(root, 0f, -wobbleAngle, T).SetEase(Ease.InOutSine));
            Vector3 squashed = Vector3.Scale(s0, new Vector3(1f + squash, 1f - squash, 1f));
            seq.Insert(0f, CardsRig.Scale(root, s0, squashed, T * 0.6f).SetEase(Ease.InOutSine));
            seq.Insert(T * 0.6f, CardsRig.Scale(root, squashed, s0, T * 0.4f).SetEase(Ease.OutQuad));
            seq.Insert(0f, PackRig.Float(rig.FullMat, "_Skew", 0f, skew, T * 0.7f).SetEase(Ease.InOutSine));
            seq.Insert(T * 0.7f, PackRig.Float(rig.FullMat, "_Skew", skew, 0f, T * 0.3f).SetEase(Ease.InOutSine));

            // 2. tear: swap sprites, fire the VFX hook
            seq.InsertCallback(T, () => { Swap(); onTear?.Invoke(); });

            // idle sparkle winds down just before the tear; the tear sparkle starts at the corner and rides the strip
            if (idleVfx != null) seq.InsertCallback(Mathf.Max(0f, T - idleStopLead), () => idleVfx.Stop());
            if (tearVfx != null)
            {
                seq.InsertCallback(T, () => { tearVfx.Follow(rig.strip.transform, tearOffset); tearVfx.Play(); });
                seq.Insert(T, DOTween.To(k => tearVfx.SetPop(k), vfxPopFrom, 1f, vfxPopTime).SetEase(Ease.OutBack));
                if (vfxDetachOnThrow)
                {
                    // Follow the strip through peel + delay, then stop emitting so sparks do not pile up at a frozen point.
                    float detachAt = tB + vfxDetachDelay;
                    seq.InsertCallback(detachAt, () =>
                    {
                        tearVfx.Follow(null, Vector2.zero);
                        tearVfx.Stop();
                    });
                    seq.Insert(detachAt, DOTween.To(a => tearVfx.SetAlpha(a), 1f, 0f, vfxLingerTime).SetEase(Ease.InQuad));
                    seq.InsertCallback(detachAt + vfxLingerTime, () => tearVfx.Hide());
                }
                else
                {
                    seq.InsertCallback(T + vfxStopTime, () => tearVfx.Stop());
                    seq.Insert(T + vfxStopTime, DOTween.To(a => tearVfx.SetAlpha(a), 1f, 0f, vfxLingerTime).SetEase(Ease.InQuad));
                    seq.InsertCallback(T + vfxStopTime + vfxLingerTime, () => tearVfx.Hide());
                }
            }

            // pack is pulled the other way while the strip peels, then recoils to upright as the strip is thrown
            seq.Insert(T, CardsRig.RotZ(root, -wobbleAngle, pullAngle, ripTime).SetEase(Ease.InOutSine));
            seq.Insert(tB, CardsRig.RotZ(root, pullAngle, 0f, recoilTime).SetEase(Ease.OutBack));

            // body jolts then settles
            seq.Insert(T, PackRig.Float(rig.BodyMat, "_Skew", bodyKickSkew, 0f, bodySettleTime).SetEase(Ease.OutElastic));
            seq.Insert(T, rig.bodyOpen.rectTransform.DOPunchScale(Vector3.one * bodyPunch, bodySettleTime, 6, 0.6f));

            // 3. strip: phase A peels slowly, force building; phase B throws it up and to the left, off screen
            var rt = rig.strip.rectTransform;
            Vector3 home = rig.StripHome;
            Vector3 away = home + new Vector3(flyOffset.x, flyOffset.y, 0f);
            float ripX = home.x + (away.x - home.x) * ripFraction;
            float ripY = home.y + ripLift;

            seq.Insert(T, StripX(rt, home.x, ripX, ripTime, ripXEase));
            seq.Insert(T, StripY(rt, home.y, ripY, ripTime, ripYEase));
            seq.Insert(tB, StripX(rt, ripX, away.x, liftTime, liftXEase));
            seq.Insert(tB, StripY(rt, ripY, away.y, liftTime, liftYEase));

            seq.Insert(T, StripRot(rt, 0f, ripRotation, ripTime, ripRotEase));
            seq.Insert(tB, StripRot(rt, ripRotation, flyRotation, liftTime, Ease.InQuad));

            seq.Insert(T, PackRig.Float(rig.StripMat, "_BendY", 0f, ripCurl, ripTime).SetEase(Ease.InQuad));
            seq.Insert(tB, PackRig.Float(rig.StripMat, "_BendY", ripCurl, curl, liftTime * 0.7f).SetEase(Ease.OutQuad));
            seq.Insert(T, PackRig.Float(rig.StripMat, "_WaveAmpY", 0f, flutterAmp * 0.4f, ripTime).SetEase(Ease.InQuad));
            seq.Insert(tB, PackRig.Float(rig.StripMat, "_WaveAmpY", flutterAmp * 0.4f, flutterAmp, liftTime * 0.5f).SetEase(Ease.OutQuad));
            seq.Insert(T, PackRig.Float(rig.StripMat, "_WavePhase", 0f, flutterTravel, FlightTime).SetEase(Ease.Linear));

            float stripFade = liftTime * stripFadeFraction;
            seq.Insert(end - stripFade, PackRig.Alpha(rig.StripMat, 1f, 0f, stripFade).SetEase(Ease.InQuad));
            seq.InsertCallback(end, () => rig.strip.gameObject.SetActive(false));

            yield return PackRig.Await(seq, ctx);
            root.localRotation = Quaternion.identity;
            root.localScale = s0;
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
