using UnityEngine;

namespace Flow.Runtime.Nodes
{
    [FlowNode("Debug/Log", "Print message to console")]
    [System.Serializable]
    public class LogNode : FlowNode
    {
        [SerializeField] private string m_Message = "Hello Flow";

        public string message { get => m_Message; set => m_Message = value; }

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddDataInput<string>("Message");
            AddExecutionOutput("Out");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            string msg = context.GetInputValue<string>(this, "Message", m_Message);
            Debug.Log($"[Flow] {msg}");
            TriggerOutput(context, "Out");
            return FlowNodeState.Completed;
        }
    }
}
