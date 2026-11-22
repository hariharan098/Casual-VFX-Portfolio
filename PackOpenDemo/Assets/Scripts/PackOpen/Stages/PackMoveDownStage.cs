using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace PackOpen.Stages
{
    /// <summary>The torn pack gives a small hop up, then drops to the PackExit anchor (cards come out of it next).</summary>
    public class PackMoveDownStage : PackOpenStageBase
    {
        [SerializeField] PackRig rig;
        [SerializeField] float hopHeight = 40f;
        [SerializeField] float hopTime = 0.12f;
        [SerializeField] float dropTime = 0.45f;
        [SerializeField] Ease dropEase = Ease.InOutCubic;
        [SerializeField, Range(0f, 0.2f)] float dropStretch = 0.06f;

        public override IEnumerator Play(PackOpenContext ctx)
        {
            var root = rig.root;
            var parent = root.parent;
            float exitY = ctx.Anchors != null && ctx.Anchors.packExit != null
                ? parent.InverseTransformPoint(ctx.Anchors.packExit.position).y
                : root.localPosition.y - 600f;
            float exitX = ctx.Anchors != null && ctx.Anchors.packExit != null
                ? parent.InverseTransformPoint(ctx.Anchors.packExit.position).x
                : root.localPosition.x;

            Vector3 start = root.localPosition;
            Vector3 end = new Vector3(exitX, exitY, start.z);
            if (ctx.Skip) { root.localPosition = end; yield break; }

            Vector3 s0 = rig.BaseScale;
            var seq = DOTween.Sequence().SetTarget(rig);
            // hop
            seq.Insert(0f, DOTween.To(k => { var p = root.localPosition; p.y = Mathf.LerpUnclamped(start.y, start.y + hopHeight, k); root.localPosition = p; }, 0f, 1f, hopTime).SetEase(Ease.OutQuad));
            // drop
            float t = hopTime;
            seq.Insert(t, DOTween.To(k => { var p = root.localPosition; p.y = Mathf.LerpUnclamped(start.y + hopHeight, end.y, k); root.localPosition = p; }, 0f, 1f, dropTime).SetEase(dropEase));
            seq.Insert(t, DOTween.To(k => { var p = root.localPosition; p.x = Mathf.LerpUnclamped(start.x, end.x, k); root.localPosition = p; }, 0f, 1f, dropTime).SetEase(Ease.OutQuad));
            // slight stretch while dropping, settles back
            Vector3 tall = Vector3.Scale(s0, new Vector3(1f - dropStretch * 0.6f, 1f + dropStretch, 1f));
            seq.Insert(t, CardsRig.Scale(root, s0, tall, dropTime * 0.5f).SetEase(Ease.OutQuad));
            seq.Insert(t + dropTime * 0.5f, CardsRig.Scale(root, tall, s0, dropTime * 0.5f).SetEase(Ease.OutBack));
            yield return PackRig.Await(seq, ctx);
            root.localPosition = end;
            root.localScale = s0;
        }
    }
}
