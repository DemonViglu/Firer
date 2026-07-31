using System;
using System.Collections.Generic;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Pure player-side facade for the activity domain. It owns no Unity
    /// component and is the only object a future PlayerActivityHost needs to
    /// keep for activity sessions, logic factories and action routing.
    /// </summary>
    public sealed class ActivityRuntime
    {
        private readonly IActivityLogicFactory _logicFactory;

        public ActivityCatalog Catalog { get; }
        public ActivitySystem System { get; }

        public ActivityRuntime(ActivityCatalog catalog, IActivityLogicFactory logicFactory)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            System = new ActivitySystem();
            _logicFactory = logicFactory ?? throw new ArgumentNullException(nameof(logicFactory));
        }

        public ActivityStartResult TryStart(
            string activityId,
            IActivityContext context,
            IEnumerable<IActivityRuleProvider> ruleProviders)
        {
            if (!Catalog.TryGet(activityId, out var definition))
                return ActivityStartResult.Reject("Activity is not registered");

            return TryStart(definition, context, ruleProviders);
        }

        public ActivityStartResult TryStart(
            ActivityDefinition definition,
            IActivityContext context,
            IEnumerable<IActivityRuleProvider> ruleProviders)
        {
            if (definition == null || !definition.IsValid)
                return ActivityStartResult.Reject("Invalid activity definition");

            var logic = _logicFactory.Create(definition);
            return logic == null
                ? ActivityStartResult.Reject("Activity logic is not registered")
                : System.TryStart(definition, context, logic, ruleProviders);
        }

        public ActivityActionResult SubmitAction(ActivityActionRequest request) => System.HandleAction(request);

        public bool Tick(string playerId, float deltaTime) => System.Tick(playerId, deltaTime);

        public bool End(string playerId, ActivityEndReason reason = ActivityEndReason.Requested) =>
            System.End(playerId, reason);
    }

    /// <summary>无需 Unity 组件的按 LogicKey 注册表。</summary>
    public sealed class ActivityLogicRegistry : IActivityLogicFactory
    {
        private readonly Dictionary<string, Func<ActivityDefinition, IActivityLogic>> _factories =
            new(StringComparer.Ordinal);

        public void Register(string logicKey, Func<ActivityDefinition, IActivityLogic> factory)
        {
            if (string.IsNullOrWhiteSpace(logicKey) || factory == null)
                throw new ArgumentException("Activity logic registration is invalid");

            _factories[logicKey.Trim()] = factory;
        }

        public IActivityLogic Create(ActivityDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.LogicKey)) return null;
            return _factories.TryGetValue(definition.LogicKey, out var factory)
                ? factory(definition)
                : null;
        }
    }
}
