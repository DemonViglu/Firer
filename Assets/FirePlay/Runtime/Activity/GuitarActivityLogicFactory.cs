using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>Scene/bootstrap registration for the independent guitar activity.</summary>
    [DisallowMultipleComponent]
    public sealed class GuitarActivityLogicFactory : MonoBehaviour, IActivityLogicFactory
    {
        public IActivityLogic Create(ActivityDefinition definition)
        {
            return definition != null && definition.LogicKey == GuitarActivityLogic.ActivityId
                ? new GuitarActivityLogic()
                : null;
        }
    }
}
