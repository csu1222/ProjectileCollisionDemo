using UnityEngine;
using Unity.Profiling;

namespace ProjectileCollisionDemo
{
    // Pool의 Projectile이 이동 직전에 호출한다. 결과와 Shot 상태는 Runner가 소유한다.
    public sealed class SphereCastAllDetector : MonoBehaviour
    {
        private static readonly ProfilerMarker queryMarker = new ProfilerMarker("SphereCastAll Query");
        [SerializeField] private ProjectileTestRunner runner;
        [SerializeField] private TestTarget target;
        [SerializeField, Tooltip("Target을 포함할 물리 쿼리 레이어입니다.")] private LayerMask targetMask = 1;

        public void Configure(ProjectileTestRunner owner, TestTarget testTarget, LayerMask mask)
        { runner = owner; target = testTarget; targetMask = mask; }

        public bool TryHit(TestProjectile projectile, float radius, float distance)
        {
            if (!isActiveAndEnabled || runner == null || !runner.UsesSphereCastAll || projectile.IsResolved) return false;
            if (!TryGetNearestHit(projectile, radius, distance, out RaycastHit hit)) return false;
            return runner.ReportHit(projectile.ShotId, hit.collider.GetComponentInParent<TestTarget>().GetInstanceID());
        }

        // radius는 Launch에 전달된 Config 값이며 Collider에서 추출한 반지름이 아니다.
        internal bool TryGetNearestHit(TestProjectile projectile, float radius, float distance, out RaycastHit nearest)
        {
            RaycastHit[] hits;
            using (queryMarker.Auto())
            {
                hits = Physics.SphereCastAll(projectile.transform.position, radius,
                    projectile.Direction, distance, targetMask, QueryTriggerInteraction.Ignore);
            }
            nearest = default;
            float nearestDistance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
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
