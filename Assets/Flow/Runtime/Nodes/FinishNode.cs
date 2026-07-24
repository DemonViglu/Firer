using UnityEngine;

namespace Flow.Runtime.Nodes
{
    [FlowNode("Flow/Finish", "Graph exit point")]
    [System.Serializable]
    public class FinishNode : FlowNode
    {
        [SerializeField] private bool m_Success = true;

        public bool success { get => m_Success; set => m_Success = value; }

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            context.MarkGraphComplete(m_Success);
            return FlowNodeState.Completed;
        }
    }
}
