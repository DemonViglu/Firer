using Flow.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flow.Editor
{
    public class FlowEditorWindow : EditorWindow
    {
        private FlowGraphView m_GraphView;
        private FlowInspectorView m_InspectorView;
        private FlowGraph m_CurrentGraph;
        private FlowGraphRunner m_ActiveRunner;
        private bool m_WasPlaying;

        [MenuItem("Window/Flow/Flow Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<FlowEditorWindow>();
            window.titleContent = new GUIContent("Flow Editor");
        }

        public static void OpenGraph(FlowGraph graph)
        {
            var window = GetWindow<FlowEditorWindow>();
            window.titleContent = new GUIContent("Flow Editor");
            window.LoadGraph(graph);
        }

        private void OnEnable()
        {
            CreateGUI();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            var toolbar = CreateToolbar();
            rootVisualElement.Add(toolbar);

            var splitView = new TwoPaneSplitView(1, 381, TwoPaneSplitViewOrientation.Horizontal);

            m_GraphView = new FlowGraphView(this);
            m_GraphView.style.flexGrow = 1;
            m_GraphView.OnNodeSelected += OnNodeSelected;

            m_InspectorView = new FlowInspectorView();
            m_InspectorView.style.width = 180;
            m_InspectorView.style.minWidth = 150;

            splitView.Add(m_GraphView);
            splitView.Add(m_InspectorView);

            rootVisualElement.Add(splitView);

            if (m_CurrentGraph != null)
            {
                m_GraphView.PopulateView(m_CurrentGraph);
            }
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 24;
            toolbar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            toolbar.style.paddingLeft = 4;
            toolbar.style.paddingRight = 4;

            var graphField = new UnityEditor.UIElements.ObjectField("Graph");
            graphField.objectType = typeof(FlowGraph);
            graphField.value = m_CurrentGraph;
            graphField.style.flexGrow = 1;
            graphField.style.maxWidth = 400;
            graphField.RegisterValueChangedCallback(evt =>
            {
                LoadGraph(evt.newValue as FlowGraph);
            });
            toolbar.Add(graphField);

            var saveButton = new Button(() => SaveGraph()) { text = "Save" };
            saveButton.style.marginLeft = 8;
            toolbar.Add(saveButton);

            return toolbar;
        }

        public void LoadGraph(FlowGraph graph)
        {
            m_CurrentGraph = graph;
            if (m_GraphView != null && graph != null)
            {
                m_GraphView.PopulateView(graph);
            }
            else if (m_GraphView != null)
            {
                m_GraphView.ClearGraph();
            }
        }

        private void SaveGraph()
        {
            if (m_CurrentGraph != null)
            {
                EditorUtility.SetDirty(m_CurrentGraph);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Flow] Graph '{m_CurrentGraph.name}' saved");
            }
        }

        private void OnNodeSelected(FlowNodeView nodeView)
        {
            m_InspectorView?.UpdateSelection(nodeView, m_CurrentGraph);
        }

        private void OnSelectionChange()
        {
            var graph = Selection.activeObject as FlowGraph;
            if (graph != null)
            {
                LoadGraph(graph);
            }
        }

        private void OnEditorUpdate()
        {
            if (m_GraphView == null)
            {
                return;
            }

            if (EditorApplication.isPlaying)
            {
                if (!m_WasPlaying)
                {
                    m_WasPlaying = true;
                    m_ActiveRunner = null;
                }

                FindActiveRunner();

                if (m_ActiveRunner != null && m_ActiveRunner.IsRunning)
                {
                    m_GraphView.UpdateRuntimeStates(m_ActiveRunner);
                    Repaint();
                }
            }
            else
            {
                if (m_WasPlaying)
                {
                    m_WasPlaying = false;
                    m_ActiveRunner = null;
                    m_GraphView.ClearRuntimeStates();
                    Repaint();
                }
            }
        }

        private void FindActiveRunner()
        {
            if (m_ActiveRunner != null && m_ActiveRunner.IsRunning && m_ActiveRunner.Graph == m_CurrentGraph)
            {
                return;
            }

            m_ActiveRunner = null;

            if (Selection.activeGameObject != null)
            {
                var runner = Selection.activeGameObject.GetComponent<FlowGraphRunner>();
                if (runner != null && runner.Graph == m_CurrentGraph)
                {
                    m_ActiveRunner = runner;
                    return;
                }
            }

            var runners = Object.FindObjectsByType<FlowGraphRunner>();
            foreach (var runner in runners)
            {
                if (runner.Graph == m_CurrentGraph && runner.IsRunning)
                {
                    m_ActiveRunner = runner;
                    return;
                }
            }
        }
    }
}
