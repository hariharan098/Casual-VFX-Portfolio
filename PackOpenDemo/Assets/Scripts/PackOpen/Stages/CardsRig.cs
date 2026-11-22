using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace PackOpen.Stages
{
    /// <summary>
    /// Spawns and owns the Card prefab instances for one run. Shared by the card stages.
    /// Cards are parented under `container` (a RectTransform on the Canvas, above the pack in sibling order).
    /// </summary>
    public class CardsRig : MonoBehaviour
    {
        public CardView cardPrefab;
        public RectTransform container;

        public readonly List<CardView> Cards = new List<CardView>();

        /// <summary>Destroys old cards and spawns one face-down card per result, hidden, at the container origin.</summary>
        public void Spawn(PackOpenContext ctx)
        {
            Clear();
            for (int i = 0; i < ctx.Results.Count; i++)
            {
                var c = Instantiate(cardPrefab, container);
                c.name = "Card_" + i;
                c.Bind(ctx.Results[i].Card, ctx.Pack != null ? ctx.Pack.cardBack : null);
                c.gameObject.SetActive(false);
                Cards.Add(c);
            }
        }

        public void Clear()
        {
            foreach (var c in Cards)
            {
                if (c == null) continue;
                DOTween.Kill(c);
                DOTween.Kill(c.root);
                Destroy(c.gameObject);
            }
            Cards.Clear();
        }

        void OnDestroy() { Clear(); }

        // ---- tween helpers (explicit start values, safe inside Sequences) ----

        /// <summary>World-space move with separate X / Y eases, which gives a curved path.</summary>
        public static void AddMove(Sequence seq, float at, Transform t, Vector3 from, Vector3 to, float dur, Ease xEase, Ease yEase)
        {
            seq.Insert(at, DOTween.To(k => { var p = t.position; p.x = Mathf.LerpUnclamped(from.x, to.x, k); t.position = p; }, 0f, 1f, dur).SetEase(xEase));
            seq.Insert(at, DOTween.To(k => { var p = t.position; p.y = Mathf.LerpUnclamped(from.y, to.y, k); t.position = p; }, 0f, 1f, dur).SetEase(yEase));
        }

        public static Tweener Scale(Transform t, Vector3 from, Vector3 to, float dur) =>
            DOTween.To(k => t.localScale = Vector3.LerpUnclamped(from, to, k), 0f, 1f, dur);

        public static Tweener ScaleX(Transform t, float from, float to, float dur) =>
            DOTween.To(x => { var s = t.localScale; s.x = x; t.localScale = s; }, from, to, dur);

        public static Tweener ScaleY(Transform t, float from, float to, float dur) =>
            DOTween.To(y => { var s = t.localScale; s.y = y; t.localScale = s; }, from, to, dur);

        public static Tweener RotZ(Transform t, float from, float to, float dur) =>
            DOTween.To(a => t.localRotation = Quaternion.Euler(0f, 0f, a), from, to, dur);

        public static Tweener Fade(CardView c, float from, float to, float dur) =>
            DOTween.To(a => c.SetAlpha(a), from, to, dur);
    }
}
