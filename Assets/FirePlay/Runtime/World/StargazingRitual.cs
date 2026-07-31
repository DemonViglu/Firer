using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>停留时将视角平滑引向关卡指定的观星目标。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class StargazingRitual : RestLookTargetRitual
    {
        [SerializeField] private Transform _companionFrameTarget;
        public override string ActivityId => "stargazing";
        public override string ActivityDisplayName => "观星";
        public override string ActivityPresentationId => "none";
        public override string RestHint => "这里可以坐下，抬头看看星星";
        public override string ShortRestHint => " · 这里可以坐下看星星";

        protected override bool UsesStargazingCamera => true;
        protected override Transform StargazingCompanionTarget => _companionFrameTarget;
    }
}
