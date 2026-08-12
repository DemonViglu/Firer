using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Rendering
{
    [DisallowMultipleComponent]
    public sealed class WarmthIceReceiver : EnvironmentWarmthReceiverBehaviour
    {
        [SerializeField] private IcePathCrackField[] _iceFields;
        [SerializeField, Range(0f, 1f)] private float _iceWarmthMultiplier = 0.42f;
        [SerializeField, Range(0.1f, 1f)] private float _radiusMultiplier = 0.58f;

        public override void ApplyEnvironmentWarmth(EnvironmentWarmthField field, float deltaTime)
        {
            if (_iceFields == null)
            {
                return;
            }

            for (var sourceIndex = 0; sourceIndex < field.Count; sourceIndex++)
            {
                field.GetSource(sourceIndex, out var position, out var radius, out var strength);
                foreach (var iceField in _iceFields)
                {
                    iceField?.ApplyWarmth(
                        position,
                        strength * _iceWarmthMultiplier,
                        radius * _radiusMultiplier,
                        deltaTime);
                }
            }
        }
    }
}
