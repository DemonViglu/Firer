using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>Scene registration for the sitting-based stargazing activity.</summary>
    [DisallowMultipleComponent]
    public sealed class StargazingActivityLogicFactory : MonoBehaviour, IActivityLogicFactory
    {
        public IActivityLogic Create(ActivityDefinition definition)
        {
            return definition != null && definition.LogicKey == StargazingActivityLogic.ActivityId
                ? new StargazingActivityLogic()
                : null;
        }
    }
}
