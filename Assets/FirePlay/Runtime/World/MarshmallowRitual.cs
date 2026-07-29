using DemonViglu.FirePlay.Flame;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>烤棉花停留仪式；消耗少量余火拟造棉花糖，手持表现由 Player 管理。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class MarshmallowRitual : RestLookTargetRitual
    {
        [SerializeField, Min(0f)] private float _materializeFuelCost = 3f;

        public float MaterializeFuelCost => _materializeFuelCost;

        protected override bool RotatePlayerTowardsTarget => true;
        protected override bool UsesExternalCamera => true;

        public bool TryMaterialize(FlameResourceController resourceController, out string status)
        {
            if (!TryGetComponent<Campfire>(out var campfire) || campfire.IsExtinguished)
            {
                status = "The fire is out";
                return false;
            }

            if (resourceController == null || !resourceController.TryConsume(_materializeFuelCost))
            {
                status = "Not enough fuel";
                return false;
            }

            status = "A marshmallow takes shape";
            return true;
        }

        private void OnValidate()
        {
            _materializeFuelCost = Mathf.Max(0f, _materializeFuelCost);
        }
    }
}
