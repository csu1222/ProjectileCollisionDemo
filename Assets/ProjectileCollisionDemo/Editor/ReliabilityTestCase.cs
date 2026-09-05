namespace ProjectileCollisionDemo.Editor
{
    public readonly struct ReliabilityTestCase
    {
        public string Strategy { get; }
        public float Speed { get; }
        public float TargetThickness { get; }
        public int ShotCount { get; }
        public const float FixedDeltaTime = 0.02f;
        public const float Radius = 0.05f;
        public ReliabilityTestCase(string strategy, float speed, float thickness, int shots)
        { Strategy = strategy; Speed = speed; TargetThickness = thickness; ShotCount = shots; }
        public override string ToString() => $"{Strategy} speed={Speed} thickness={TargetThickness} shots={ShotCount}";
    }
}
