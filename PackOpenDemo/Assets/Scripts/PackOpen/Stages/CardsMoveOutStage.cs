using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace PackOpen.Stages
{
    /// <summary>
    /// Spawns the face-down cards at the pack opening and deals them to the card slots one by one.
    /// Each card starts before the previous one lands (stagger shorter than travel time), so they overlap.
    /// The pack sinks off screen at the end.
    /// </summary>
    public class CardsMoveOutStage : PackOpenStageBase
    {
        [SerializeField] PackRig rig;
        [SerializeField] CardsRig cards;

        [Header("Deal")]
        [SerializeField] float stagger = 0.18f;
        [SerializeField] float travelTime = 0.55f;
        [SerializeField, Range(0.1f, 1f)] float startScale = 0.55f;
        [SerializeField] float startTilt = 14f;
        [Tooltip("Where inside the pack opening the cards appear, in pixels below its top edge.")]
        [SerializeField] float spawnDepth = 60f;
        [SerializeField] Ease xEase = Ease.OutCubic;
        [SerializeField] Ease yEase = Ease.OutBack;
        [SerializeField] Ease scaleEase = Ease.OutBack;

        [Tooltip("Card spacing in canvas pixels, used only when the scene has fewer CardSlot anchors than cards.")]
        [SerializeField] float fallbackSpacing = 290f;

        [Header("Glow burst (VFX_Card_Glow)")]
        [SerializeField] UiVfx glowVfx;
        [Tooltip("Canvas pixels from the card spawn point (the pack opening).")]
        [SerializeField] Vector2 glowOffset = Vector2.zero;
        [Tooltip("On: the glow restarts as every card is dealt. Off: one burst when the first card is dealt.")]
        [SerializeField] bool glowPerCard = true;
        [Tooltip("Seconds after the last burst starts before the fade begins.")]
        [SerializeField] float glowFadeDelay = 0.15f;
        [Tooltip("Seconds the glow takes to fade out (emission stops when the fade starts).")]
        [SerializeField] float glowFadeTime = 0.4f;

        [Header("Pack sink")]
        [SerializeField] float sinkDelay = 0.25f;
        [SerializeField] float sinkTime = 0.5f;
        [SerializeField] float sinkDistance = 900f;

        public override IEnumerator Play(PackOpenContext ctx)
        {
            cards.Spawn(ctx);
            Vector3 spawn = SpawnPoint();
            int n = cards.Cards.Count;
            var root = rig.root;
            Vector3 sunk = root.localPosition + Vector3.down * sinkDistance;

            if (ctx.Skip)
            {
                for (int i = 0; i < n; i++) Place(cards.Cards[i], Target(ctx, i, n));
                root.localPosition = sunk;
                yield break;
            }

            var seq = DOTween.Sequence().SetTarget(this);
            int glowLast = glowPerCard ? n - 1 : 0;
            Vector3 glowPos = spawn;
            if (glowVfx != null)
            {
                var gp = glowVfx.transform.parent;
                if (gp != null) glowPos += gp.TransformVector(new Vector3(glowOffset.x, glowOffset.y, 0f));
                glowVfx.Hide();
            }
            for (int i = 0; i < n; i++)
            {
                var card = cards.Cards[i];
                var rt = card.root;
                Vector3 to = Target(ctx, i, n);
                float at = i * stagger;
                float tilt = (i % 2 == 0 ? 1f : -1f) * startTilt;

                rt.position = spawn;
                rt.localScale = Vector3.one * startScale;
                rt.localRotation = Quaternion.Euler(0, 0, tilt);
                seq.InsertCallback(at, () => { rt.position = spawn; card.gameObject.SetActive(true); });
                if (glowVfx != null && i <= glowLast && (glowPerCard || i == 0))
                {
                    seq.InsertCallback(at, () => { glowVfx.transform.position = glowPos; glowVfx.Play(); });
                    if (i == glowLast)
                    {
                        float fadeAt = at + glowFadeDelay;
                        seq.InsertCallback(fadeAt, () => glowVfx.Stop());
                        seq.Insert(fadeAt, DOTween.To(a => glowVfx.SetAlpha(a), 1f, 0f, glowFadeTime).SetEase(Ease.InQuad));
                        seq.InsertCallback(fadeAt + glowFadeTime, () => glowVfx.Hide());
                    }
                }
                CardsRig.AddMove(seq, at, rt, spawn, to, travelTime, xEase, yEase);
                seq.Insert(at, CardsRig.Scale(rt, Vector3.one * startScale, Vector3.one, travelTime).SetEase(scaleEase));
                seq.Insert(at, CardsRig.RotZ(rt, tilt, 0f, travelTime).SetEase(Ease.OutQuad));
            }

            float sinkAt = sinkDelay;
            Vector3 sinkFrom = root.localPosition;
            seq.Insert(sinkAt, DOTween.To(k => { var p = root.localPosition; p.y = Mathf.LerpUnclamped(sinkFrom.y, sunk.y, k); root.localPosition = p; }, 0f, 1f, sinkTime).SetEase(Ease.InBack));
            yield return PackRig.Await(seq, ctx);

            // end state, in case a Skip completed things out of order
            for (int i = 0; i < n; i++) Place(cards.Cards[i], Target(ctx, i, n));
            root.localPosition = sunk;
        }

        void Place(CardView c, Vector3 pos)
        {
            c.gameObject.SetActive(true);
            c.root.position = pos;
            c.root.localScale = Vector3.one;
            c.root.localRotation = Quaternion.identity;
        }

        Vector3 SpawnPoint()
        {
            var corners = new Vector3[4];
            rig.bodyOpen.rectTransform.GetWorldCorners(corners);
            Vector3 top = (corners[1] + corners[2]) * 0.5f;
            float worldPerPx = (corners[3].x - corners[0].x) / Mathf.Max(1f, rig.bodyOpen.rectTransform.rect.width);
            return top + Vector3.down * spawnDepth * worldPerPx;
        }

        Vector3 Target(PackOpenContext ctx, int i, int n)
        {
            var slot = ctx.CardSlot(i);
            if (slot != null) return slot.position;
            // fallback if the scene has fewer slots: spread around the pack centre
            // canvas-local spacing converted to world, so it follows any camera size
            return cards.container.TransformPoint(new Vector3((i - (n - 1) * 0.5f) * fallbackSpacing, 0f, 0f));
        }
    }
}
