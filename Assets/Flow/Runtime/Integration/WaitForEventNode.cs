using System;
using UnityEngine;

namespace Flow.Runtime.Nodes
{
    [FlowNode("Events/Wait For Event", "Wait until a custom flow event is received")]
    [System.Serializable]
    public class WaitForEventNode : FlowNode
    {
        [SerializeField] private string m_EventId;

        private Action<FlowCustomEvent> m_Handler;

        public string eventId { get => m_EventId; set => m_EventId = value; }

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddExecutionOutput("Out");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            var eventBus = GameInstanceSubsystem.TryGet<IEventPublisher>();
            if (eventBus == null)
            {
                Debug.LogWarning("[Flow] WaitForEventNode: No IEventPublisher available");
                return FlowNodeState.Failed;
            }

            m_Handler = (evt) =>
            {
                if (evt.eventId == m_EventId)
                {
                    TriggerOutput(context, "Out");
                    state = FlowNodeState.Completed;
                }
            };

            eventBus.Subscribe(m_Handler);
            context.RegisterCleanup(() => eventBus.Unsubscribe(m_Handler));
            return FlowNodeState.Active;
        }
    }
}
