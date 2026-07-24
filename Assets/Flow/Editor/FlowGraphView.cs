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
    public class FlowGraphView : GraphView
    {
        private FlowEditorWindow m_EditorWindow;
        private FlowGraph m_Graph;
        private FlowSearchWindow m_SearchWindow;
        private Vector2 m_LastNodeCreationPosition;

        public Action<FlowNodeView> OnNodeSelected;

        public FlowGraphView(FlowEditorWindow editorWindow)
        {
            m_EditorWindow = editorWindow;

            Insert(0, new GridBackground());

            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Game/Scripts/Flow/Editor/FlowEditorStyles.uss");
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }

            m_SearchWindow = ScriptableObject.CreateInstance<FlowSearchWindow>();
            m_SearchWindow.Initialize(this);
            nodeCreationRequest = context =>
            {
                var localMousePos = context.screenMousePosition - m_EditorWindow.position.position;
                m_LastNodeCreationPosition = contentViewContainer.WorldToLocal(this.LocalToWorld(localMousePos));
                SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), m_SearchWindow);
            };

            graphViewChanged += OnGraphViewChanged;
            Undo.undoRedoPerformed += OnUndoRedo;

            serializeGraphElements = OnSerializeElements;
            canPasteSerializedData = OnCanPaste;
            unserializeAndPaste = OnPaste;
        }

        ~FlowGraphView()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            if (m_Graph != null)
            {
                PopulateView(m_Graph);
            }
        }

        [System.Serializable]
        private class CopyData
        {
            public List<CopyNodeData> nodes = new List<CopyNodeData>();
            public List<CopyConnectionData> connections = new List<CopyConnectionData>();
        }

        [System.Serializable]
        private class CopyNodeData
        {
            public string typeName;
            public string json;
            public string oldNodeId;
        }

        [System.Serializable]
        private class CopyConnectionData
        {
            public string outputNodeId;
            public string outputPortId;
            public string inputNodeId;
            public string inputPortId;
        }

        private string OnSerializeElements(IEnumerable<GraphElement> elements)
        {
            var copyData = new CopyData();
            var selectedNodeIds = new HashSet<string>();

            foreach (var element in elements)
            {
                if (element is FlowNodeView nodeView)
                {
                    selectedNodeIds.Add(nodeView.Node.nodeId);
                    copyData.nodes.Add(new CopyNodeData
                    {
                        typeName = nodeView.Node.GetType().AssemblyQualifiedName,
                        json = JsonUtility.ToJson(nodeView.Node),
                        oldNodeId = nodeView.Node.nodeId
                    });
                }
            }

            if (m_Graph != null)
            {
                foreach (var conn in m_Graph.Connections)
                {
                    if (selectedNodeIds.Contains(conn.outputNodeId) && selectedNodeIds.Contains(conn.inputNodeId))
                    {
                        copyData.connections.Add(new CopyConnectionData
                        {
                            outputNodeId = conn.outputNodeId,
                            outputPortId = conn.outputPortId,
                            inputNodeId = conn.inputNodeId,
                            inputPortId = conn.inputPortId
                        });
                    }
                }
            }

            return JsonUtility.ToJson(copyData);
        }

        private bool OnCanPaste(string data)
        {
            try
            {
                var copyData = JsonUtility.FromJson<CopyData>(data);
                return copyData != null && copyData.nodes.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private void OnPaste(string operationName, string data)
        {
            if (m_Graph == null)
            {
                return;
            }

            CopyData copyData;
            try
            {
                copyData = JsonUtility.FromJson<CopyData>(data);
            }
            catch
            {
                return;
            }

            if (copyData == null || copyData.nodes.Count == 0)
            {
                return;
            }

            Undo.RecordObject(m_Graph, "Paste Flow Nodes");

            var idMapping = new Dictionary<string, string>();
            var newNodes = new List<FlowNode>();
            Vector2 offset = new Vector2(50, 50);

            foreach (var nodeData in copyData.nodes)
            {
                var type = System.Type.GetType(nodeData.typeName);
                if (type == null)
                {
                    continue;
                }

                var newNode = (FlowNode)JsonUtility.FromJson(nodeData.json, type);
                string oldId = newNode.RegenerateNodeId();
                newNode.InitializeOwnership();
                newNode.position += offset;

                idMapping[oldId] = newNode.nodeId;
                m_Graph.AddExistingNode(newNode);
                newNodes.Add(newNode);
            }

            foreach (var connData in copyData.connections)
            {
                if (idMapping.TryGetValue(connData.outputNodeId, out string newOutputId) &&
                    idMapping.TryGetValue(connData.inputNodeId, out string newInputId))
                {
                    m_Graph.Connect(newOutputId, connData.outputPortId, newInputId, connData.inputPortId);
                }
            }

            EditorUtility.SetDirty(m_Graph);
            PopulateView(m_Graph);

            ClearSelection();
            foreach (var node in newNodes)
            {
                var view = GetNodeByGuid(node.nodeId) as FlowNodeView;
                if (view != null)
                {
                    AddToSelection(view);
                }
            }
        }

        public void PopulateView(FlowGraph graph)
        {
            m_Graph = graph;

            graphViewChanged -= OnGraphViewChanged;

            ClearGraph();

            if (m_Graph == null)
            {
                graphViewChanged += OnGraphViewChanged;
                return;
            }

            foreach (var node in m_Graph.Nodes)
            {
                if (node != null)
                {
                    CreateNodeView(node);
                }
            }

            foreach (var conn in m_Graph.Connections)
            {
                CreateEdgeView(conn);
            }

            graphViewChanged += OnGraphViewChanged;
        }

        public void ClearGraph()
        {
            DeleteElements(graphElements.ToList());
        }

        private void CreateNodeView(FlowNode node)
        {
            var nodeView = new FlowNodeView(node, m_Graph);
            nodeView.OnNodeSelected = OnNodeSelected;
            AddElement(nodeView);
        }

        private void CreateEdgeView(FlowConnection connection)
        {
            var outputNodeView = GetNodeByGuid(connection.outputNodeId) as FlowNodeView;
            var inputNodeView = GetNodeByGuid(connection.inputNodeId) as FlowNodeView;

            if (outputNodeView == null || inputNodeView == null)
            {
                return;
            }

            var outputPort = outputNodeView.GetPortView(connection.outputPortId);
            var inputPort = inputNodeView.GetPortView(connection.inputPortId);

            if (outputPort == null || inputPort == null)
            {
                return;
            }

            var edge = outputPort.ConnectTo(inputPort);
            AddElement(edge);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(endPort =>
                endPort.direction != startPort.direction &&
                endPort.node != startPort.node &&
                endPort.portType == startPort.portType
            ).ToList();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            if (m_Graph == null)
            {
                return graphViewChange;
            }

            Undo.RecordObject(m_Graph, "Flow Graph Change");

            if (graphViewChange.elementsToRemove != null)
            {
                foreach (var element in graphViewChange.elementsToRemove)
                {
                    if (element is FlowNodeView nodeView)
                    {
                        m_Graph.RemoveNode(nodeView.Node);
                    }
                    else if (element is Edge edge)
                    {
                        var outputNodeView = edge.output.node as FlowNodeView;
                        var inputNodeView = edge.input.node as FlowNodeView;
                        if (outputNodeView != null && inputNodeView != null)
                        {
                            var outputPortData = outputNodeView.GetPortData(edge.output);
                            var inputPortData = inputNodeView.GetPortData(edge.input);
                            if (outputPortData != null && inputPortData != null)
                            {
                                var conn = m_Graph.Connections.FirstOrDefault(c =>
                                    c.outputNodeId == outputNodeView.Node.nodeId &&
                                    c.outputPortId == outputPortData.portId &&
                                    c.inputNodeId == inputNodeView.Node.nodeId &&
                                    c.inputPortId == inputPortData.portId);
                                if (conn != null)
                                {
                                    m_Graph.Disconnect(conn);
                                }
                            }
                        }
                    }
                }
            }

            if (graphViewChange.edgesToCreate != null)
            {
                foreach (var edge in graphViewChange.edgesToCreate)
                {
                    var outputNodeView = edge.output.node as FlowNodeView;
                    var inputNodeView = edge.input.node as FlowNodeView;
                    if (outputNodeView != null && inputNodeView != null)
                    {
                        var outputPortData = outputNodeView.GetPortData(edge.output);
                        var inputPortData = inputNodeView.GetPortData(edge.input);
                        if (outputPortData != null && inputPortData != null)
                        {
                            m_Graph.Connect(
                                outputNodeView.Node.nodeId, outputPortData.portId,
                                inputNodeView.Node.nodeId, inputPortData.portId
                            );
                        }
                    }
                }
            }

            if (graphViewChange.movedElements != null)
            {
                foreach (var element in graphViewChange.movedElements)
                {
                    if (element is FlowNodeView nodeView)
                    {
                        nodeView.Node.position = nodeView.GetPosition().position;
                    }
                }
            }

            EditorUtility.SetDirty(m_Graph);
            return graphViewChange;
        }

        public void CreateNode(Type nodeType, Vector2 screenPosition)
        {
            if (m_Graph == null)
            {
                return;
            }

            var windowMousePos = m_EditorWindow.position.position;
            var localPos = screenPosition - windowMousePos;
            var graphPos = contentViewContainer.WorldToLocal(localPos);

            Undo.RecordObject(m_Graph, "Create Flow Node");
            var node = m_Graph.AddNode(nodeType, graphPos);
            EditorUtility.SetDirty(m_Graph);

            CreateNodeView(node);
        }

        public void CreateNodeAtGraphPosition(Type nodeType, Vector2 graphPosition)
        {
            if (m_Graph == null)
            {
                return;
            }

            Undo.RecordObject(m_Graph, "Create Flow Node");
            var node = m_Graph.AddNode(nodeType, graphPosition);
            EditorUtility.SetDirty(m_Graph);

            CreateNodeView(node);
        }

        public Vector2 GetLastNodeCreationPosition()
        {
            return m_LastNodeCreationPosition;
        }

        public void UpdateRuntimeStates(FlowGraphRunner runner)
        {
            if (runner == null || runner.Graph == null)
            {
                ClearRuntimeStates();
                return;
            }

            foreach (var element in graphElements.ToList())
            {
                if (element is FlowNodeView nodeView)
                {
                    nodeView.UpdateDebugState(nodeView.Node.state);
                }
            }
        }

        public void ClearRuntimeStates()
        {
            foreach (var element in graphElements.ToList())
            {
                if (element is FlowNodeView nodeView)
                {
                    nodeView.ClearDebugState();
                }
            }
        }
    }
}
