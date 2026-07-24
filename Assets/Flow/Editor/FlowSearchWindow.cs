using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Flow.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flow.Editor
{
    public class FlowSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private FlowGraphView m_GraphView;
        private Texture2D m_IndentationIcon;

        public void Initialize(FlowGraphView graphView)
        {
            m_GraphView = graphView;
            m_IndentationIcon = new Texture2D(1, 1);
            m_IndentationIcon.SetPixel(0, 0, Color.clear);
            m_IndentationIcon.Apply();
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node"), 0)
            };

            var nodeTypes = GetAllNodeTypes();
            var groups = new HashSet<string>();

            foreach (var (type, attr) in nodeTypes.OrderBy(x => x.attr.menuPath))
            {
                string[] parts = attr.menuPath.Split('/');

                string groupPath = "";
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    groupPath += parts[i];
                    if (!groups.Contains(groupPath))
                    {
                        groups.Add(groupPath);
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(parts[i]), i + 1));
                    }
                    groupPath += "/";
                }

                string nodeName = parts[parts.Length - 1];
                string tooltip = string.IsNullOrEmpty(attr.description) ? "" : attr.description;
                tree.Add(new SearchTreeEntry(new GUIContent(nodeName, m_IndentationIcon, tooltip))
                {
                    userData = type,
                    level = parts.Length
                });
            }

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            var nodeType = searchTreeEntry.userData as Type;
            if (nodeType == null)
            {
                return false;
            }

            var graphPosition = m_GraphView.GetLastNodeCreationPosition();
            m_GraphView.CreateNodeAtGraphPosition(nodeType, graphPosition);
            return true;
        }

        private List<(Type type, FlowNodeAttribute attr)> GetAllNodeTypes()
        {
            var result = new List<(Type, FlowNodeAttribute)>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsAbstract || !typeof(FlowNode).IsAssignableFrom(type))
                        {
                            continue;
                        }

                        var attr = type.GetCustomAttribute<FlowNodeAttribute>();
                        if (attr != null)
                        {
                            result.Add((type, attr));
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                }
            }

            return result;
        }
    }
}
