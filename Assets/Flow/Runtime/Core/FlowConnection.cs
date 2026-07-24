using UnityEngine;

namespace Flow.Runtime
{
    [System.Serializable]
    public class FlowConnection
    {
        [SerializeField] private string m_ConnectionId;
        [SerializeField] private string m_OutputNodeId;
        [SerializeField] private string m_OutputPortId;
        [SerializeField] private string m_InputNodeId;
        [SerializeField] private string m_InputPortId;

        public string connectionId => m_ConnectionId;
        public string outputNodeId => m_OutputNodeId;
        public string outputPortId => m_OutputPortId;
        public string inputNodeId => m_InputNodeId;
        public string inputPortId => m_InputPortId;

        public FlowConnection(string outputNodeId, string outputPortId, string inputNodeId, string inputPortId)
        {
            m_ConnectionId = System.Guid.NewGuid().ToString();
            m_OutputNodeId = outputNodeId;
            m_OutputPortId = outputPortId;
            m_InputNodeId = inputNodeId;
            m_InputPortId = inputPortId;
        }
    }
}
