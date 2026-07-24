using UnityEngine;

namespace Flow.Runtime.Nodes
{
    [FlowNode("Flow/Branch", "Conditional branch")]
    [System.Serializable]
    public class BranchNode : FlowNode
    {
        [SerializeField] private bool m_DefaultCondition = true;

        public bool defaultCondition { get => m_DefaultCondition; set => m_DefaultCondition = value; }

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddDataInput<bool>("Condition");
            AddExecutionOutput("True");
            AddExecutionOutput("False");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            bool condition = context.GetInputValue<bool>(this, "Condition", m_DefaultCondition);
            TriggerOutput(context, condition ? "True" : "False");
            return FlowNodeState.Completed;
        }
    }
}
