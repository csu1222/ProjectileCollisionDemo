using UnityEngine;

namespace ProjectileCollisionDemo
{
    // 공통 실험 변수의 유일한 저장소다. 실행 중 변경은 Runner가 제한한다.
    public sealed class ProjectileTestConfig : MonoBehaviour
    {
        [field: SerializeField, Min(0.01f)] public float ProjectileSpeed { get; private set; } = 40f;
        [field: SerializeField, Min(0.001f)] public float ProjectileRadius { get; private set; } = 0.05f;
        [field: SerializeField, Min(0.001f)] public float TargetThickness { get; private set; } = 0.2f;
        [field: SerializeField, Min(1)] public int ShotCount { get; private set; } = 100;
        [field: SerializeField, Min(0f)] public float ShotInterval { get; private set; } = 0.1f;
        public bool IsValid => float.IsFinite(ProjectileSpeed) && ProjectileSpeed > 0 &&
            float.IsFinite(ProjectileRadius) && ProjectileRadius > 0 &&
            float.IsFinite(TargetThickness) && TargetThickness > 0 && ShotCount > 0 &&
            float.IsFinite(ShotInterval) && ShotInterval >= 0;
        public void StepSpeed(int step) => ProjectileSpeed = Step(ProjectileSpeed, step, new[] { 15f, 40f, 100f, 200f });
        public void StepThickness(int step) => TargetThickness = Step(TargetThickness, step, new[] { 0.05f, 0.2f, 1f });
        private static float Step(float value, int step, float[] values)
        {
            int closest = 0;
            for (int i = 1; i < values.Length; i++)
                if (Mathf.Abs(values[i] - value) < Mathf.Abs(values[closest] - value)) closest = i;
            return values[(closest + (step < 0 ? values.Length - 1 : 1)) % values.Length];
        }
    }
}
