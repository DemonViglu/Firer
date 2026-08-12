using UnityEngine;

namespace DemonViglu.FirePlay.Rendering
{
    public abstract class EnvironmentWarmthReceiverBehaviour : MonoBehaviour, IEnvironmentWarmthReceiver
    {
        protected virtual void OnEnable() => EnvironmentWarmthDirector.Register(this);
        protected virtual void OnDisable() => EnvironmentWarmthDirector.Unregister(this);
        public abstract void ApplyEnvironmentWarmth(EnvironmentWarmthField field, float deltaTime);
    }
}
