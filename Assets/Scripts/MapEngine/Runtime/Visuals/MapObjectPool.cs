using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.MapEngine
{
    public class MapObjectPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Transform parentTransform;
        private readonly Queue<T> poolQueue = new Queue<T>();
        private readonly List<T> activeList = new List<T>();

        public IReadOnlyList<T> ActiveObjects => activeList;

        public MapObjectPool(T prefab, int initialCapacity, Transform parentTransform = null)
        {
            this.prefab = prefab;
            this.parentTransform = parentTransform;

            for (int i = 0; i < initialCapacity; i++)
            {
                T instance = Object.Instantiate(prefab, parentTransform);
                instance.gameObject.SetActive(false);
                poolQueue.Enqueue(instance);
            }
        }

        public T Get()
        {
            T instance;
            if (poolQueue.Count > 0)
            {
                instance = poolQueue.Dequeue();
            }
            else
            {
                instance = Object.Instantiate(prefab, parentTransform);
            }

            instance.gameObject.SetActive(true);
            activeList.Add(instance);
            return instance;
        }

        public void Return(T instance)
        {
            if (instance == null) return;
            instance.gameObject.SetActive(false);
            activeList.Remove(instance);
            poolQueue.Enqueue(instance);
        }

        public void ReturnAll()
        {
            for (int i = activeList.Count - 1; i >= 0; i--)
            {
                T instance = activeList[i];
                instance.gameObject.SetActive(false);
                poolQueue.Enqueue(instance);
            }
            activeList.Clear();
        }
    }
}
