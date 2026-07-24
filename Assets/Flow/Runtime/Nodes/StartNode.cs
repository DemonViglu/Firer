namespace Flow.Runtime.Nodes
{
    [FlowNode("Flow/Start", "Graph entry point")]
    [System.Serializable]
    public class StartNode : FlowNode
    {
        protected override void DefinePortLayout()
        {
            AddExecutionOutput("Out");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            TriggerOutput(context, "Out");
            return FlowNodeState.Completed;
        }
    }
}
