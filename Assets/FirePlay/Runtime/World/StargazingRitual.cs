using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>停留时将视角平滑引向关卡指定的观星目标。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class StargazingRitual : RestLookTargetRitual
    {
        [SerializeField] private Transform _companionFrameTarget;

        protected override bool UsesStargazingCamera => true;
        protected override Transform StargazingCompanionTarget => _companionFrameTarget;
    }
}
