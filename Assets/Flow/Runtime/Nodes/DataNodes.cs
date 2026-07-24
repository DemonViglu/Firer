using UnityEngine;

namespace Flow.Runtime.Nodes
{
    [FlowNode("Data/Bool Value", "Output a bool value")]
    [System.Serializable]
    public class BoolValueNode : FlowNode
    {
        [SerializeField] private bool m_Value = true;

        public bool value { get => m_Value; set => m_Value = value; }

        protected override void DefinePortLayout()
        {
            AddDataOutput<bool>("Value");
        }

        public override T GetOutputValue<T>(string portId)
        {
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)m_Value;
            }
            return default;
        }
    }

    [FlowNode("Data/Float Value", "Output a float value")]
    [System.Serializable]
    public class FloatValueNode : FlowNode
    {
        [SerializeField] private float m_Value = 0f;

        public float value { get => m_Value; set => m_Value = value; }

        protected override void DefinePortLayout()
        {
            AddDataOutput<float>("Value");
        }

        public override T GetOutputValue<T>(string portId)
        {
            if (typeof(T) == typeof(float))
            {
                return (T)(object)m_Value;
            }
            return default;
        }
    }

    [FlowNode("Data/String Value", "Output a string value")]
    [System.Serializable]
    public class StringValueNode : FlowNode
    {
        [SerializeField] private string m_Value = "";

        public string value { get => m_Value; set => m_Value = value; }

        protected override void DefinePortLayout()
        {
            AddDataOutput<string>("Value");
        }

        public override T GetOutputValue<T>(string portId)
        {
            if (typeof(T) == typeof(string))
            {
                return (T)(object)m_Value;
            }
            return default;
        }
    }

    [FlowNode("Data/Blackboard Get Bool", "Read bool from blackboard")]
    [System.Serializable]
    public class BlackboardGetBoolNode : FlowNode
    {
        [SerializeField] private string m_Key = "";
        [SerializeField] private bool m_DefaultValue = false;

        private FlowExecutionContext m_Context;

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddExecutionOutput("Out");
            AddDataOutput<bool>("Value");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            m_Context = context;
            TriggerOutput(context, "Out");
            return FlowNodeState.Completed;
        }

        public override T GetOutputValue<T>(string portId)
        {
            if (typeof(T) == typeof(bool) && m_Context != null)
            {
                return (T)(object)m_Context.Blackboard.Get<bool>(m_Key, m_DefaultValue);
            }
            return default;
        }
    }

    [FlowNode("Data/Blackboard Set", "Write value to blackboard")]
    [System.Serializable]
    public class BlackboardSetBoolNode : FlowNode
    {
        [SerializeField] private string m_Key = "";
        [SerializeField] private bool m_Value = true;

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddDataInput<bool>("Value");
            AddExecutionOutput("Out");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            bool val = context.GetInputValue<bool>(this, "Value", m_Value);
            context.Blackboard.Set(m_Key, val);
            TriggerOutput(context, "Out");
            return FlowNodeState.Completed;
        }
    }
}
