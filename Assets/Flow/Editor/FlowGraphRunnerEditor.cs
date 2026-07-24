using System;
using System.Collections.Generic;
using System.Reflection;
using Flow.Runtime;
using UnityEditor;
using UnityEngine;

namespace Flow.Editor
{
    [CustomEditor(typeof(FlowGraphRunner))]
    public class FlowGraphRunnerEditor : UnityEditor.Editor
    {
        private FlowGraphRunner m_Runner;
        private List<string> m_RequiredKeys = new List<string>();

        private void OnEnable()
        {
            m_Runner = (FlowGraphRunner)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Graph"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AutoStart"));

            EditorGUILayout.Space(10);

            DrawBindingsSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBindingsSection()
        {
            var graph = m_Runner.Graph;
            if (graph == null)
            {
                EditorGUILayout.HelpBox("Assign a FlowGraph to see required bindings.", MessageType.Info);
                return;
            }

            CollectRequiredKeys(graph);

            EditorGUILayout.LabelField("Bindings", EditorStyles.boldLabel);

            if (m_RequiredKeys.Count == 0)
            {
                EditorGUILayout.HelpBox("This graph has no binding keys.", MessageType.Info);
                return;
            }

            bool changed = false;

            foreach (string key in m_RequiredKeys)
            {
                UnityEngine.Object currentTarget = null;
                int bindingIndex = -1;

                for (int i = 0; i < m_Runner.Bindings.Count; i++)
                {
                    if (m_Runner.Bindings[i].key == key)
                    {
                        currentTarget = m_Runner.Bindings[i].target;
                        bindingIndex = i;
                        break;
                    }
                }

                EditorGUILayout.BeginHorizontal();

                if (currentTarget == null)
                {
                    GUI.color = new Color(1f, 0.9f, 0.5f);
                }

                var newTarget = EditorGUILayout.ObjectField(key, currentTarget, typeof(UnityEngine.Object), true);

                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();

                if (newTarget != currentTarget)
                {
                    Undo.RecordObject(m_Runner, "Change Flow Binding");
                    if (bindingIndex >= 0)
                    {
                        m_Runner.Bindings[bindingIndex].target = newTarget;
                    }
                    else
                    {
                        m_Runner.Bindings.Add(new FlowBinding { key = key, target = newTarget });
                    }
                    changed = true;
                }
            }

            var unusedBindings = new List<int>();
            for (int i = 0; i < m_Runner.Bindings.Count; i++)
            {
                if (!m_RequiredKeys.Contains(m_Runner.Bindings[i].key))
                {
                    unusedBindings.Add(i);
                }
            }

            if (unusedBindings.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Unused Bindings", EditorStyles.miniLabel);
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                foreach (int idx in unusedBindings)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(m_Runner.Bindings[idx].key, GUILayout.Width(120));
                    EditorGUILayout.ObjectField(m_Runner.Bindings[idx].target, typeof(UnityEngine.Object), true);
                    EditorGUILayout.EndHorizontal();
                }
                GUI.color = Color.white;

                if (GUILayout.Button("Remove Unused Bindings"))
                {
                    Undo.RecordObject(m_Runner, "Remove Unused Bindings");
                    for (int i = unusedBindings.Count - 1; i >= 0; i--)
                    {
                        m_Runner.Bindings.RemoveAt(unusedBindings[i]);
                    }
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(m_Runner);
            }
        }

        private void CollectRequiredKeys(FlowGraph graph)
        {
            m_RequiredKeys.Clear();
            var seen = new HashSet<string>();

            foreach (var node in graph.Nodes)
            {
                if (node == null)
                {
                    continue;
                }

                var fields = node.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var field in fields)
                {
                    if (field.FieldType != typeof(string))
                    {
                        continue;
                    }

                    var bindingAttr = field.GetCustomAttribute<FlowBindingKeyAttribute>();
                    if (bindingAttr == null)
                    {
                        continue;
                    }

                    string keyValue = (string)field.GetValue(node);
                    if (!string.IsNullOrEmpty(keyValue) && seen.Add(keyValue))
                    {
                        m_RequiredKeys.Add(keyValue);
                    }
                }
            }

            m_RequiredKeys.Sort();
        }
    }
}
