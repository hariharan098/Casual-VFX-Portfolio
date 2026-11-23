using UnityEngine;
using UnityEngine.UI;

namespace PackOpen
{
    /// <summary>
    /// One card. The same prefab shows the back or the front: toggle with ShowBack / ShowFront.
    /// Front = masked Art + shared Frame + Stars + name. Back = Back image (same silhouette as the mask).
    /// Restructure the prefab freely, just keep these references wired.
    /// </summary>
    public class CardView : MonoBehaviour
    {
        public RectTransform root;
        public GameObject front;
        public GameObject back;
        public Image art;
        public Image backImage;
        public Image[] stars;
        public Text nameLabel;

        [Header("Reveal VFX (nested on prefab)")]
        [SerializeField] UiVfx burstVfx;
        [SerializeField] UiVfx revealIdleVfx;

        public CardData Data { get; private set; }
        public bool IsFront { get; private set; }

        /// <summary>Sets the art, stars and name. Leaves the card face-down.</summary>
        public void Bind(CardData card, Sprite backSprite = null)
        {
            Data = card;
            if (card != null)
            {
                if (art != null) art.sprite = card.face;
                if (nameLabel != null) nameLabel.text = card.displayName;
                for (int i = 0; i < stars.Length; i++)
                    if (stars[i] != null) stars[i].gameObject.SetActive(i < card.rarity);
            }
            if (backImage != null && backSprite != null) backImage.sprite = backSprite;
            ShowBack();
        }

        public void ShowBack()
        {
            IsFront = false;
            if (front != null) front.SetActive(false);
            if (back != null) back.SetActive(true);
            HideRevealVfx();
        }

        public void ShowFront() { IsFront = true; if (front != null) front.SetActive(true); if (back != null) back.SetActive(false); }

        /// <summary>Starts burst + idle on the same frame as the face swap (CardsFlipStage).</summary>
        public void PlayRevealVfx()
        {
            ResolveVfxRefs();
            if (burstVfx != null) burstVfx.Play();
            if (revealIdleVfx != null) revealIdleVfx.Play();
        }

        public void HideRevealVfx()
        {
            ResolveVfxRefs();
            if (burstVfx != null) burstVfx.Hide();
            if (revealIdleVfx != null) revealIdleVfx.Hide();
        }

        void ResolveVfxRefs()
        {
            if (burstVfx != null && revealIdleVfx != null) return;
            foreach (var v in GetComponentsInChildren<UiVfx>(true))
            {
                if (burstVfx == null && v.name == "VFX_Card_Burst") burstVfx = v;
                if (revealIdleVfx == null && v.name == "VFX_CardPack_Idle") revealIdleVfx = v;
            }
        }

        public void SetAlpha(float a)
        {
            var g = GetComponent<CanvasGroup>();
            if (g != null) g.alpha = a;
        }
    }
}
