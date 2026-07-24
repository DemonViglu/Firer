using Flow.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Flow.Editor
{
    [CustomEditor(typeof(FlowGraph))]
    public class FlowGraphEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var graph = target as FlowGraph;
            if (graph == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Flow Graph", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Graph ID", graph.graphId);
            EditorGUILayout.LabelField("Nodes", graph.Nodes.Count.ToString());
            EditorGUILayout.LabelField("Connections", graph.Connections.Count.ToString());

            EditorGUILayout.Space();

            if (UnityEngine.GUILayout.Button("Open in Flow Editor"))
            {
                FlowEditorWindow.OpenGraph(graph);
            }
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            if (Selection.activeObject is FlowGraph asset)
            {
                FlowEditorWindow.OpenGraph(asset);
                return true;
            }
            return false;
        }
    }
}
