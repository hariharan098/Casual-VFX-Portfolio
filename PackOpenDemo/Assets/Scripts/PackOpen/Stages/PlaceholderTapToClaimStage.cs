using System.Collections;
using UnityEngine;

namespace PackOpen.Stages
{
    /// <summary>Waits for a player tap. Your version: pulse the prompt, then wait the same way.</summary>
    public class PlaceholderTapToClaimStage : PackOpenStageBase
    {
        public override IEnumerator Play(PackOpenContext ctx)
        {
            Debug.Log("[PackOpen] Waiting for tap to claim...");
            yield return ctx.WaitForTap();
        }
    }
}
