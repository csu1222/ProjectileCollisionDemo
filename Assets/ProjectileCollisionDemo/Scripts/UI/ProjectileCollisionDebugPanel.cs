using UnityEngine;
using UnityEngine.UI;

namespace ProjectileCollisionDemo
{
    // 표시와 조작 요청만 담당하며 Projectile을 참조하지 않는다.
    public sealed class ProjectileCollisionDebugPanel : MonoBehaviour
    {
        [SerializeField] private ProjectileTestRunner runner;
        [SerializeField] private Text status;
        public void Configure(ProjectileTestRunner source, Text label) { runner = source; status = label; }
        public string StatusText => status.text;
        private void Update()
        {
            if (runner == null) return;
            ProjectileTestConfig c = runner.Config;
            status.text = $"Projectile Collision Demo\n{(runner.UsesSphereCastNonAlloc ? "Phase 4" : runner.UsesSphereCastAll ? "Phase 3" : runner.UsesOnTrigger ? "Phase 2" : "Phase 1")}  |  {runner.State}\n\nStrategy: {(runner.UsesSphereCastNonAlloc ? "SphereCastNonAlloc" : runner.UsesSphereCastAll ? "SphereCastAll" : runner.UsesOnTrigger ? "OnTrigger" : "Not Assigned")}\n\nProjectile Speed: {c.ProjectileSpeed:F2} unit/s\nFixed Delta Time: {Time.fixedDeltaTime:F3} s\nTravel Per Tick: {c.ProjectileSpeed * Time.fixedDeltaTime:F3} unit\nProjectile Radius: {c.ProjectileRadius:F3} unit\nTarget Thickness: {c.TargetThickness:F3} unit\n\nShots: {runner.FiredCount} / {c.ShotCount}\nCompleted: {runner.CompletedCount} / {c.ShotCount}\nDetected: {(runner.HasCollisionStrategy ? runner.DetectedCount.ToString() : "N/A")}\nMissed: {(runner.HasCollisionStrategy ? runner.MissedCount.ToString() : "N/A")}\nHit Rate: {(runner.HasCollisionStrategy ? runner.HitRate.ToString("F2") + " %" : "N/A")}\nDuplicate: {(runner.HasCollisionStrategy ? runner.DuplicateCount.ToString() : "N/A")}";
        }
        public void StartTest() => runner.StartTest();
        public void StopTest() => runner.StopTest();
        public void ResetTest() => runner.ResetTest();
        public void SpeedDown() => runner.ChangeSpeed(-1);
        public void SpeedUp() => runner.ChangeSpeed(1);
        public void ThicknessDown() => runner.ChangeThickness(-1);
        public void ThicknessUp() => runner.ChangeThickness(1);
    }
}
