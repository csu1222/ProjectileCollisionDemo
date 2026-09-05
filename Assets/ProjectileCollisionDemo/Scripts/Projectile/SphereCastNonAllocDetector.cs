using UnityEngine;
using Unity.Profiling;

namespace ProjectileCollisionDemo
{
    // Query와 후보 선택을 담당한다. Buffer는 각 Projectile, 결과는 Runner가 소유한다.
    public sealed class SphereCastNonAllocDetector : MonoBehaviour
    {
        private static readonly ProfilerMarker queryMarker = new ProfilerMarker("SphereCastNonAlloc Query");
        [SerializeField] private ProjectileTestRunner runner;
        [SerializeField] private TestTarget target;
        [SerializeField, Tooltip("Target을 포함할 물리 쿼리 레이어입니다.")] private LayerMask targetMask = 1;

        public int BufferCapacity => 16;
        public int LastHitCount { get; private set; }
        // 컴포넌트 lifetime 누계다. 검증에서는 실행 전후 차이를 기록한다.
        public int SaturationSuspectedCount { get; private set; }

        public void Configure(ProjectileTestRunner owner, TestTarget testTarget, LayerMask mask)
        { runner = owner; target = testTarget; targetMask = mask; }

        public bool TryHit(TestProjectile projectile, float radius, float distance)
        {
            if (!isActiveAndEnabled || runner == null || !runner.UsesSphereCastNonAlloc || projectile.IsResolved) return false;
            if (!TryGetNearestHit(projectile, radius, distance, out RaycastHit hit)) return false;
            return runner.ReportHit(projectile.ShotId, hit.collider.GetComponentInParent<TestTarget>().GetInstanceID());
        }

        internal bool TryGetNearestHit(TestProjectile projectile, float radius, float distance, out RaycastHit nearest)
        {
            RaycastHit[] hits = projectile.HitBuffer;
            int hitCount;
            using (queryMarker.Auto())
            {
                hitCount = Physics.SphereCastNonAlloc(projectile.transform.position, radius,
                    projectile.Direction, hits, distance, targetMask, QueryTriggerInteraction.Ignore);
            }
            LastHitCount = hitCount;
            // 포화 시 누락 여부와 전체 후보의 최근접 Hit 포함 여부는 알 수 없다.
            if (hitCount == hits.Length) SaturationSuspectedCount++;
            nearest = default;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                Collider collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(projectile.transform)) continue;
                if ((targetMask.value & (1 << collider.gameObject.layer)) == 0) continue;
                TestTarget candidate = collider.GetComponentInParent<TestTarget>();
                if (candidate == null || candidate != target || hit.distance >= nearestDistance) continue;
                nearest = hit;
                nearestDistance = hit.distance;
            }
            return nearest.collider != null;
        }
    }
}
