using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace PackOpen.Stages
{
    /// <summary>
    /// Modelled on the reference (frames 59-67): the pack swings in from the side of the screen, tilted and
    /// straightening, overshoots the centre a little, then lands with a hop and a squash-and-stretch bounce.
    /// </summary>
    public class PackAppearStage : PackOpenStageBase
    {
        [SerializeField] PackRig rig;
        [SerializeField] UiVfx idleVfx;
        [SerializeField] Vector2 idleOffset;

        [Header("Entry")]
        [Tooltip("On: start at 'sideOffset' from the centre. Off: start at the tapped tile / PackOrigin (old behaviour).")]
        [SerializeField] bool enterFromSide = true;
        [Tooltip("Canvas pixels from PackCenter where the pack starts (right of and a bit below the centre).")]
        [SerializeField] Vector2 sideOffset = new Vector2(700f, -150f);
        [SerializeField] float flyTime = 0.45f;
        [SerializeField, Range(0.05f, 1f)] float startScale = 0.55f;
        [Tooltip("Degrees at the start (negative = clockwise). It unwinds to 0 with a small overshoot.")]
        [SerializeField] float startTilt = -50f;
        [SerializeField] Ease xEase = Ease.OutCubic;
        [SerializeField] Ease yEase = Ease.OutBack;
        [SerializeField] Ease scaleEase = Ease.OutBack;
        [SerializeField] Ease tiltEase = Ease.OutBack;

        [Header("Landing bounce")]
        [SerializeField] float bounceTime = 0.4f;
        [Tooltip("Canvas pixels the pack hops up after landing.")]
        [SerializeField] float bounceHop = 28f;
        [SerializeField, Range(0f, 0.3f)] float bounceSquash = 0.08f;

        // 0 = scene made before the reference-based preset; PackRigSetup applies the preset once and sets this to 2.
        [SerializeField, HideInInspector] int tuningVersion = 0;

        public override IEnumerator Play(PackOpenContext ctx)
        {
            rig.Apply(ctx.Pack);
            rig.ResetToFull();
            var root = rig.root;
            var parent = root.parent;
            Vector3 s0 = rig.BaseScale;
            Vector3 to = ctx.Anchors != null && ctx.Anchors.packCenter != null ? ctx.Anchors.packCenter.position : root.position;
            Vector3 from;
            if (enterFromSide) from = to + parent.TransformVector(new Vector3(sideOffset.x, sideOffset.y, 0f));
            else from = ctx.PackOrigin != null ? ctx.PackOrigin.position : to;

            // idle sparkle rides with the pack from the start (PackTear stops it)
            if (idleVfx != null) { idleVfx.Follow(root, idleOffset); idleVfx.Play(); }

            if (ctx.Skip) { root.position = to; yield break; }

            root.position = from;
            root.localScale = s0 * startScale;
            root.localRotation = Quaternion.Euler(0, 0, startTilt);

            var seq = DOTween.Sequence().SetTarget(rig);

            // 1. entry: X and Y with different eases for an arc, rotation unwinds, scale grows
            CardsRig.AddMove(seq, 0f, root, from, to, flyTime, xEase, yEase);
            seq.Insert(0f, CardsRig.Scale(root, s0 * startScale, s0, flyTime).SetEase(scaleEase));
            seq.Insert(0f, CardsRig.RotZ(root, startTilt, 0f, flyTime).SetEase(tiltEase));

            // 2. landing bounce: squash on impact, hop with a stretch, fall back with a small squash, settle
            float t0 = flyTime;
            float B = bounceTime;
            float hop = parent.TransformVector(new Vector3(0f, bounceHop, 0f)).y;
            Vector3 squashed = Vector3.Scale(s0, new Vector3(1f + bounceSquash, 1f - bounceSquash, 1f));
            Vector3 stretched = Vector3.Scale(s0, new Vector3(1f - bounceSquash * 0.4f, 1f + bounceSquash * 0.5f, 1f));
            Vector3 small = Vector3.Scale(s0, new Vector3(1f + bounceSquash * 0.4f, 1f - bounceSquash * 0.4f, 1f));
            seq.Insert(t0, CardsRig.Scale(root, s0, squashed, B * 0.2f).SetEase(Ease.OutQuad));
            seq.Insert(t0 + B * 0.2f, CardsRig.Scale(root, squashed, stretched, B * 0.3f).SetEase(Ease.OutQuad));
            seq.Insert(t0 + B * 0.5f, CardsRig.Scale(root, stretched, small, B * 0.3f).SetEase(Ease.InQuad));
            seq.Insert(t0 + B * 0.8f, CardsRig.Scale(root, small, s0, B * 0.2f).SetEase(Ease.OutBack));
            seq.Insert(t0 + B * 0.2f, DOTween.To(k => { var p = root.position; p.y = Mathf.LerpUnclamped(to.y, to.y + hop, k); root.position = p; }, 0f, 1f, B * 0.3f).SetEase(Ease.OutQuad));
            seq.Insert(t0 + B * 0.5f, DOTween.To(k => { var p = root.position; p.y = Mathf.LerpUnclamped(to.y + hop, to.y, k); root.position = p; }, 0f, 1f, B * 0.3f).SetEase(Ease.InQuad));

            yield return PackRig.Await(seq, ctx);
            root.position = to;
            root.localScale = s0;
            root.localRotation = Quaternion.identity;
        }
    }
}
