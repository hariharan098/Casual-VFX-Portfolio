using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace PackOpen.Stages
{
    /// <summary>Pack flies from the tapped tile to the centre: tilt settles, scale overshoots, X and Y use different eases for an arc.</summary>
    public class PackAppearStage : PackOpenStageBase
    {
        [SerializeField] PackRig rig;
        [SerializeField] UiVfx idleVfx;
        [SerializeField] Vector2 idleOffset;
        [SerializeField] float duration = 0.6f;
        [SerializeField, Range(0.05f, 1f)] float startScale = 0.35f;
        [SerializeField] float startTilt = -25f;
        [SerializeField] Ease xEase = Ease.OutQuad;
        [SerializeField] Ease yEase = Ease.OutBack;
        [SerializeField] Ease scaleEase = Ease.OutBack;
        [SerializeField] Ease tiltEase = Ease.OutBack;

        public override IEnumerator Play(PackOpenContext ctx)
        {
            rig.Apply(ctx.Pack);
            rig.ResetToFull();
            var root = rig.root;
            Vector3 to = ctx.Anchors != null && ctx.Anchors.packCenter != null ? ctx.Anchors.packCenter.position : root.position;
            Vector3 from = ctx.PackOrigin != null ? ctx.PackOrigin.position : to;

            // idle sparkle rides with the pack from the start (PackTear stops it)
            if (idleVfx != null) { idleVfx.Follow(root, idleOffset); idleVfx.Play(); }

            if (ctx.Skip) { root.position = to; yield break; }

            root.position = from;
            root.localScale = rig.BaseScale * startScale;
            root.localRotation = Quaternion.Euler(0, 0, startTilt);

            var seq = DOTween.Sequence().SetTarget(rig);
            seq.Append(root.DOMoveX(to.x, duration).SetEase(xEase));
            seq.Join(root.DOMoveY(to.y, duration).SetEase(yEase));
            seq.Join(root.DOScale(rig.BaseScale, duration).SetEase(scaleEase));
            seq.Join(root.DOLocalRotate(Vector3.zero, duration).SetEase(tiltEase));
            yield return PackRig.Await(seq, ctx);
            root.position = to;
        }
    }
}
