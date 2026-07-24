using System.Collections.Generic;
using UnityEngine;

namespace Flow.Runtime.Nodes
{
    [FlowNode("Flow/Parallel", "Execute all outputs simultaneously")]
    [System.Serializable]
    public class ParallelNode : FlowNode
    {
        [SerializeField] private int m_OutputCount = 2;

        public int outputCount => m_OutputCount;
        public override bool SupportsDynamicPorts => true;

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
            foreach (var output in outputs)
            {
                TriggerOutput(context, output.portName);
            }
            return FlowNodeState.Completed;
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
