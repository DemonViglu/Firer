using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flow.Runtime
{
    [System.Serializable]
    public class FlowBinding
    {
        public string key;
        public UnityEngine.Object target;
    }

    [AddComponentMenu("Flow/Flow Owner")]
    public class FlowOwner : MonoBehaviour
    {
        [SerializeField] private List<FlowBinding> m_Bindings = new List<FlowBinding>();

        public UnityEngine.Object GetBinding(string key)
        {
            for (int i = 0; i < m_Bindings.Count; i++)
            {
                if (m_Bindings[i].key == key)
                {
                    return m_Bindings[i].target;
                }
            }
            return null;
        }

        public T GetBinding<T>(string key) where T : UnityEngine.Object
        {
            var obj = GetBinding(key);
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
            var obj = GetBinding(key);
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
                if (m_Bindings[i].key == key)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
