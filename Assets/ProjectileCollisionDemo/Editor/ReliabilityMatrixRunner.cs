using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectileCollisionDemo.Editor
{
    // Editor 전용 자동화다. 기존 Scene은 PlayMode에서만 로드하고 설정하며 저장하지 않는다.
    [InitializeOnLoad]
    public static class ReliabilityMatrixRunner
    {
        private const string SessionKey = "ReliabilityMatrixRunning";
        private const string Output = "ReliabilityResults.csv";
        private const string Log = "Logs/PhaseFiveValidation.txt";
        private static readonly string[] strategies = { "OnTrigger", "SphereCastAll", "SphereCastNonAlloc" };
        private static readonly string[] scenes = { "01_OnTrigger", "02_SphereCastAll", "03_SphereCastNonAlloc" };
        private static readonly List<ReliabilityTestCase> cases = new List<ReliabilityTestCase>();
        private static int index, stage, saturationBefore;
        private static double deadline;
        private static float previousTimeScale, previousFixedDeltaTime;
        private static bool finishing;
        private static ProjectileTestRunner runner;
        private static ProjectilePool pool;
        private static TestTarget target;
        private static SphereCastNonAllocDetector detector;
        private static HashSet<int> poolIds;

        static ReliabilityMatrixRunner()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(SessionKey, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) Begin();
                else if (state == PlayModeStateChange.ExitingPlayMode) Finish(false, "PlayMode exited before completion");
            };
        }

        [MenuItem("Tools/Projectile Collision Demo/Run Reliability Matrix")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || SessionState.GetBool(SessionKey, false))
                throw new InvalidOperationException("Exit PlayMode before starting the matrix.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath(0));
            SessionState.SetBool(SessionKey, true);
            EditorApplication.isPlaying = true;
        }

        private static string ScenePath(int strategy) =>
            "Assets/ProjectileCollisionDemo/Scenes/" + scenes[strategy] + ".unity";

        private static void Begin()
        {
            finishing = false; index = 0; stage = 0; runner = null;
            previousTimeScale = Time.timeScale; previousFixedDeltaTime = Time.fixedDeltaTime;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            try
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText(Log, "Phase 5: smoke first; FixedUpdate dt=0.02; interval=0.1; timeScale=20\n");
                File.WriteAllText(Output, ReliabilityResult.Header + "\n");
                cases.Clear();
                foreach (string strategy in strategies) cases.Add(new ReliabilityTestCase(strategy, 40, 0.2f, 100));
                foreach (string strategy in strategies)
                    foreach (float speed in new[] { 15f, 40f, 100f, 200f })
                        foreach (float thickness in new[] { 0.05f, 0.2f, 1f })
                            cases.Add(new ReliabilityTestCase(strategy, speed, thickness, 1000));
                Time.fixedDeltaTime = ReliabilityTestCase.FixedDeltaTime;
                Time.timeScale = 20;
                deadline = EditorApplication.timeSinceStartup + 180;
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void Require(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException(reason); }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Finish(false, message + "\n" + stack);
        }

        private static void Tick()
        {
            if (finishing) return;
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Case timeout: unresolved shots or scene initialization");
                ReliabilityTestCase test = cases[index];
                int strategyIndex = Array.IndexOf(strategies, test.Strategy);
                if (stage == 0)
                {
                    if (SceneManager.GetActiveScene().path != ScenePath(strategyIndex))
                    {
                        runner = null;
                        EditorSceneManager.LoadSceneInPlayMode(ScenePath(strategyIndex), new LoadSceneParameters(LoadSceneMode.Single));
                        return;
                    }
                    runner = UnityEngine.Object.FindFirstObjectByType<ProjectileTestRunner>();
                    pool = UnityEngine.Object.FindFirstObjectByType<ProjectilePool>();
                    target = UnityEngine.Object.FindFirstObjectByType<TestTarget>();
                    if (runner == null || pool == null || target == null || pool.CreatedCount == 0) return;
                    detector = pool.GetComponent<SphereCastNonAllocDetector>();
                    Require(runner.State != TestState.Running, "Unexpected running test");
                    // 비공개 setter를 추가하지 않고 Unity의 기존 직렬화 필드를 PlayMode에서 적용한다.
                    var config = new SerializedObject(runner.Config);
                    SetFloat(config, "ProjectileSpeed", test.Speed);
                    SetFloat(config, "ProjectileRadius", ReliabilityTestCase.Radius);
                    SetFloat(config, "TargetThickness", test.TargetThickness);
                    SetFloat(config, "ShotInterval", 0.1f);
                    config.FindProperty("<ShotCount>k__BackingField").intValue = test.ShotCount;
                    config.ApplyModifiedPropertiesWithoutUndo();
                    runner.ResetTest();
                    Require(runner.State == TestState.Idle && runner.FiredCount == 0 && runner.CompletedCount == 0 &&
                        runner.Results.Count == 0 && runner.DetectedCount == 0 && runner.MissedCount == 0 &&
                        runner.DuplicateCount == 0 && pool.ActiveCount == 0, "Reset did not clear run");
                    CheckConfig(test, strategyIndex);
                    Require(pool.CreatedCount == 32, "Pool prewarm/reuse count differs from baseline");
                    poolIds = new HashSet<int>();
                    foreach (var projectile in pool.GetComponentsInChildren<TestProjectile>(true)) poolIds.Add(projectile.GetInstanceID());
                    Require(poolIds.Count == 32, "Pool instance count");
                    saturationBefore = detector == null ? 0 : detector.SaturationSuspectedCount;
                    Physics.SyncTransforms();
                    runner.StartTest();
                    Require(runner.State == TestState.Running, "StartTest failed");
                    stage = 1;
                    return;
                }
                CheckConfig(test, strategyIndex);
                Require(runner.State == TestState.Running || runner.State == TestState.Complete, "Test interrupted");
                if (runner.State != TestState.Complete) return;
                int wrongTarget = 0, hits = 0, misses = 0, duplicates = 0;
                var ids = new HashSet<int>();
                bool validResults = true;
                foreach (var result in runner.Results)
                {
                    validResults &= ids.Add(result.ShotId) && result.ShotId >= 1 && result.ShotId <= test.ShotCount &&
                        result.IsResolved && result.WasDetected != result.ReachedEndBoundary;
                    if (result.WasDetected)
                    {
                        hits++; duplicates += result.HitCount - 1;
                        if (result.DetectedTargetId != target.GetInstanceID()) wrongTarget++;
                    }
                    else { misses++; validResults &= result.HitCount == 0; }
                }
                var measured = new ReliabilityResult(test, runner, wrongTarget,
                    (detector == null ? 0 : detector.SaturationSuspectedCount) - saturationBefore);
                // 측정값은 검증 실패 시에도 유지한다. Fail 상태와 원인은 별도 실행 로그에 남긴다.
                if (index >= 3) File.AppendAllText(Output, measured.ToCsv() + "\n");
                File.AppendAllText(Log, (index < 3 ? "SMOKE " : "MATRIX ") + measured.ToCsv() + "\n");
                Require(runner.DetectedCount + runner.MissedCount == test.ShotCount, "INVARIANT FAILURE: Detected + Missed != ShotCount");
                Require(runner.FiredCount == test.ShotCount && runner.CompletedCount == test.ShotCount &&
                    runner.Results.Count == test.ShotCount && validResults && hits == runner.DetectedCount &&
                    misses == runner.MissedCount && duplicates == runner.DuplicateCount, "Result/Shot ID integrity failure");
                Require(pool.ActiveCount == 0 && pool.CreatedCount == 32, "Pool not fully returned/reused");
                var afterIds = new HashSet<int>();
                foreach (var projectile in pool.GetComponentsInChildren<TestProjectile>(true)) afterIds.Add(projectile.GetInstanceID());
                Require(poolIds.SetEquals(afterIds), "Pool instances replaced");
                Require(measured.Duplicate == 0 && measured.WrongTarget == 0 && measured.SaturationSuspected == 0,
                    "Unexpected duplicate, wrong target, or saturation; inspect measured CSV");
                if (test.Speed == 40 && Mathf.Approximately(test.TargetThickness, 0.2f))
                    Require(hits == (strategyIndex == 0 ? 0 : test.ShotCount), "Representative result contradicts Phase 2-4 baseline");
                File.AppendAllText(Log, "CASE PASS: " + test + "; reset/unique IDs/pool/invariant verified\n");
                index++;
                if (index == cases.Count)
                {
                    Require(File.ReadAllLines(Output).Length == 37, "CSV must contain header and 36 rows");
                    Finish(true, "PHASE5_PLAYMODE_PASS: 3 smoke + 36 matrix cases; no PlayMode error/exception/assert");
                    return;
                }
                stage = 0;
                deadline = EditorApplication.timeSinceStartup + 180;
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void SetFloat(SerializedObject config, string name, float value) =>
            config.FindProperty("<" + name + ">k__BackingField").floatValue = value;

        private static void CheckConfig(ReliabilityTestCase test, int strategy)
        {
            Require(runner.UsesOnTrigger == (strategy == 0) && runner.UsesSphereCastAll == (strategy == 1) &&
                runner.UsesSphereCastNonAlloc == (strategy == 2), "Scene strategy mismatch");
            Require(runner.Config.ProjectileSpeed == test.Speed && runner.Config.ShotCount == test.ShotCount &&
                Mathf.Approximately(runner.Config.ProjectileRadius, ReliabilityTestCase.Radius) &&
                Mathf.Approximately(runner.Config.TargetThickness, test.TargetThickness) &&
                Mathf.Approximately(target.transform.localScale.x, test.TargetThickness) &&
                Mathf.Approximately(runner.Config.ShotInterval, 0.1f) &&
                Mathf.Approximately(Time.fixedDeltaTime, ReliabilityTestCase.FixedDeltaTime), "Config changed or was not applied");
        }

        private static void Finish(bool success, string message)
        {
            if (finishing) return;
            finishing = true;
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            SessionState.SetBool(SessionKey, false);
            Time.timeScale = previousTimeScale;
            Time.fixedDeltaTime = previousFixedDeltaTime;
            string status = success ? message : "CASE FAIL: " + (index < cases.Count ? cases[index].ToString() : "initialization") + "\n" + message;
            if (!success && runner != null) status += $"\nFired={runner.FiredCount}; Completed={runner.CompletedCount}; Detected={runner.DetectedCount}; Missed={runner.MissedCount}";
            try { File.AppendAllText(Log, status + "\n"); }
            finally
            {
                if (runner != null) runner.StopTest();
                Debug.Log(status);
                if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
                else EditorApplication.isPlaying = false;
            }
        }
    }
}
