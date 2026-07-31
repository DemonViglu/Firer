using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DemonViglu.FirePlay.Debugging
{
    /// <summary>
    /// 无美术复杂 Presenter 探针：验证独立 Input Action Map 和 ActivityActionRequested 生命周期。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingActivityPresenterProbe : MonoBehaviour, IActivityPresenter
    {
        [SerializeField] private bool _showDebugPanel = true;

        private InputActionMap _inputMap;
        private InputAction _castAction;
        private InputAction _reelAction;
        private InputAction _exitAction;
        private ActivityUIOrchestrator _orchestrator;
        private ActivitySessionSnapshot _session;
        private bool _isPresented;
        private string _lastActionId = "等待动作";

        public string ActivityId => "fishing";

        public void Present(ActivitySessionSnapshot session, ActivityUIOrchestrator orchestrator)
        {
            _session = session;
            _orchestrator = orchestrator;
            _isPresented = true;
            EnsureInputMap();
            _inputMap.Enable();
        }

        public void Close()
        {
            _isPresented = false;
            _inputMap?.Disable();
            _orchestrator = null;
        }

        private void EnsureInputMap()
        {
            if (_inputMap != null) return;

            _inputMap = new InputActionMap("FishingActivityProbe");
            _castAction = _inputMap.AddAction("Cast");
            _castAction.AddBinding("<Keyboard>/q");
            _reelAction = _inputMap.AddAction("Reel");
            _reelAction.AddBinding("<Keyboard>/e");
            _exitAction = _inputMap.AddAction("Exit");
            _exitAction.AddBinding("<Keyboard>/escape");

            _castAction.performed += OnCast;
            _reelAction.performed += OnReel;
            _exitAction.performed += OnExit;
        }

        private void OnCast(InputAction.CallbackContext context) => PublishAction("fishing.cast");
        private void OnReel(InputAction.CallbackContext context) => PublishAction("fishing.reel");
        private void OnExit(InputAction.CallbackContext context) => PublishAction("activity.exit");

        private void PublishAction(string actionId)
        {
            if (!_isPresented || _orchestrator == null) return;
            _lastActionId = actionId;
            _orchestrator.RequestAction(actionId);
        }

        private void OnGUI()
        {
            if (!_showDebugPanel || !_isPresented) return;

            GUILayout.BeginArea(new Rect(16f, Screen.height - 150f, 360f, 130f), GUI.skin.box);
            GUILayout.Label("Fishing Presenter Probe");
            GUILayout.Label($"Session: {_session.ActivityId} / rev {_session.Revision}");
            GUILayout.Label("Q 抛竿   E 收线   Esc 退出动作");
            GUILayout.Label($"Last action: {_lastActionId}");
            GUILayout.EndArea();
        }

        private void OnDisable()
        {
            Close();
            if (_inputMap != null)
            {
                _castAction.performed -= OnCast;
                _reelAction.performed -= OnReel;
                _exitAction.performed -= OnExit;
                _inputMap.Dispose();
                _inputMap = null;
            }
        }
    }
}
