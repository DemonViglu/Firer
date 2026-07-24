using UnityEngine;

namespace Flow.Runtime.Nodes
{
    [FlowNode("Flow/SubGraph", "Run another FlowGraph")]
    [System.Serializable]
    public class SubGraphNode : FlowNode
    {
        [SerializeField] private FlowGraph m_SubGraph;

        private FlowGraphRunner m_SubRunner;

        public FlowGraph subGraph { get => m_SubGraph; set => m_SubGraph = value; }

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddExecutionOutput("Success");
            AddExecutionOutput("Failure");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            if (m_SubGraph == null)
            {
                Debug.LogWarning("[Flow] SubGraphNode: No sub-graph assigned");
                TriggerOutput(context, "Failure");
                return FlowNodeState.Completed;
            }

            var runnerGo = new GameObject($"SubGraph_{m_SubGraph.name}");
            runnerGo.transform.SetParent(context.Runner.transform);
            m_SubRunner = runnerGo.AddComponent<FlowGraphRunner>();

            foreach (var binding in context.Runner.Bindings)
            {
                m_SubRunner.SetBinding(binding.key, binding.target);
            }

            m_SubRunner.OnGraphCompleted += (success) =>
            {
                TriggerOutput(context, success ? "Success" : "Failure");
                state = FlowNodeState.Completed;
                Object.Destroy(runnerGo);
            };
            m_SubRunner.Run(m_SubGraph);

            return FlowNodeState.Active;
        }

        public override void OnExit(FlowExecutionContext context)
        {
            if (m_SubRunner != null && m_SubRunner.IsRunning)
            {
                m_SubRunner.Stop();
                Object.Destroy(m_SubRunner.gameObject);
            }
            m_SubRunner = null;
        }
    }
}
