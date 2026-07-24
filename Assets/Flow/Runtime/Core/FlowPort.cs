using UnityEngine;

namespace Flow.Runtime
{
    [System.Serializable]
    public class FlowPort
    {
        [SerializeField] private string m_PortId;
        [SerializeField] private string m_PortName;
        [SerializeField] private FlowPortDirection m_Direction;
        [SerializeField] private FlowPortType m_PortType;
        [SerializeField] private string m_DataTypeName;

        public string portId => m_PortId;
        public string portName => m_PortName;
        public FlowPortDirection direction => m_Direction;
        public FlowPortType portType => m_PortType;
        public System.Type DataType => string.IsNullOrEmpty(m_DataTypeName) ? null : System.Type.GetType(m_DataTypeName);

        [System.NonSerialized] public FlowNode ownerNode;

        public FlowPort(string name, FlowPortDirection direction, FlowPortType portType, System.Type dataType = null)
        {
            m_PortId = System.Guid.NewGuid().ToString();
            m_PortName = name;
            m_Direction = direction;
            m_PortType = portType;
            m_DataTypeName = dataType != null ? dataType.AssemblyQualifiedName : string.Empty;
        }

        public bool IsSingleCapacity()
        {
            if (m_Direction == FlowPortDirection.Input)
            {
                return true;
            }
            return false;
        }
    }
}
