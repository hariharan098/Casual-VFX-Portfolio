using UnityEngine;

namespace PackOpen
{
    [CreateAssetMenu(menuName = "PackOpen/Card", fileName = "Card_")]
    public class CardData : ScriptableObject
    {
        public string id;
        public string displayName;
        [Range(1, 3)] public int rarity = 1;
        public Sprite face;
    }
}
