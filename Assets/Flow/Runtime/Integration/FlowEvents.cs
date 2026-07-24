namespace Flow.Runtime
{
    public struct FlowGraphStartedEvent : IGameEvent
    {
        public string graphName;

        public FlowGraphStartedEvent(string graphName)
        {
            this.graphName = graphName;
        }
    }

    public struct FlowGraphCompletedEvent : IGameEvent
    {
        public string graphName;
        public bool success;

        public FlowGraphCompletedEvent(string graphName, bool success)
        {
            this.graphName = graphName;
            this.success = success;
        }
    }

    public struct FlowCustomEvent : IGameEvent
    {
        public string eventId;
        public string graphName;

        public FlowCustomEvent(string eventId, string graphName)
        {
            this.eventId = eventId;
            this.graphName = graphName;
        }
    }

    public struct FlowNodeStateChangedEvent : IGameEvent
    {
        public string graphName;
        public string nodeId;
        public FlowNodeState oldState;
        public FlowNodeState newState;

        public FlowNodeStateChangedEvent(string graphName, string nodeId, FlowNodeState oldState, FlowNodeState newState)
        {
            this.graphName = graphName;
            this.nodeId = nodeId;
            this.oldState = oldState;
            this.newState = newState;
        }
    }
}
