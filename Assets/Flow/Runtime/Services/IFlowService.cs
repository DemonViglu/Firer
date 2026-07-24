using System.Collections.Generic;

namespace Flow.Runtime
{
    public interface IFlowService
    {
        FlowGraphRunner RunGraph(FlowGraph graph, UnityEngine.GameObject owner = null);
        void StopGraph(FlowGraphRunner runner);
        IReadOnlyList<FlowGraphRunner> ActiveRunners { get; }
    }
}
