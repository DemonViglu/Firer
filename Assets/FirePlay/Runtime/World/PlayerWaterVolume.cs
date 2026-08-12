using System.Collections.Generic;
using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// Explicit scene-owned water contact volume. It enables the basic buoyancy state
    /// only after the player's root passes below the waterline, so standing on ice does
    /// not accidentally count as swimming.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PlayerWaterVolume : MonoBehaviour
    {
        [SerializeField] private float _surfaceY;
        [SerializeField, Min(0f)] private float _entryDepth = 0.18f;

        private readonly Dictionary<PlayerMovement, HashSet<Collider>> _occupants = new();

        private void Awake()
        {
            var waterCollider = GetComponent<Collider>();
            if (!waterCollider.isTrigger)
            {
                Debug.LogError("[PlayerWaterVolume] Water collider must be a trigger.", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            RefreshContact(other);
        }

        private void OnTriggerStay(Collider other)
        {
            RefreshContact(other);
        }

        private void OnTriggerExit(Collider other)
        {
            var movement = other.GetComponentInParent<PlayerMovement>();
            if (movement == null || !_occupants.TryGetValue(movement, out var contacts))
            {
                return;
            }

            contacts.Remove(other);
            if (contacts.Count > 0)
            {
                return;
            }

            _occupants.Remove(movement);
            movement.SetWaterContact(this, _surfaceY, false);
        }

        private void OnDisable()
        {
            foreach (var movement in _occupants.Keys)
            {
                if (movement != null)
                {
                    movement.SetWaterContact(this, _surfaceY, false);
                }
            }

            _occupants.Clear();
        }

        private void RefreshContact(Collider other)
        {
            var movement = other.GetComponentInParent<PlayerMovement>();
            if (movement == null)
            {
                return;
            }

            if (movement.transform.position.y > _surfaceY - _entryDepth && !movement.IsInWater)
            {
                return;
            }

            if (!_occupants.TryGetValue(movement, out var contacts))
            {
                contacts = new HashSet<Collider>();
                _occupants.Add(movement, contacts);
            }

            contacts.Add(other);
            movement.SetWaterContact(this, _surfaceY, true);
        }

        private void OnValidate()
        {
            _entryDepth = Mathf.Max(0f, _entryDepth);
        }
    }
}
