using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Flow.Runtime
{
    [CreateAssetMenu(fileName = "NewFlowGraph", menuName = "Flow/Flow Graph")]
    public class FlowGraph : ScriptableObject
    {
        [SerializeField] private string m_GraphId;
        [SerializeReference] private List<FlowNode> m_Nodes = new List<FlowNode>();
        [SerializeField] private List<FlowConnection> m_Connections = new List<FlowConnection>();

        public string graphId => m_GraphId;
        public IReadOnlyList<FlowNode> Nodes => m_Nodes;
        public IReadOnlyList<FlowConnection> Connections => m_Connections;

        public FlowNode EntryNode => m_Nodes.FirstOrDefault(n => n is Nodes.StartNode);

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(m_GraphId))
            {
                m_GraphId = System.Guid.NewGuid().ToString();
            }
            foreach (var node in m_Nodes)
            {
                if (node != null)
                {
                    node.InitializeOwnership();
                }
            }
        }

        public FlowNode AddNode(System.Type nodeType, Vector2 position)
        {
            if (!typeof(FlowNode).IsAssignableFrom(nodeType))
            {
                Debug.LogError($"[FlowGraph] {nodeType.Name} is not a FlowNode");
                return null;
            }

            var node = (FlowNode)System.Activator.CreateInstance(nodeType);
            node.position = position;
            m_Nodes.Add(node);
            return node;
        }

        public T AddNode<T>(Vector2 position) where T : FlowNode, new()
        {
            var node = new T();
            node.position = position;
            m_Nodes.Add(node);
            return node;
        }

        public void AddExistingNode(FlowNode node)
        {
            m_Nodes.Add(node);
        }

        public void RemoveNode(FlowNode node)
        {
            m_Connections.RemoveAll(c => c.outputNodeId == node.nodeId || c.inputNodeId == node.nodeId);
            m_Nodes.Remove(node);
        }

        public FlowConnection Connect(string outputNodeId, string outputPortId, string inputNodeId, string inputPortId)
        {
            var existingConnection = m_Connections.FirstOrDefault(c =>
                c.inputNodeId == inputNodeId && c.inputPortId == inputPortId);

            var inputNode = GetNode(inputNodeId);
            var inputPort = inputNode?.GetPort(inputPortId);
            if (inputPort != null && inputPort.IsSingleCapacity() && existingConnection != null)
            {
                m_Connections.Remove(existingConnection);
            }

            var connection = new FlowConnection(outputNodeId, outputPortId, inputNodeId, inputPortId);
            m_Connections.Add(connection);
            return connection;
        }

        public void Disconnect(FlowConnection connection)
        {
            m_Connections.Remove(connection);
        }

        public void Disconnect(string connectionId)
        {
            m_Connections.RemoveAll(c => c.connectionId == connectionId);
        }

        public FlowNode GetNode(string nodeId)
        {
            return m_Nodes.FirstOrDefault(n => n.nodeId == nodeId);
        }

        public List<FlowConnection> GetConnectionsFromPort(string nodeId, string portId)
        {
            return m_Connections.Where(c => c.outputNodeId == nodeId && c.outputPortId == portId).ToList();
        }

        public FlowConnection GetConnectionToPort(string nodeId, string portId)
        {
            return m_Connections.FirstOrDefault(c => c.inputNodeId == nodeId && c.inputPortId == portId);
        }

        public List<FlowNode> GetConnectedNodes(FlowNode node, FlowPort outputPort)
        {
            var connections = m_Connections.Where(c => c.outputNodeId == node.nodeId && c.outputPortId == outputPort.portId);
            return connections.Select(c => GetNode(c.inputNodeId)).Where(n => n != null).ToList();
        }
    }
}
