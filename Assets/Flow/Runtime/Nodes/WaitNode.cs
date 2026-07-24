using UnityEngine;

namespace Flow.Runtime.Nodes
{
    [FlowNode("Flow/Wait", "Wait for duration")]
    [System.Serializable]
    public class WaitNode : FlowNode
    {
        [SerializeField] private float m_Duration = 1f;

        private float m_Elapsed;

        public float duration { get => m_Duration; set => m_Duration = value; }

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddDataInput<float>("Duration");
            AddExecutionOutput("Out");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            m_Elapsed = 0f;
            return FlowNodeState.Active;
        }

        public override void OnTick(FlowExecutionContext context, float deltaTime)
        {
            m_Elapsed += deltaTime;
            float targetDuration = context.GetInputValue<float>(this, "Duration", m_Duration);
            if (m_Elapsed >= targetDuration)
            {
                TriggerOutput(context, "Out");
                state = FlowNodeState.Completed;
            }
        }
    }
}
