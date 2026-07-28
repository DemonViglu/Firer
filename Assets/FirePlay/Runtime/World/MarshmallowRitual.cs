using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>烤棉花停留仪式；手持表现由 Player 管理，视角由 Look Target 引导至火焰。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class MarshmallowRitual : RestLookTargetRitual
    {
        protected override bool RotatePlayerTowardsTarget => true;
        protected override bool UsesExternalCamera => true;
    }
}
