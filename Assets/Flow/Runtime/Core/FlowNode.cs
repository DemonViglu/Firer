using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Flow.Runtime
{
    [System.Serializable]
    public abstract class FlowNode
    {
        [SerializeField] private string m_NodeId;
        [SerializeField] private Vector2 m_Position;
        [SerializeField] private List<FlowPort> m_Ports = new List<FlowPort>();

        public string nodeId => m_NodeId;
        public Vector2 position { get => m_Position; set => m_Position = value; }
        public IReadOnlyList<FlowPort> Ports => m_Ports;

        [System.NonSerialized] public FlowNodeState state = FlowNodeState.Idle;
        [System.NonSerialized] public string triggeredPortId;

        public FlowNode()
        {
            m_NodeId = System.Guid.NewGuid().ToString();
            DefinePortLayout();
            foreach (var port in m_Ports)
            {
                port.ownerNode = this;
            }
        }

        protected virtual void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddExecutionOutput("Out");
        }

        public virtual void OnInitialize(FlowExecutionContext context) { }

        public virtual FlowNodeState OnExecute(FlowExecutionContext context)
        {
            return FlowNodeState.Completed;
        }

        public virtual void OnTick(FlowExecutionContext context, float deltaTime) { }

        public virtual void OnExit(FlowExecutionContext context) { }

        protected FlowPort AddExecutionInput(string name = "In")
        {
            var port = new FlowPort(name, FlowPortDirection.Input, FlowPortType.Execution);
            port.ownerNode = this;
            m_Ports.Add(port);
            return port;
        }

        protected FlowPort AddExecutionOutput(string name = "Out")
        {
            var port = new FlowPort(name, FlowPortDirection.Output, FlowPortType.Execution);
            port.ownerNode = this;
            m_Ports.Add(port);
            return port;
        }

        protected FlowPort AddDataInput<T>(string name)
        {
            var port = new FlowPort(name, FlowPortDirection.Input, FlowPortType.Data, typeof(T));
            port.ownerNode = this;
            m_Ports.Add(port);
            return port;
        }

        protected FlowPort AddDataOutput<T>(string name)
        {
            var port = new FlowPort(name, FlowPortDirection.Output, FlowPortType.Data, typeof(T));
            port.ownerNode = this;
            m_Ports.Add(port);
            return port;
        }

        protected void TriggerOutput(FlowExecutionContext context, string portName = "Out")
        {
            var port = m_Ports.FirstOrDefault(p => p.portName == portName && p.direction == FlowPortDirection.Output);
            if (port != null)
            {
                context.TriggerPort(this, port);
            }
        }

        public FlowPort GetPort(string portId)
        {
            return m_Ports.FirstOrDefault(p => p.portId == portId);
        }

        public FlowPort GetPortByName(string portName, FlowPortDirection direction)
        {
            return m_Ports.FirstOrDefault(p => p.portName == portName && p.direction == direction);
        }

        public List<FlowPort> GetExecutionOutputs()
        {
            return m_Ports.Where(p => p.direction == FlowPortDirection.Output && p.portType == FlowPortType.Execution).ToList();
        }

        public virtual T GetOutputValue<T>(string portId)
        {
            return default;
        }

        public void InitializeOwnership()
        {
            foreach (var port in m_Ports)
            {
                port.ownerNode = this;
            }
        }

        public string RegenerateNodeId()
        {
            string oldId = m_NodeId;
            m_NodeId = System.Guid.NewGuid().ToString();
            return oldId;
        }

        public FlowPort AddExecutionOutputPublic(string name)
        {
            var port = new FlowPort(name, FlowPortDirection.Output, FlowPortType.Execution);
            port.ownerNode = this;
            m_Ports.Add(port);
            return port;
        }

        public bool RemovePort(string portId)
        {
            var port = m_Ports.FirstOrDefault(p => p.portId == portId);
            if (port != null)
            {
                m_Ports.Remove(port);
                return true;
            }
            return false;
        }

        public virtual bool SupportsDynamicPorts => false;
    }
}
