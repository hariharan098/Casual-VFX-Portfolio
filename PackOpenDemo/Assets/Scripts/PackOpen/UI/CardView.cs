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

        /// <summary>Nested Canvas sort keys (same on every card so all back VFX draw under all card art).</summary>
        public const int SortBackVfx = 0;
        public const int SortCardArt = 10;
        public const int SortFrontVfx = 20;

        void Awake() => EnforceCanvasLayers();

        /// <summary>
        /// UI "layers" via nested Canvas.overrideSorting (not a second camera): every card uses the same
        /// three sorting orders so burst/idle-back paint below every card's Front/Back, and VFX_Front above art.
        /// RectMask2D does not reliably clip ZParticleSystem meshes, so sorting is the fix.
        /// </summary>
        public void EnforceCanvasLayers()
        {
            var rt = root != null ? root : (RectTransform)transform;
            var vfxBack = rt.Find("VFX_Back");
            var vfxFront = rt.Find("VFX_Front");
            if (vfxBack != null) ApplySortLayer(vfxBack, SortBackVfx);
            if (front != null) ApplySortLayer(front.transform, SortCardArt);
            if (back != null) ApplySortLayer(back.transform, SortCardArt);
            if (vfxFront != null) ApplySortLayer(vfxFront, SortFrontVfx);
        }

        static void ApplySortLayer(Transform t, int order)
        {
            var c = t.GetComponent<Canvas>();
            if (c == null) c = t.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = order;
            // Sub-canvases must not steal input or spawn extra raycasters.
            var ray = t.GetComponent<GraphicRaycaster>();
            if (ray != null) Object.Destroy(ray);
        }

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
            EnforceCanvasLayers();
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
            EnforceCanvasLayers();
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
