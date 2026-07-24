using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Flow.Runtime
{
    public class FlowExecutionContext
    {
        private FlowGraphRunner m_Runner;
        private FlowGraph m_Graph;
        private FlowBlackboard m_Blackboard;
        private Dictionary<string, FlowNode> m_NodeLookup;
        private List<Action> m_CleanupActions = new List<Action>();

        public FlowGraph Graph => m_Graph;
        public IFlowBlackboard Blackboard => m_Blackboard;
        public FlowGraphRunner Runner => m_Runner;
        public bool WasSuccessful { get; private set; } = true;
        public bool IsCompleted { get; private set; }

        public T GetBinding<T>(string key) where T : UnityEngine.Object
        {
            return m_Runner.GetBinding<T>(key);
        }

        public GameObject GetGameObject(string key)
        {
            return m_Runner.GetGameObject(key);
        }

        public FlowExecutionContext(FlowGraphRunner runner, FlowGraph graph)
        {
            m_Runner = runner;
            m_Graph = graph;
            m_Blackboard = new FlowBlackboard();
            BuildLookups();
        }

        private void BuildLookups()
        {
            m_NodeLookup = new Dictionary<string, FlowNode>();
            foreach (var node in m_Graph.Nodes)
            {
                if (node != null)
                {
                    m_NodeLookup[node.nodeId] = node;
                }
            }
        }

        public T GetInputValue<T>(FlowNode node, string portName, T defaultValue = default)
        {
            var port = node.Ports.FirstOrDefault(p => p.portName == portName && p.direction == FlowPortDirection.Input);
            if (port == null)
            {
                return defaultValue;
            }

            var connection = m_Graph.GetConnectionToPort(node.nodeId, port.portId);
            if (connection == null)
            {
                return defaultValue;
            }

            if (!m_NodeLookup.TryGetValue(connection.outputNodeId, out var sourceNode))
            {
                return defaultValue;
            }

            return sourceNode.GetOutputValue<T>(connection.outputPortId);
        }

        public void TriggerPort(FlowNode sourceNode, FlowPort outputPort)
        {
            if (outputPort.portType != FlowPortType.Execution)
            {
                return;
            }

            var connections = m_Graph.GetConnectionsFromPort(sourceNode.nodeId, outputPort.portId);
            foreach (var conn in connections)
            {
                if (m_NodeLookup.TryGetValue(conn.inputNodeId, out var targetNode))
                {
                    m_Runner.EnqueueExecution(targetNode, conn.inputPortId);
                }
            }
        }

        public void MarkGraphComplete(bool success)
        {
            WasSuccessful = success;
            IsCompleted = true;
        }

        public void RegisterCleanup(Action cleanup)
        {
            m_CleanupActions.Add(cleanup);
        }

        public void Cleanup()
        {
            foreach (var action in m_CleanupActions)
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Flow] Cleanup error: {ex.Message}");
                }
            }
            m_CleanupActions.Clear();
            m_Blackboard.Clear();
        }

        public FlowNode GetNode(string nodeId)
        {
            m_NodeLookup.TryGetValue(nodeId, out var node);
            return node;
        }
    }
}
