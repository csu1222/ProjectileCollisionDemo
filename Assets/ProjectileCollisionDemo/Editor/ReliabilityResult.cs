using System.Globalization;

namespace ProjectileCollisionDemo.Editor
{
    public readonly struct ReliabilityResult
    {
        public const string Header = "Strategy,Speed,FixedDeltaTime,TravelPerTick,Radius,TargetThickness,ShotCount,Detected,Missed,HitRate,Duplicate,WrongTarget,SaturationSuspected";
        public ReliabilityTestCase Case { get; }
        public int Detected { get; }
        public int Missed { get; }
        public int Duplicate { get; }
        public int WrongTarget { get; }
        public int SaturationSuspected { get; }
        public ReliabilityResult(ReliabilityTestCase testCase, ProjectileTestRunner runner, int wrongTarget, int saturation)
        {
            Case = testCase; Detected = runner.DetectedCount; Missed = runner.MissedCount;
            Duplicate = runner.DuplicateCount; WrongTarget = wrongTarget; SaturationSuspected = saturation;
        }
        public string ToCsv() => string.Format(CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9:F6},{10},{11},{12}",
            Case.Strategy, Case.Speed, ReliabilityTestCase.FixedDeltaTime,
            Case.Speed * ReliabilityTestCase.FixedDeltaTime, ReliabilityTestCase.Radius,
            Case.TargetThickness, Case.ShotCount, Detected, Missed,
            100.0 * Detected / Case.ShotCount, Duplicate, WrongTarget, SaturationSuspected);
    }
}
