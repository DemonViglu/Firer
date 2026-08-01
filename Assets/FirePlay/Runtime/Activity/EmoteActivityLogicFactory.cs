using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>Scene/bootstrap registration for the Anywhere emote activity.</summary>
    [DisallowMultipleComponent]
    public sealed class EmoteActivityLogicFactory : MonoBehaviour, IActivityLogicFactory
    {
        public IActivityLogic Create(ActivityDefinition definition)
        {
            return definition != null && definition.LogicKey == EmoteActivityLogic.ActivityId
                ? new EmoteActivityLogic()
                : null;
        }
    }
}
