using System.Collections.Generic;
using UnityEngine;

namespace Flow.Runtime.Nodes
{
    [FlowNode("Flow/Sequence", "Execute outputs in order, waiting for each branch to complete")]
    [System.Serializable]
    public class SequenceNode : FlowNode
    {
        [SerializeField] private int m_OutputCount = 2;

        public int outputCount => m_OutputCount;
        public override bool SupportsDynamicPorts => true;

        [System.NonSerialized] private int m_CurrentIndex;
        [System.NonSerialized] private bool m_WaitForProcess;
        [System.NonSerialized] private HashSet<FlowNode> m_CurrentBranchNodes;

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            for (int i = 0; i < m_OutputCount; i++)
            {
                AddExecutionOutput($"Out_{i}");
            }
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            var outputs = GetExecutionOutputs();
            if (outputs.Count == 0)
            {
                return FlowNodeState.Completed;
            }

            m_CurrentIndex = 0;
            m_WaitForProcess = false;
            m_CurrentBranchNodes = CollectReachableNodes(context, outputs[0]);
            TriggerOutput(context, outputs[0].portName);
            return FlowNodeState.Active;
        }

        public override void OnTick(FlowExecutionContext context, float deltaTime)
        {
            if (m_WaitForProcess)
            {
                m_WaitForProcess = false;
                return;
            }

            if (!IsBranchComplete(context))
            {
                return;
            }

            m_CurrentIndex++;
            var outputs = GetExecutionOutputs();

            if (m_CurrentIndex >= outputs.Count)
            {
                state = FlowNodeState.Completed;
                return;
            }

            m_CurrentBranchNodes = CollectReachableNodes(context, outputs[m_CurrentIndex]);
            TriggerOutput(context, outputs[m_CurrentIndex].portName);
            m_WaitForProcess = true;
        }

        private bool IsBranchComplete(FlowExecutionContext context)
        {
            if (m_CurrentBranchNodes == null || m_CurrentBranchNodes.Count == 0)
            {
                return true;
            }

            var runner = context.Runner;
            foreach (var node in m_CurrentBranchNodes)
            {
                if (node.state == FlowNodeState.Active || runner.IsNodePending(node))
                {
                    return false;
                }
            }
            return true;
        }

        private HashSet<FlowNode> CollectReachableNodes(FlowExecutionContext context, FlowPort outputPort)
        {
            var reachable = new HashSet<FlowNode>();
            var graph = context.Graph;
            var visited = new HashSet<string>();
            var queue = new Queue<string>();

            var connections = graph.GetConnectionsFromPort(nodeId, outputPort.portId);
            foreach (var conn in connections)
            {
                queue.Enqueue(conn.inputNodeId);
            }

            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                if (visited.Contains(id))
                {
                    continue;
                }
                visited.Add(id);

                var node = context.GetNode(id);
                if (node == null)
                {
                    continue;
                }
                reachable.Add(node);

                var outputs = node.GetExecutionOutputs();
                foreach (var port in outputs)
                {
                    var downstreamConns = graph.GetConnectionsFromPort(id, port.portId);
                    foreach (var conn in downstreamConns)
                    {
                        queue.Enqueue(conn.inputNodeId);
                    }
                }
            }

            return reachable;
        }

        public void AddOutput()
        {
            string name = $"Out_{m_OutputCount}";
            m_OutputCount++;
            AddExecutionOutputPublic(name);
        }

        public bool RemoveLastOutput()
        {
            if (m_OutputCount <= 1)
            {
                return false;
            }

            var outputs = GetExecutionOutputs();
            if (outputs.Count > 0)
            {
                var last = outputs[outputs.Count - 1];
                RemovePort(last.portId);
                m_OutputCount--;
                return true;
            }
            return false;
        }
    }
}
