using UnityEngine;

namespace Flow.Runtime.Nodes
{
    [FlowNode("Events/Publish Event", "Publish a custom flow event via EventBus")]
    [System.Serializable]
    public class CustomEventNode : FlowNode
    {
        [SerializeField] private string m_EventId;

        public string eventId { get => m_EventId; set => m_EventId = value; }

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddExecutionOutput("Out");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            var eventBus = GameInstanceSubsystem.TryGet<IEventPublisher>();
            if (eventBus != null && !string.IsNullOrEmpty(m_EventId))
            {
                eventBus.Publish(new FlowCustomEvent(m_EventId, context.Graph.name));
            }
            TriggerOutput(context, "Out");
            return FlowNodeState.Completed;
        }
    }
}
