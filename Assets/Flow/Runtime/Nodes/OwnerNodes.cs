using System.Reflection;
using UnityEngine;

namespace Flow.Runtime.Nodes
{
    [FlowNode("Owner/Get GameObject", "Get a bound GameObject from FlowOwner")]
    [System.Serializable]
    public class GetOwnerGameObjectNode : FlowNode
    {
        [FlowBindingKey]
        [SerializeField] private string m_Key = "";

        public string key { get => m_Key; set => m_Key = value; }

        private GameObject m_CachedResult;

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddExecutionOutput("Out");
            AddDataOutput<GameObject>("Object");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            m_CachedResult = context.GetGameObject(m_Key);
            if (m_CachedResult == null)
            {
                Debug.LogWarning($"[Flow] GetOwnerGameObject: No binding found for key '{m_Key}'");
            }
            TriggerOutput(context, "Out");
            return FlowNodeState.Completed;
        }

        public override T GetOutputValue<T>(string portId)
        {
            if (typeof(T) == typeof(GameObject))
            {
                return (T)(object)m_CachedResult;
            }
            return default;
        }
    }

    [FlowNode("Owner/Set Active", "Enable/disable a bound GameObject")]
    [System.Serializable]
    public class SetOwnerActiveNode : FlowNode
    {
        [FlowBindingKey]
        [SerializeField] private string m_Key = "";
        [SerializeField] private bool m_Active = true;

        public string key { get => m_Key; set => m_Key = value; }
        public bool active { get => m_Active; set => m_Active = value; }

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddDataInput<bool>("Active");
            AddExecutionOutput("Out");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            var go = context.GetGameObject(m_Key);
            if (go != null)
            {
                bool val = context.GetInputValue<bool>(this, "Active", m_Active);
                go.SetActive(val);
            }
            TriggerOutput(context, "Out");
            return FlowNodeState.Completed;
        }
    }

    [FlowNode("Owner/Get Component", "Get a component from a bound object")]
    [System.Serializable]
    public class GetOwnerComponentNode : FlowNode
    {
        [FlowBindingKey]
        [SerializeField] private string m_Key = "";

        public string key { get => m_Key; set => m_Key = value; }

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddExecutionOutput("Out");
            AddDataOutput<Component>("Component");
        }

        private Component m_CachedComponent;

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            m_CachedComponent = context.GetBinding<Component>(m_Key);
            TriggerOutput(context, "Out");
            return FlowNodeState.Completed;
        }

        public override T GetOutputValue<T>(string portId)
        {
            if (m_CachedComponent is T typed)
            {
                return typed;
            }
            return default;
        }
    }

    [FlowNode("Owner/Send Message", "Call a method on bound object via SendMessage")]
    [System.Serializable]
    public class SendMessageNode : FlowNode
    {
        [FlowBindingKey]
        [SerializeField] private string m_Key = "";
        [SerializeField] private string m_MethodName = "";

        public string key { get => m_Key; set => m_Key = value; }
        public string methodName { get => m_MethodName; set => m_MethodName = value; }

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddExecutionOutput("Out");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            var go = context.GetGameObject(m_Key);
            if (go != null && !string.IsNullOrEmpty(m_MethodName))
            {
                go.SendMessage(m_MethodName, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                Debug.LogWarning($"[Flow] SendMessage: key='{m_Key}' method='{m_MethodName}' failed");
            }
            TriggerOutput(context, "Out");
            return FlowNodeState.Completed;
        }
    }

    [FlowNode("Owner/Invoke Method", "Call a specific method on a bound Component via reflection")]
    [System.Serializable]
    public class InvokeMethodNode : FlowNode
    {
        [FlowBindingKey]
        [SerializeField] private string m_Key = "";
        [SerializeField] private string m_MethodName = "";

        public string key { get => m_Key; set => m_Key = value; }
        public string methodName { get => m_MethodName; set => m_MethodName = value; }

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddExecutionOutput("Success");
            AddExecutionOutput("Failure");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            var component = context.GetBinding<Component>(m_Key);
            if (component == null)
            {
                Debug.LogWarning($"[Flow] InvokeMethod: No component bound for key '{m_Key}'");
                TriggerOutput(context, "Failure");
                return FlowNodeState.Completed;
            }

            var method = component.GetType().GetMethod(m_MethodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null)
            {
                Debug.LogWarning($"[Flow] InvokeMethod: Method '{m_MethodName}' not found on {component.GetType().Name}");
                TriggerOutput(context, "Failure");
                return FlowNodeState.Completed;
            }

            method.Invoke(component, null);
            TriggerOutput(context, "Success");
            return FlowNodeState.Completed;
        }
    }
}
