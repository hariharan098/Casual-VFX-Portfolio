using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace PackOpen.Stages
{
    /// <summary>Cards fly to the wallet one by one: small pull-back, curved path, shrink, fade. Wallet punches on each arrival.</summary>
    public class FlyToWalletStage : PackOpenStageBase
    {
        [SerializeField] CardsRig cards;
        [SerializeField] float stagger = 0.2f;
        [SerializeField] float flyTime = 0.55f;
        [SerializeField, Range(0.05f, 1f)] float endScale = 0.2f;
        [SerializeField] float spin = 18f;
        [SerializeField] Ease xEase = Ease.InOutSine;
        [SerializeField] Ease yEase = Ease.InBack;
        [SerializeField] float walletPunch = 0.12f;
        public UnityEngine.Events.UnityEvent onCardArrived;

        public override IEnumerator Play(PackOpenContext ctx)
        {
            var wallet = ctx.Anchors != null ? ctx.Anchors.walletTarget : null;
            if (ctx.Skip || wallet == null) { HideAll(); yield break; }

            var seq = DOTween.Sequence().SetTarget(this);
            int n = cards.Cards.Count;
            for (int i = 0; i < n; i++)
            {
                var card = cards.Cards[i];
                var rt = card.root;
                float at = i * stagger;
                Vector3 from = rt.position;
                Vector3 to = wallet.position;
                float dir = i % 2 == 0 ? 1f : -1f;

                CardsRig.AddMove(seq, at, rt, from, to, flyTime, xEase, yEase);
                seq.Insert(at, CardsRig.Scale(rt, Vector3.one, Vector3.one * endScale, flyTime).SetEase(Ease.InQuad));
                seq.Insert(at, CardsRig.RotZ(rt, 0f, spin * dir, flyTime).SetEase(Ease.InQuad));
                seq.Insert(at + flyTime * 0.7f, CardsRig.Fade(card, 1f, 0f, flyTime * 0.3f));
                seq.InsertCallback(at + flyTime, () =>
                {
                    card.gameObject.SetActive(false);
                    wallet.DOComplete();
                    wallet.DOPunchScale(Vector3.one * walletPunch, 0.25f, 5, 0.6f);
                    onCardArrived?.Invoke();
                });
            }
            yield return PackRig.Await(seq, ctx);
            HideAll();
            wallet.DOComplete();
        }

        void HideAll()
        {
            foreach (var c in cards.Cards) { c.gameObject.SetActive(false); c.SetAlpha(1f); }
        }
    }
}
