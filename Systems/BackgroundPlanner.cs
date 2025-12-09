using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace TerrariaOptimizer.Systems
{
    // Lightweight background planner for safe, off-thread computations.
    // Only works on snapshots of primitive data; never touches Terraria types off-thread.
    public static class BackgroundPlanner
    {
        private static volatile int[] _projRemovalPlan;
        private static volatile int _projRemovalCount;
        private static volatile uint _projPlanTick;
        private static int _busyProj; // 0 = idle, 1 = busy

        // Offscreen flags computation (server-side precompute)
        private static int _busyNpcFlags; // 0 = idle, 1 = busy
        private static int _busyProjFlags; // 0 = idle, 1 = busy
        private static volatile bool[] _npcFarFlags;
        private static volatile bool[] _projFarFlags;

        public struct ProjectileSnapshot
        {
            public int index;
            public int timeLeft; // lower means older
            public bool isImportant;
        }

        public struct PlayerSnapshot
        {
            public Vector2 center;
        }

        public struct EntitySnapshot
        {
            public int index;
            public Vector2 center;
        }

        // Schedule an off-thread sort/selection for projectile removals.
        // Inputs are pure snapshots; safe to use off-thread.
        public static void ScheduleProjectileTrim(List<ProjectileSnapshot> snapshots, int toRemove, uint tick)
        {
            if (snapshots == null || snapshots.Count == 0 || toRemove <= 0)
                return;

            if (Interlocked.CompareExchange(ref _busyProj, 1, 0) != 0)
                return; // already computing

            try
            {
                var copy = snapshots.ToArray();
                Task.Run(() =>
                {
                    try
                    {
                        Array.Sort(copy, (a, b) =>
                        {
                            if (a.isImportant && !b.isImportant) return -1;
                            if (!a.isImportant && b.isImportant) return 1;
                            return a.timeLeft.CompareTo(b.timeLeft);
                        });

                        var indices = new List<int>(toRemove);
                        int removed = 0;
                        for (int i = 0; i < copy.Length && removed < toRemove; i++)
                        {
                            if (copy[i].isImportant) continue;
                            indices.Add(copy[i].index);
                            removed++;
                        }

                        _projRemovalPlan = indices.ToArray();
                        _projRemovalCount = removed;
                        _projPlanTick = tick;
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _busyProj, 0);
                    }
                });
            }
            catch
            {
                Interlocked.Exchange(ref _busyProj, 0);
            }
        }

        // Try to consume a computed plan; returns false if not ready or plan is empty.
        public static bool TryConsumeProjectileTrimPlan(out int[] indices)
        {
            indices = null;
            var plan = _projRemovalPlan;
            if (plan == null || plan.Length == 0)
                return false;

            // Reset after consumption
            _projRemovalPlan = null;
            _projRemovalCount = 0;
            indices = plan;
            return true;
        }

        // Schedule offscreen flags for NPCs
        public static void ScheduleOffscreenFlagsForNPCs(List<PlayerSnapshot> players, List<EntitySnapshot> npcs, float threshold)
        {
            if (players == null || players.Count == 0 || npcs == null || npcs.Count == 0)
                return;
            if (Interlocked.CompareExchange(ref _busyNpcFlags, 1, 0) != 0)
                return;
            var pcopy = players.ToArray();
            var ncopy = npcs.ToArray();
            float threshSq = threshold * threshold;
            Task.Run(() =>
            {
                try
                {
                    int maxIndex = 0;
                    for (int i = 0; i < ncopy.Length; i++)
                    {
                        if (ncopy[i].index > maxIndex) maxIndex = ncopy[i].index;
                    }
                    var flags = new bool[Math.Max(1, maxIndex + 1)];
                    for (int i = 0; i < ncopy.Length; i++)
                    {
                        float minSq = float.MaxValue;
                        for (int j = 0; j < pcopy.Length; j++)
                        {
                            float dsq = Vector2.DistanceSquared(ncopy[i].center, pcopy[j].center);
                            if (dsq < minSq) minSq = dsq;
                        }
                        if (minSq > threshSq)
                        {
                            int idx = ncopy[i].index;
                            if (idx >= 0 && idx < flags.Length)
                                flags[idx] = true;
                        }
                    }
                    _npcFarFlags = flags;
                }
                finally
                {
                    Interlocked.Exchange(ref _busyNpcFlags, 0);
                }
            });
        }

        // Schedule offscreen flags for Projectiles
        public static void ScheduleOffscreenFlagsForProjectiles(List<PlayerSnapshot> players, List<EntitySnapshot> projectiles, float threshold)
        {
            if (players == null || players.Count == 0 || projectiles == null || projectiles.Count == 0)
                return;
            if (Interlocked.CompareExchange(ref _busyProjFlags, 1, 0) != 0)
                return;
            var pcopy = players.ToArray();
            var pjc = projectiles.ToArray();
            float threshSq = threshold * threshold;
            Task.Run(() =>
            {
                try
                {
                    int maxIndex = 0;
                    for (int i = 0; i < pjc.Length; i++)
                    {
                        if (pjc[i].index > maxIndex) maxIndex = pjc[i].index;
                    }
                    var flags = new bool[Math.Max(1, maxIndex + 1)];
                    for (int i = 0; i < pjc.Length; i++)
                    {
                        float minSq = float.MaxValue;
                        for (int j = 0; j < pcopy.Length; j++)
                        {
                            float dsq = Vector2.DistanceSquared(pjc[i].center, pcopy[j].center);
                            if (dsq < minSq) minSq = dsq;
                        }
                        if (minSq > threshSq)
                        {
                            int idx = pjc[i].index;
                            if (idx >= 0 && idx < flags.Length)
                                flags[idx] = true;
                        }
                    }
                    _projFarFlags = flags;
                }
                finally
                {
                    Interlocked.Exchange(ref _busyProjFlags, 0);
                }
            });
        }

        public static bool IsNpcFar(int idx)
        {
            var arr = _npcFarFlags;
            return arr != null && idx >= 0 && idx < arr.Length && arr[idx];
        }

        public static bool IsProjectileFar(int idx)
        {
            var arr = _projFarFlags;
            return arr != null && idx >= 0 && idx < arr.Length && arr[idx];
        }
    }
}
