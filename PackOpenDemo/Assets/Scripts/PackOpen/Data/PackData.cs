using System.Collections.Generic;
using UnityEngine;

namespace PackOpen
{
    public enum PackVariant { Burst, TearDrop }

    [CreateAssetMenu(menuName = "PackOpen/Pack", fileName = "Pack_")]
    public class PackData : ScriptableObject
    {
        public string id;
        public string displayName;
        public int cardCount = 2;
        public PackVariant openVariant = PackVariant.Burst;
        [Tooltip("Relative weight for rarity 1, 2, 3")]
        public float[] rarityWeights = { 70f, 25f, 5f };
        public Sprite wrapper;
        public Sprite tearStrip;
        public Sprite cardBack;
        public List<CardData> pool = new List<CardData>();
    }
}
