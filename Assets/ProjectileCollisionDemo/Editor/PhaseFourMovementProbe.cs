using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.LowLevel;

namespace ProjectileCollisionDemo.Editor
{
    // 검증 동안 FixedUpdate 직전에 관찰하며 종료 시 원래 PlayerLoop를 복구한다.
    public sealed class PhaseFourMovementProbe
    {
        private readonly ProjectilePool pool;
        private readonly ProjectileTestRunner runner;
        private readonly PlayerLoopSystem originalLoop;
        public int MovementChecks { get; private set; }
        public int CastFirstChecks { get; private set; }
        private readonly Dictionary<TestProjectile, Sample> previous = new Dictionary<TestProjectile, Sample>();
        private readonly MethodInfo query = typeof(SphereCastNonAllocDetector).GetMethod("TryGetNearestHit", BindingFlags.Instance | BindingFlags.NonPublic);
        private struct Sample
        {
            public int Id;
            public Vector3 Origin;
            public Vector3 Next;
            public bool Hit;
        }
        public PhaseFourMovementProbe(ProjectileTestRunner owner, ProjectilePool source)
        {
            runner = owner; pool = source;
            originalLoop = PlayerLoop.GetCurrentPlayerLoop();
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type != typeof(UnityEngine.PlayerLoop.FixedUpdate)) continue;
                var systems = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList);
                int index = systems.FindIndex(system => system.type == typeof(UnityEngine.PlayerLoop.FixedUpdate.ScriptRunBehaviourFixedUpdate));
                if (index < 0) throw new Exception("FixedUpdate callback not found");
                systems.Insert(index, new PlayerLoopSystem { type = typeof(PhaseFourMovementProbe), updateDelegate = FixedUpdate });
                loop.subSystemList[i].subSystemList = systems.ToArray();
                PlayerLoop.SetPlayerLoop(loop);
                return;
            }
            throw new Exception("FixedUpdate loop not found");
        }
        public void Dispose() => PlayerLoop.SetPlayerLoop(originalLoop);
        private void FixedUpdate()
        {
            foreach (var pair in previous)
            {
                TestProjectile projectile = pair.Key;
                Sample sample = pair.Value;
                if (projectile.ShotId != sample.Id) continue;
                if (sample.Hit)
                {
                    if (!projectile.IsResolved || projectile.gameObject.activeSelf || projectile.transform.position != sample.Origin)
                        throw new Exception("Cast-first must resolve and return before movement");
                    CastFirstChecks++;
                }
                else
                {
                    if (Vector3.Distance(projectile.transform.position, sample.Next) > 0.0001f)
                        throw new Exception("No-hit movement must equal origin + direction * speed * fixedDeltaTime");
                    MovementChecks++;
                }
            }
            previous.Clear();
            if (runner.State != TestState.Running) return;
            var detector = pool.GetComponent<SphereCastNonAllocDetector>();
            foreach (var projectile in pool.GetComponentsInChildren<TestProjectile>())
            {
                if (!projectile.gameObject.activeSelf || projectile.IsResolved) continue;
                float distance = projectile.Speed * Time.fixedDeltaTime;
                if (projectile.Direction != Vector3.right || Mathf.Abs(projectile.Direction.magnitude - 1f) > 0.0001f)
                    throw new Exception("Normalized +X direction expected");
                object[] args = { projectile, runner.Config.ProjectileRadius, distance, default(RaycastHit) };
                bool hit = detector.enabled && (bool)query.Invoke(detector, args);
                previous[projectile] = new Sample { Id = projectile.ShotId, Origin = projectile.transform.position,
                    Next = projectile.transform.position + projectile.Direction * distance, Hit = hit };
            }
        }
    }
}
