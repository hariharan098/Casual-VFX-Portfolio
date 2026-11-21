using System.Collections.Generic;
using System.Linq;

namespace PackOpen
{
    public static class PackRoller
    {
        public static List<CardResult> Roll(PackData pack, System.Random rng)
        {
            var results = new List<CardResult>();
            for (int i = 0; i < pack.cardCount; i++)
                results.Add(new CardResult { Card = Pick(pack, rng) });
            return results;
        }

        static CardData Pick(PackData pack, System.Random rng)
        {
            int rarity = PickRarity(pack.rarityWeights, rng);
            var candidates = pack.pool.Where(c => c.rarity == rarity).ToList();
            if (candidates.Count == 0) candidates = pack.pool;
            return candidates[rng.Next(candidates.Count)];
        }

        static int PickRarity(float[] weights, System.Random rng)
        {
            float total = 0f;
            foreach (var w in weights) total += w;
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll <= 0) return i + 1;
            }
            return weights.Length;
        }
    }
}
