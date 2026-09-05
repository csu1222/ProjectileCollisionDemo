namespace ProjectileCollisionDemo
{
    // 중단된 Shot은 IsResolved=false로 남기며 Miss에 포함하지 않는다.
    public readonly struct ProjectileTestResult
    {
        public int ShotId { get; }
        public bool ReachedEndBoundary { get; }
        public bool WasDetected { get; }
        public int HitCount { get; }
        public bool IsResolved { get; }
        public int DetectedTargetId { get; }
        public ProjectileTestResult(int shotId, bool reachedEndBoundary)
            : this(shotId, reachedEndBoundary, 0, reachedEndBoundary, 0) { }
        public ProjectileTestResult(int shotId, bool reachedEndBoundary, int hitCount, bool isResolved, int targetId)
        {
            ShotId = shotId;
            ReachedEndBoundary = reachedEndBoundary;
            HitCount = hitCount;
            WasDetected = hitCount > 0;
            IsResolved = isResolved;
            DetectedTargetId = targetId;
        }
    }
}
