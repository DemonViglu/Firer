using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Runtime;
using Flow.Runtime.Nodes;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flow.Editor
{
    public class FlowNodeView : Node
    {
        private FlowNode m_Node;
        private Dictionary<string, Port> m_PortViews = new Dictionary<string, Port>();
        private Dictionary<Port, FlowPort> m_PortDataMap = new Dictionary<Port, FlowPort>();
        private FlowGraph m_Graph;

        public FlowNode Node => m_Node;
        public Action<FlowNodeView> OnNodeSelected;

        public FlowNodeView(FlowNode node, FlowGraph graph = null)
        {
            m_Node = node;
            m_Graph = graph;
            viewDataKey = node.nodeId;

            var attr = node.GetType().GetCustomAttributes(typeof(FlowNodeAttribute), false).FirstOrDefault() as FlowNodeAttribute;
            string displayName = attr != null ? attr.menuPath.Split('/').Last() : node.GetType().Name;
            title = displayName;

            SetPosition(new Rect(node.position, new Vector2(200, 150)));

            SetupNodeStyle(attr);
            CreatePorts();
            CreateDynamicPortButtons();
        }

        private void SetupNodeStyle(FlowNodeAttribute attr)
        {
            string category = "";
            if (attr != null && attr.menuPath.Contains("/"))
            {
                category = attr.menuPath.Split('/')[0];
            }

            switch (category)
            {
                case "Flow":
                    titleContainer.style.backgroundColor = new Color(0.2f, 0.4f, 0.6f, 0.8f);
                    break;
                case "Events":
                    titleContainer.style.backgroundColor = new Color(0.6f, 0.3f, 0.2f, 0.8f);
                    break;
                case "Debug":
                    titleContainer.style.backgroundColor = new Color(0.4f, 0.4f, 0.2f, 0.8f);
                    break;
                case "Logic":
                    titleContainer.style.backgroundColor = new Color(0.3f, 0.5f, 0.3f, 0.8f);
                    break;
                case "AI":
                    titleContainer.style.backgroundColor = new Color(0.5f, 0.3f, 0.5f, 0.8f);
                    break;
                case "Dialog":
                    titleContainer.style.backgroundColor = new Color(0.2f, 0.5f, 0.5f, 0.8f);
                    break;
                case "Owner":
                    titleContainer.style.backgroundColor = new Color(0.4f, 0.35f, 0.2f, 0.8f);
                    break;
                case "Data":
                    titleContainer.style.backgroundColor = new Color(0.2f, 0.45f, 0.3f, 0.8f);
                    break;
                case "Action":
                    titleContainer.style.backgroundColor = new Color(0.5f, 0.35f, 0.2f, 0.8f);
                    break;
                default:
                    titleContainer.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                    break;
            }
        }

        private void CreatePorts()
        {
            foreach (var portData in m_Node.Ports)
            {
                CreatePortView(portData);
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        private Port CreatePortView(FlowPort portData)
        {
            var direction = portData.direction == FlowPortDirection.Input
                ? Direction.Input
                : Direction.Output;

            var capacity = portData.IsSingleCapacity()
                ? Port.Capacity.Single
                : Port.Capacity.Multi;

            Type portType = portData.portType == FlowPortType.Execution
                ? typeof(FlowExecutionPort)
                : (portData.DataType ?? typeof(object));

            var port = InstantiatePort(Orientation.Horizontal, direction, capacity, portType);
            port.portName = portData.portName;
            port.portColor = GetPortColor(portData);

            m_PortViews[portData.portId] = port;
            m_PortDataMap[port] = portData;

            if (direction == Direction.Input)
            {
                inputContainer.Add(port);
            }
            else
            {
                outputContainer.Add(port);
            }

            return port;
        }

        private void CreateDynamicPortButtons()
        {
            if (!m_Node.SupportsDynamicPorts)
            {
                return;
            }

            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.justifyContent = Justify.Center;
            buttonContainer.style.marginTop = 4;

            var addButton = new Button(() => OnAddPort()) { text = "+" };
            addButton.style.width = 24;
            addButton.style.height = 20;
            addButton.style.fontSize = 14;

            var removeButton = new Button(() => OnRemovePort()) { text = "-" };
            removeButton.style.width = 24;
            removeButton.style.height = 20;
            removeButton.style.fontSize = 14;

            buttonContainer.Add(addButton);
            buttonContainer.Add(removeButton);

            outputContainer.Add(buttonContainer);
        }

        private void OnAddPort()
        {
            if (m_Graph != null)
            {
                Undo.RecordObject(m_Graph, "Add Port");
            }

            if (m_Node is SequenceNode seqNode)
            {
                seqNode.AddOutput();
            }
            else if (m_Node is ParallelNode parNode)
            {
                parNode.AddOutput();
            }

            var newPort = m_Node.Ports[m_Node.Ports.Count - 1];
            CreatePortView(newPort);
            RefreshExpandedState();
            RefreshPorts();

            if (m_Graph != null)
            {
                EditorUtility.SetDirty(m_Graph);
            }
        }

        private void OnRemovePort()
        {
            if (m_Graph != null)
            {
                Undo.RecordObject(m_Graph, "Remove Port");
            }

            bool removed = false;
            string removedPortId = null;

            if (m_Node is SequenceNode seqNode)
            {
                var outputs = m_Node.GetExecutionOutputs();
                if (outputs.Count > 1)
                {
                    removedPortId = outputs[outputs.Count - 1].portId;
                    removed = seqNode.RemoveLastOutput();
                }
            }
            else if (m_Node is ParallelNode parNode)
            {
                var outputs = m_Node.GetExecutionOutputs();
                if (outputs.Count > 1)
                {
                    removedPortId = outputs[outputs.Count - 1].portId;
                    removed = parNode.RemoveLastOutput();
                }
            }

            if (removed && removedPortId != null && m_PortViews.TryGetValue(removedPortId, out var portView))
            {
                foreach (var edge in portView.connections.ToList())
                {
                    edge.output.Disconnect(edge);
                    edge.input.Disconnect(edge);
                    var graphView = GetFirstAncestorOfType<FlowGraphView>();
                    graphView?.RemoveElement(edge);
                }

                m_PortDataMap.Remove(portView);
                m_PortViews.Remove(removedPortId);
                outputContainer.Remove(portView);
                RefreshExpandedState();
                RefreshPorts();
            }

            if (m_Graph != null)
            {
                EditorUtility.SetDirty(m_Graph);
            }
        }

        private Color GetPortColor(FlowPort portData)
        {
            if (portData.portType == FlowPortType.Execution)
            {
                return new Color(0.9f, 0.9f, 0.9f, 1f);
            }

            var dataType = portData.DataType;
            if (dataType == typeof(float) || dataType == typeof(int))
            {
                return new Color(0.4f, 0.8f, 0.4f, 1f);
            }
            if (dataType == typeof(bool))
            {
                return new Color(0.8f, 0.3f, 0.3f, 1f);
            }
            if (dataType == typeof(string))
            {
                return new Color(0.9f, 0.8f, 0.3f, 1f);
            }
            return new Color(0.4f, 0.6f, 0.9f, 1f);
        }

        public Port GetPortView(string portId)
        {
            m_PortViews.TryGetValue(portId, out var port);
            return port;
        }

        public FlowPort GetPortData(Port portView)
        {
            m_PortDataMap.TryGetValue(portView, out var portData);
            return portData;
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            m_Node.position = newPos.position;
        }

        public override void OnSelected()
        {
            base.OnSelected();
            OnNodeSelected?.Invoke(this);
        }

        public void UpdateDebugState(FlowNodeState state)
        {
            RemoveFromClassList("flow-node-active");
            RemoveFromClassList("flow-node-completed");
            RemoveFromClassList("flow-node-failed");

            switch (state)
            {
                case FlowNodeState.Active:
                    AddToClassList("flow-node-active");
                    break;
                case FlowNodeState.Completed:
                    AddToClassList("flow-node-completed");
                    break;
                case FlowNodeState.Failed:
                    AddToClassList("flow-node-failed");
                    break;
            }
        }

        public void ClearDebugState()
        {
            RemoveFromClassList("flow-node-active");
            RemoveFromClassList("flow-node-completed");
            RemoveFromClassList("flow-node-failed");
        }
    }

    public class FlowExecutionPort { }
}
