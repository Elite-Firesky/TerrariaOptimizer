using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrariaOptimizer.Configs;

namespace TerrariaOptimizer.Systems
{
    public class ObjectPoolManager : ModSystem
    {
        // Generic object pool implementation
        public class ObjectPool<T> where T : class, new()
        {
            private readonly Queue<T> _pool = new Queue<T>();
            private readonly Func<T> _factory;
            private readonly Action<T> _resetAction;
            private readonly int _maxPoolSize;

            public ObjectPool(Func<T> factory = null, Action<T> resetAction = null, int maxPoolSize = 100)
            {
                _factory = factory ?? (() => new T());
                _resetAction = resetAction ?? (_ => { });
                _maxPoolSize = maxPoolSize;
            }

            public T GetObject()
            {
                lock (_pool)
                {
                    if (_pool.Count > 0)
                    {
                        T obj = _pool.Dequeue();
                        _resetAction(obj);
                        DebugUtility.Log($"ObjectPool<{typeof(T).Name}>: Retrieved object from pool, remaining: {_pool.Count}");
                        return obj;
                    }
                }
                DebugUtility.Log($"ObjectPool<{typeof(T).Name}>: Created new object, pool empty");
                return _factory();
            }

            public void ReturnObject(T obj)
            {
                if (obj == null) return;

                lock (_pool)
                {
                    // Don't pool too many objects
                    if (_pool.Count >= _maxPoolSize)
                    {
                        DebugUtility.Log($"ObjectPool<{typeof(T).Name}>: Pool full, discarding object");
                        return;
                    }

                    _resetAction(obj);
                    _pool.Enqueue(obj);
                    DebugUtility.Log($"ObjectPool<{typeof(T).Name}>: Returned object to pool, new count: {_pool.Count}");
                }
            }

            public int Count => _pool.Count;

            public void Clear()
            {
                lock (_pool)
                {
                    int clearedCount = _pool.Count;
                    _pool.Clear();
                    DebugUtility.Log($"ObjectPool<{typeof(T).Name}>: Cleared {clearedCount} objects from pool");
                }
            }
        }

        // Pools for common objects that are frequently created/destroyed
        public static ObjectPool<List<int>> IntListPool { get; private set; }
        public static ObjectPool<List<Vector2>> Vector2ListPool { get; private set; }
        public static ObjectPool<Dictionary<int, int>> IntDictionaryPool { get; private set; }
        public static ObjectPool<List<BackgroundPlanner.PlayerSnapshot>> PlayerSnapshotListPool { get; private set; }
        public static ObjectPool<List<BackgroundPlanner.EntitySnapshot>> EntitySnapshotListPool { get; private set; }
        private int poolDebugCounter = 0;

        public override void Load()
        {
            DebugUtility.LogAlways("ObjectPoolManager loaded");
        }

        public override void Unload()
        {
            // During unload, the mod instance may already be null, so we need to be careful
            try
            {
                if (TerrariaOptimizer.Instance != null)
                {
                    TerrariaOptimizer.Instance.Logger.Info("[TerrariaOptimizer] ObjectPoolManager unloaded");
                }
            }
            catch
            {
                // Silently ignore logging errors during unload
            }

            // Clean up pools
            IntListPool?.Clear();
            Vector2ListPool?.Clear();
            IntDictionaryPool?.Clear();
        }

        public override void OnModLoad()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            if (!config.GarbageCollectionOptimization)
            {
                DebugUtility.Log("ObjectPoolManager: Garbage collection optimization is disabled");
                return;
            }

            // Initialize pools
            IntListPool = new ObjectPool<List<int>>(
                factory: () => new List<int>(),
                resetAction: list => list.Clear(),
                maxPoolSize: 50
            );

            Vector2ListPool = new ObjectPool<List<Vector2>>(
                factory: () => new List<Vector2>(),
                resetAction: list => list.Clear(),
                maxPoolSize: 50
            );

            IntDictionaryPool = new ObjectPool<Dictionary<int, int>>(
                factory: () => new Dictionary<int, int>(),
                resetAction: dict => dict.Clear(),
                maxPoolSize: 30
            );

            PlayerSnapshotListPool = new ObjectPool<List<BackgroundPlanner.PlayerSnapshot>>(
                factory: () => new List<BackgroundPlanner.PlayerSnapshot>(),
                resetAction: list => list.Clear(),
                maxPoolSize: 50
            );

            EntitySnapshotListPool = new ObjectPool<List<BackgroundPlanner.EntitySnapshot>>(
                factory: () => new List<BackgroundPlanner.EntitySnapshot>(),
                resetAction: list => list.Clear(),
                maxPoolSize: 50
            );

            DebugUtility.Log("ObjectPoolManager: Initialized object pools");
        }

        public override void OnModUnload()
        {
            // Clean up pools
            IntListPool?.Clear();
            Vector2ListPool?.Clear();
            IntDictionaryPool?.Clear();
            PlayerSnapshotListPool?.Clear();
            EntitySnapshotListPool?.Clear();
        }

        public override void PreUpdateNPCs()
        {
            poolDebugCounter++;
            if (DebugUtility.IsDebugEnabled() && poolDebugCounter % 300 == 0)
            {
                int intListCount = IntListPool?.Count ?? 0;
                int vec2ListCount = Vector2ListPool?.Count ?? 0;
                int intDictCount = IntDictionaryPool?.Count ?? 0;
                int playerSnapLists = PlayerSnapshotListPool?.Count ?? 0;
                int entitySnapLists = EntitySnapshotListPool?.Count ?? 0;
                DebugUtility.Log($"ObjectPoolManager Summary: intLists={intListCount}, vector2Lists={vec2ListCount}, intDicts={intDictCount}, playerSnapLists={playerSnapLists}, entitySnapLists={entitySnapLists}");
            }
        }

        // Helper methods for using pools
        public static List<int> GetIntList()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.GarbageCollectionOptimization)
                return new List<int>();
            // Be defensive in case pools aren't initialized yet
            return IntListPool?.GetObject() ?? new List<int>();
        }

        public static void ReturnIntList(List<int> list)
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.GarbageCollectionOptimization)
                return;
            // Be defensive in case pools aren't initialized yet
            IntListPool?.ReturnObject(list);
        }

        public static List<Vector2> GetVector2List()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.GarbageCollectionOptimization)
                return new List<Vector2>();
            // Be defensive in case pools aren't initialized yet
            return Vector2ListPool?.GetObject() ?? new List<Vector2>();
        }

        public static void ReturnVector2List(List<Vector2> list)
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.GarbageCollectionOptimization)
                return;
            // Be defensive in case pools aren't initialized yet
            Vector2ListPool?.ReturnObject(list);
        }

        public static List<BackgroundPlanner.PlayerSnapshot> GetPlayerSnapshotList()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.GarbageCollectionOptimization)
                return new List<BackgroundPlanner.PlayerSnapshot>();
            return PlayerSnapshotListPool?.GetObject() ?? new List<BackgroundPlanner.PlayerSnapshot>();
        }

        public static void ReturnPlayerSnapshotList(List<BackgroundPlanner.PlayerSnapshot> list)
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.GarbageCollectionOptimization)
                return;
            PlayerSnapshotListPool?.ReturnObject(list);
        }

        public static List<BackgroundPlanner.EntitySnapshot> GetEntitySnapshotList()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.GarbageCollectionOptimization)
                return new List<BackgroundPlanner.EntitySnapshot>();
            return EntitySnapshotListPool?.GetObject() ?? new List<BackgroundPlanner.EntitySnapshot>();
        }

        public static void ReturnEntitySnapshotList(List<BackgroundPlanner.EntitySnapshot> list)
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.GarbageCollectionOptimization)
                return;
            EntitySnapshotListPool?.ReturnObject(list);
        }
    }
}
