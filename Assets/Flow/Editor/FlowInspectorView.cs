using System.Reflection;
using Flow.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flow.Editor
{
    public class FlowInspectorView : VisualElement
    {
        private IMGUIContainer m_InspectorContainer;
        private FlowNodeView m_SelectedNodeView;
        private FlowGraph m_Graph;
        private SerializedObject m_SerializedGraph;
        private Vector2 m_ScrollPosition;

        public FlowInspectorView()
        {
            style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            style.paddingTop = 8;
            style.paddingLeft = 8;
            style.paddingRight = 8;

            var titleLabel = new Label("Inspector");
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 8;
            Add(titleLabel);

            m_InspectorContainer = new IMGUIContainer(DrawInspector);
            m_InspectorContainer.style.flexGrow = 1;
            Add(m_InspectorContainer);
        }

        public void UpdateSelection(FlowNodeView nodeView, FlowGraph graph)
        {
            m_SelectedNodeView = nodeView;
            m_Graph = graph;
            m_SerializedGraph = graph != null ? new SerializedObject(graph) : null;
        }

        private void DrawInspector()
        {
            if (m_SelectedNodeView == null || m_Graph == null)
            {
                EditorGUILayout.LabelField("No node selected");
                return;
            }

            var node = m_SelectedNodeView.Node;
            var nodeType = node.GetType();

            var attr = nodeType.GetCustomAttribute<FlowNodeAttribute>();
            string displayName = attr != null ? attr.menuPath : nodeType.Name;

            EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Node ID", node.nodeId);
            EditorGUILayout.Space(8);

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

            DrawNodeFields(node, nodeType);

            EditorGUILayout.EndScrollView();
        }

        private void DrawNodeFields(FlowNode node, System.Type nodeType)
        {
            var fields = nodeType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            bool changed = false;

            foreach (var field in fields)
            {
                var serializeField = field.GetCustomAttribute<SerializeField>();
                bool isPublic = field.IsPublic;

                if (!isPublic && serializeField == null)
                {
                    continue;
                }

                if (field.DeclaringType == typeof(FlowNode))
                {
                    continue;
                }

                var hideAttr = field.GetCustomAttribute<HideInInspector>();
                if (hideAttr != null)
                {
                    continue;
                }

                string label = ObjectNames.NicifyVariableName(field.Name.Replace("m_", ""));
                var value = field.GetValue(node);

                EditorGUI.BeginChangeCheck();

                object newValue = DrawField(label, field.FieldType, value);

                if (EditorGUI.EndChangeCheck())
                {
                    if (m_Graph != null)
                    {
                        Undo.RecordObject(m_Graph, $"Change {label}");
                    }
                    field.SetValue(node, newValue);
                    changed = true;
                }
            }

            if (changed && m_Graph != null)
            {
                EditorUtility.SetDirty(m_Graph);
            }
        }

        private object DrawField(string label, System.Type fieldType, object value)
        {
            if (fieldType == typeof(string))
            {
                return EditorGUILayout.TextField(label, (string)value ?? "");
            }
            if (fieldType == typeof(int))
            {
                return EditorGUILayout.IntField(label, (int)value);
            }
            if (fieldType == typeof(float))
            {
                return EditorGUILayout.FloatField(label, (float)value);
            }
            if (fieldType == typeof(bool))
            {
                return EditorGUILayout.Toggle(label, (bool)value);
            }
            if (fieldType == typeof(Vector2))
            {
                return EditorGUILayout.Vector2Field(label, (Vector2)value);
            }
            if (fieldType == typeof(Vector3))
            {
                return EditorGUILayout.Vector3Field(label, (Vector3)value);
            }
            if (fieldType == typeof(Color))
            {
                return EditorGUILayout.ColorField(label, (Color)value);
            }
            if (fieldType.IsEnum)
            {
                return EditorGUILayout.EnumPopup(label, (System.Enum)value);
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                return EditorGUILayout.ObjectField(label, (UnityEngine.Object)value, fieldType, false);
            }

            EditorGUILayout.LabelField(label, value?.ToString() ?? "(null)");
            return value;
        }
    }
}
