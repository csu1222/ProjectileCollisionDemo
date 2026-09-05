using System.Collections.Generic;
using UnityEngine;

namespace ProjectileCollisionDemo
{
    public enum TestState { Idle, Running, Complete, Stopped }

    // 실행 상태, Shot ID와 결과의 소유자다. UI는 이 상태를 읽기만 한다.
    [DefaultExecutionOrder(-100)]
    public sealed class ProjectileTestRunner : MonoBehaviour
    {
        [field: SerializeField] public ProjectileTestConfig Config { get; private set; }
        [SerializeField] private ProjectileLauncher launcher;
        [SerializeField] private ProjectilePool pool;
        [SerializeField] private EndBoundary boundary;
        [SerializeField] private TestTarget target;
        [field: SerializeField] public bool UsesOnTrigger { get; private set; }
        [field: SerializeField] public bool UsesSphereCastAll { get; private set; }
        [field: SerializeField] public bool UsesSphereCastNonAlloc { get; private set; }
        public bool HasCollisionStrategy => UsesOnTrigger || UsesSphereCastAll || UsesSphereCastNonAlloc;
        private readonly Dictionary<int, int> resultIndices = new Dictionary<int, int>();
        private readonly List<ProjectileTestResult> results = new List<ProjectileTestResult>();
        private readonly HashSet<int> pending = new HashSet<int>();
        private int nextShotId = 1;
        private double elapsed;
        public TestState State { get; private set; }
        public int FiredCount { get; private set; }
        public int CompletedCount { get; private set; }
        public int DetectedCount { get; private set; }
        public int MissedCount { get; private set; }
        public int DuplicateCount { get; private set; }
        public float HitRate => CompletedCount == 0 ? 0f : 100f * DetectedCount / CompletedCount;
        public void EnableOnTrigger() { UsesOnTrigger = true; UsesSphereCastAll = false; UsesSphereCastNonAlloc = false; target.Configure(this); }
        public void EnableSphereCastAll() { UsesOnTrigger = false; UsesSphereCastAll = true; UsesSphereCastNonAlloc = false; }
        public void EnableSphereCastNonAlloc() { UsesOnTrigger = false; UsesSphereCastAll = false; UsesSphereCastNonAlloc = true; }
        public IReadOnlyList<ProjectileTestResult> Results => results.AsReadOnly();
        public void Configure(ProjectileTestConfig config, ProjectileLauncher source, ProjectilePool owner, EndBoundary end, TestTarget testTarget)
        { Config = config; launcher = source; pool = owner; boundary = end; target = testTarget; }
        private void OnEnable() { if (boundary != null) boundary.Reached += OnReached; }
        private void OnDisable()
        {
            if (boundary != null) boundary.Reached -= OnReached;
            StopTest();
        }
        private void Start() { pool.Prewarm(32); ApplyTarget(); }
        public void StartTest()
        {
            if (State == TestState.Running) return;
            if (Config == null || !Config.IsValid || !launcher.IsValid)
            { Debug.LogError("실험 설정 또는 참조가 유효하지 않습니다.", this); return; }
            ClearRun();
            ApplyTarget();
            State = TestState.Running;
        }
        private void FixedUpdate()
        {
            if (State != TestState.Running) return;
            // 누적 시간과 예정 발사 시각을 비교해 FixedUpdate 반올림 오차의 누적을 피한다.
            while (FiredCount < Config.ShotCount && elapsed + 0.000001 >= FiredCount * (double)Config.ShotInterval)
            {
                int id = nextShotId++;
                pending.Add(id);
                FiredCount++;
                launcher.Fire(id, Config.ProjectileSpeed, Config.ProjectileRadius);
            }
            elapsed += Time.fixedDeltaTime;
        }
        private void OnReached(int id)
        {
            ReportMiss(id);
        }
        public bool ReportHit(int id, int targetId)
        {
            if (!HasCollisionStrategy || target == null || targetId != target.GetInstanceID()) return false;
            if (resultIndices.TryGetValue(id, out int index))
            {
                ProjectileTestResult previous = results[index];
                if (!previous.WasDetected) return false;
                results[index] = new ProjectileTestResult(id, false, previous.HitCount + 1, true, targetId);
                DuplicateCount++;
                return true;
            }
            if (State != TestState.Running || !pending.Remove(id)) return false;
            resultIndices.Add(id, results.Count);
            results.Add(new ProjectileTestResult(id, false, 1, true, targetId));
            DetectedCount++;
            CompleteShot();
            return true;
        }
        public void ReportMiss(int id)
        {
            if (!pending.Remove(id)) return;
            resultIndices.Add(id, results.Count);
            results.Add(new ProjectileTestResult(id, true));
            if (HasCollisionStrategy) MissedCount++;
            CompleteShot();
        }
        private void CompleteShot()
        {
            CompletedCount++;
            if (FiredCount == Config.ShotCount && pending.Count == 0) State = TestState.Complete;
        }
        public void StopTest()
        {
            if (State != TestState.Running) return;
            State = TestState.Stopped;
            foreach (int id in pending) results.Add(new ProjectileTestResult(id, false));
            pending.Clear();
            pool.ReturnAll();
        }
        public void ResetTest() { StopTest(); ClearRun(); nextShotId = 1; State = TestState.Idle; ApplyTarget(); }
        private void ClearRun()
        {
            pool.ReturnAll(); pending.Clear(); results.Clear();
            resultIndices.Clear(); DetectedCount = 0; MissedCount = 0; DuplicateCount = 0;
            FiredCount = 0; CompletedCount = 0; elapsed = 0;
        }
        public void ChangeSpeed(int step) { if (State != TestState.Running) Config.StepSpeed(step); }
        public void ChangeThickness(int step)
        { if (State != TestState.Running) { Config.StepThickness(step); ApplyTarget(); } }
        private void ApplyTarget() { if (target != null && Config != null) target.ApplyThickness(Config.TargetThickness); }
    }
}
