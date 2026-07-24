using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flow.Runtime
{
    public class FlowGraphRunner : MonoBehaviour
    {
        [SerializeField] private FlowGraph m_Graph;
        [SerializeField] private bool m_AutoStart = false;
        [SerializeField] private List<FlowBinding> m_Bindings = new List<FlowBinding>();

        private FlowExecutionContext m_Context;
        private FlowOwner m_FallbackOwner;
        private List<FlowNode> m_ActiveNodes = new List<FlowNode>();
        private Queue<(FlowNode node, string portId)> m_PendingExecutions = new Queue<(FlowNode, string)>();
        private bool m_IsRunning;

        public FlowGraph Graph => m_Graph;
        public bool IsRunning => m_IsRunning;
        public FlowExecutionContext Context => m_Context;
        public List<FlowBinding> Bindings => m_Bindings;

        public bool IsNodePending(FlowNode node)
        {
            foreach (var (pendingNode, _) in m_PendingExecutions)
            {
                if (pendingNode == node)
                {
                    return true;
                }
            }
            return false;
        }

        public T GetBinding<T>(string key) where T : UnityEngine.Object
        {
            var obj = GetBindingObject(key);
            if (obj is T typed)
            {
                return typed;
            }
            if (obj is GameObject go && typeof(Component).IsAssignableFrom(typeof(T)))
            {
                return go.GetComponent<T>();
            }
            return null;
        }

        public GameObject GetGameObject(string key)
        {
            var obj = GetBindingObject(key);
            if (obj is GameObject go)
            {
                return go;
            }
            if (obj is Component comp)
            {
                return comp.gameObject;
            }
            return null;
        }

        public void SetBinding(string key, UnityEngine.Object target)
        {
            for (int i = 0; i < m_Bindings.Count; i++)
            {
                if (m_Bindings[i].key == key)
                {
                    m_Bindings[i].target = target;
                    return;
                }
            }
            m_Bindings.Add(new FlowBinding { key = key, target = target });
        }

        public bool HasBinding(string key)
        {
            for (int i = 0; i < m_Bindings.Count; i++)
            {
                if (m_Bindings[i].key == key && m_Bindings[i].target != null)
                {
                    return true;
                }
            }
            return GetFallbackOwner()?.HasBinding(key) ?? false;
        }

        private UnityEngine.Object GetBindingObject(string key)
        {
            for (int i = 0; i < m_Bindings.Count; i++)
            {
                if (m_Bindings[i].key == key && m_Bindings[i].target != null)
                {
                    return m_Bindings[i].target;
                }
            }

            var fallback = GetFallbackOwner();
            if (fallback != null)
            {
                return fallback.GetBinding(key);
            }
            return null;
        }

        private FlowOwner GetFallbackOwner()
        {
            if (m_FallbackOwner == null)
            {
                m_FallbackOwner = GetComponent<FlowOwner>();
                if (m_FallbackOwner == null)
                {
                    m_FallbackOwner = GetComponentInParent<FlowOwner>();
                }
            }
            return m_FallbackOwner;
        }

        public event Action OnGraphStarted;
        public event Action<bool> OnGraphCompleted;

        private void Start()
        {
            if (m_AutoStart && m_Graph != null)
            {
                Run();
            }
        }

        public void Run(FlowGraph overrideGraph = null)
        {
            if (overrideGraph != null)
            {
                m_Graph = overrideGraph;
            }

            if (m_Graph == null)
            {
                Debug.LogError("[FlowGraphRunner] No FlowGraph assigned");
                return;
            }

            Stop();

            m_Context = new FlowExecutionContext(this, m_Graph);
            m_IsRunning = true;

            var entry = m_Graph.EntryNode;
            if (entry != null)
            {
                ExecuteNode(entry);
            }
            else
            {
                Debug.LogWarning("[FlowGraphRunner] No StartNode found in graph");
                m_IsRunning = false;
            }

            OnGraphStarted?.Invoke();
        }

        public void Stop()
        {
            if (!m_IsRunning)
            {
                return;
            }

            foreach (var node in m_ActiveNodes)
            {
                node.OnExit(m_Context);
                node.state = FlowNodeState.Idle;
            }

            m_ActiveNodes.Clear();
            m_PendingExecutions.Clear();
            m_Context?.Cleanup();
            m_IsRunning = false;
        }

        private void Update()
        {
            if (!m_IsRunning)
            {
                return;
            }

            ProcessPendingExecutions();
            TickActiveNodes();
            CheckCompletion();
        }

        private void ProcessPendingExecutions()
        {
            int safetyCounter = 1000;
            while (m_PendingExecutions.Count > 0 && safetyCounter > 0)
            {
                safetyCounter--;
                var (node, portId) = m_PendingExecutions.Dequeue();
                node.triggeredPortId = portId;
                ExecuteNode(node);
            }

            if (safetyCounter <= 0)
            {
                Debug.LogError("[FlowGraphRunner] Execution limit reached, possible infinite loop");
                Stop();
            }
        }

        private void TickActiveNodes()
        {
            for (int i = m_ActiveNodes.Count - 1; i >= 0; i--)
            {
                var node = m_ActiveNodes[i];
                node.OnTick(m_Context, Time.deltaTime);

                if (node.state == FlowNodeState.Completed || node.state == FlowNodeState.Failed)
                {
                    node.OnExit(m_Context);
                    m_ActiveNodes.RemoveAt(i);
                }
            }
        }

        private void CheckCompletion()
        {
            if (m_Context.IsCompleted)
            {
                CompleteGraph(m_Context.WasSuccessful);
                return;
            }

            if (m_ActiveNodes.Count == 0 && m_PendingExecutions.Count == 0)
            {
                CompleteGraph(m_Context.WasSuccessful);
            }
        }

        internal void ExecuteNode(FlowNode node)
        {
            node.OnInitialize(m_Context);
            var result = node.OnExecute(m_Context);
            node.state = result;

            switch (result)
            {
                case FlowNodeState.Active:
                    m_ActiveNodes.Add(node);
                    break;
                case FlowNodeState.Completed:
                    node.OnExit(m_Context);
                    break;
                case FlowNodeState.Failed:
                    node.OnExit(m_Context);
                    Debug.LogWarning($"[FlowGraphRunner] Node {node.GetType().Name} failed");
                    break;
            }
        }

        internal void EnqueueExecution(FlowNode node, string inputPortId = null)
        {
            m_PendingExecutions.Enqueue((node, inputPortId));
        }

        private void CompleteGraph(bool success)
        {
            m_IsRunning = false;
            m_Context.Cleanup();

            OnGraphCompleted?.Invoke(success);
        }

        private void OnDestroy()
        {
            Stop();
        }
    }
}
