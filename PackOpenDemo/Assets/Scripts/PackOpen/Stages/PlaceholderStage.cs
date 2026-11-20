using System.Collections;
using UnityEngine;

namespace PackOpen.Stages
{
    /// <summary>Does nothing except wait. Replace the slot's component with your own stage.</summary>
    public class PlaceholderStage : PackOpenStageBase
    {
        [SerializeField] float duration = 0.3f;

        public override IEnumerator Play(PackOpenContext ctx)
        {
            if (!ctx.Skip) yield return new WaitForSeconds(duration);
        }
    }
}
