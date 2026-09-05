using UnityEngine;

namespace ProjectileCollisionDemo
{
    // 진행축인 X 두께만 설정에 맞추고 다른 축의 크기는 보존한다.
    public sealed class TestTarget : MonoBehaviour
    {
        [SerializeField] private ProjectileTestRunner runner;
        public void Configure(ProjectileTestRunner owner) => runner = owner;
        private void OnTriggerEnter(Collider other)
        {
            if (runner == null || !runner.UsesOnTrigger) return;
            TestProjectile projectile = other.GetComponentInParent<TestProjectile>();
            if (projectile == null) return;
            if (runner.ReportHit(projectile.ShotId, GetInstanceID())) projectile.Resolve();
        }
        public void ApplyThickness(float thickness)
        {
            Vector3 scale = transform.localScale;
            scale.x = thickness;
            transform.localScale = scale;
        }
    }
}
