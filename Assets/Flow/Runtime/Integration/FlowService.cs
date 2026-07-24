using System.Collections.Generic;
using UnityEngine;

namespace Flow.Runtime
{
    public class FlowService : IFlowService
    {
        private List<FlowGraphRunner> m_ActiveRunners = new List<FlowGraphRunner>();

        public IReadOnlyList<FlowGraphRunner> ActiveRunners => m_ActiveRunners;

        public FlowGraphRunner RunGraph(FlowGraph graph, GameObject owner = null)
        {
            if (graph == null)
            {
                Debug.LogError("[FlowService] Cannot run null graph");
                return null;
            }

            if (owner == null)
            {
                owner = new GameObject($"FlowRunner_{graph.name}");
            }

            var runner = owner.AddComponent<FlowGraphRunner>();
            runner.OnGraphCompleted += (success) => OnRunnerCompleted(runner);
            runner.Run(graph);
            m_ActiveRunners.Add(runner);

            var eventBus = GameInstanceSubsystem.TryGet<IEventPublisher>();
            if (eventBus != null)
            {
                eventBus.Publish(new FlowGraphStartedEvent(graph.name));
            }

            return runner;
        }

        public void StopGraph(FlowGraphRunner runner)
        {
            if (runner == null)
            {
                return;
            }

            runner.Stop();
            m_ActiveRunners.Remove(runner);
        }

        public void StopAll()
        {
            for (int i = m_ActiveRunners.Count - 1; i >= 0; i--)
            {
                if (m_ActiveRunners[i] != null)
                {
                    m_ActiveRunners[i].Stop();
                }
            }
            m_ActiveRunners.Clear();
        }

        private void OnRunnerCompleted(FlowGraphRunner runner)
        {
            m_ActiveRunners.Remove(runner);
        }
    }
}
