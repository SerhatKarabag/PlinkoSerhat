using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plinko.Pooling
{
    // Generic object pool for minimizing GC allocations.
    public class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _pool;
        private readonly Func<T> _createFunc;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onDestroy;
        private readonly int _maxSize;

        public int CountInactive => _pool.Count;
        public int CountActive { get; private set; }
        public int CountTotal => CountActive + CountInactive;

        public ObjectPool(
            Func<T> createFunc,
            Action<T> onGet = null,
            Action<T> onRelease = null,
            Action<T> onDestroy = null,
            int defaultCapacity = 10,
            int maxSize = 100)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _onGet = onGet;
            _onRelease = onRelease;
            _onDestroy = onDestroy;
            _maxSize = maxSize;
            _pool = new Stack<T>(defaultCapacity);

            for (int i = 0; i < defaultCapacity; i++)
            {
                var item = _createFunc();
                _onRelease?.Invoke(item);
                _pool.Push(item);
            }
        }

        public T Get()
        {
            T item;

            if (_pool.Count > 0)
            {
                item = _pool.Pop();
            }
            else
            {
                item = _createFunc();
            }

            CountActive++;
            _onGet?.Invoke(item);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;

            _onRelease?.Invoke(item);

            if (CountActive > 0)
            {
                CountActive--;
            }

            if (_pool.Count < _maxSize)
            {
                _pool.Push(item);
            }
            else
            {
                _onDestroy?.Invoke(item);
            }
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var item = _pool.Pop();
                _onDestroy?.Invoke(item);
            }
            CountActive = 0;
        }
    }

    // Specialized pool for Unity GameObjects with component caching.
    public class GameObjectPool
    {
        private readonly Stack<GameObject> _pool;
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly int _maxSize;

        private readonly List<GameObject> _activeObjects;

        public int CountInactive => _pool.Count;
        public int CountActive => _activeObjects.Count;
        public IReadOnlyList<GameObject> ActiveObjects => _activeObjects;

        public GameObjectPool(GameObject prefab, Transform parent, int initialSize = 10, int maxSize = 100)
        {
            _prefab = prefab;
            _parent = parent;
            _maxSize = maxSize;
            _pool = new Stack<GameObject>(initialSize);
            _activeObjects = new List<GameObject>(maxSize);

            for (int i = 0; i < initialSize; i++)
            {
                var obj = CreateNew();
                obj.SetActive(false);
                _pool.Push(obj);
            }
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject obj;

            if (_pool.Count > 0)
            {
                obj = _pool.Pop();
            }
            else
            {
                obj = CreateNew();
            }

            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            _activeObjects.Add(obj);

            return obj;
        }

        public T Get<T>(Vector3 position, Quaternion rotation) where T : Component
        {
            var obj = Get(position, rotation);
            return obj.GetComponent<T>();
        }

        public void Release(GameObject obj)
        {
            if (obj == null) return;

            obj.SetActive(false);
            _activeObjects.Remove(obj);

            if (_pool.Count < _maxSize)
            {
                _pool.Push(obj);
            }
            else
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        public void ReleaseAll()
        {
            for (int i = _activeObjects.Count - 1; i >= 0; i--)
            {
                Release(_activeObjects[i]);
            }
        }

        private GameObject CreateNew()
        {
            var obj = UnityEngine.Object.Instantiate(_prefab, _parent);
            obj.name = $"{_prefab.name}_Pooled";
            return obj;
        }

        public void Clear()
        {
            ReleaseAll();

            while (_pool.Count > 0)
            {
                var obj = _pool.Pop();
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                }
            }
        }
    }
}
