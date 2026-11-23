using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace PackOpen.Stages
{
    /// <summary>Each card flips (scaleX to 0, swap back for front, scaleX back) with a small pop. Flips overlap by a short offset.</summary>
    public class CardsFlipStage : PackOpenStageBase
    {
        [SerializeField] CardsRig cards;
        [SerializeField] float stagger = 0.12f;
        [SerializeField] float halfFlipTime = 0.16f;
        [SerializeField] float popScale = 1.12f;
        [SerializeField] Ease closeEase = Ease.InQuad;
        [SerializeField] Ease openEase = Ease.OutBack;
        public UnityEngine.Events.UnityEvent onCardRevealed;

        public override IEnumerator Play(PackOpenContext ctx)
        {
            if (ctx.Skip) { Finish(revealVfx: true); yield break; }

            var seq = DOTween.Sequence().SetTarget(this);
            for (int i = 0; i < cards.Cards.Count; i++)
            {
                var card = cards.Cards[i];
                var rt = card.root;
                float at = i * stagger;
                seq.Insert(at, CardsRig.ScaleX(rt, 1f, 0f, halfFlipTime).SetEase(closeEase));
                seq.InsertCallback(at + halfFlipTime, () =>
                {
                    card.ShowFront();
                    card.PlayRevealVfx();
                    onCardRevealed?.Invoke();
                });
                seq.Insert(at + halfFlipTime, CardsRig.ScaleX(rt, 0f, 1f, halfFlipTime * 1.4f).SetEase(openEase));
                // pop on Y so the card feels like it lifts
                seq.Insert(at, CardsRig.ScaleY(rt, 1f, popScale, halfFlipTime).SetEase(Ease.OutQuad));
                seq.Insert(at + halfFlipTime, CardsRig.ScaleY(rt, popScale, 1f, halfFlipTime * 1.4f).SetEase(Ease.OutBack));
            }
            yield return PackRig.Await(seq, ctx);
            Finish();
        }

        void Finish(bool revealVfx = false)
        {
            foreach (var c in cards.Cards)
            {
                c.ShowFront();
                c.root.localScale = Vector3.one;
                if (revealVfx) c.PlayRevealVfx();
            }
        }
    }
}
