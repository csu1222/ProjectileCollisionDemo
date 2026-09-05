using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectileCollisionDemo.Editor
{
    // 대표 조건만 실제 PlayMode에서 검사한다. 전체 조합 Benchmark가 아니다.
    [InitializeOnLoad]
    public static class PhaseTwoValidation
    {
        private static readonly float[] speeds = { 15, 40, 100, 200 };
        private static readonly float[] thicknesses = { 1, 0.2f, 0.2f, 0.05f };
        private static readonly List<string> records = new List<string>();
        private static int stage;
        private static double deadline;
        private static double completeAt;
        private static int caseIndex;
        static PhaseTwoValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("PhaseTwoValidate", false)) return;
                deadline = EditorApplication.timeSinceStartup + 120;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            };
        }
        public static void BuildAndValidate()
        {
            try
            {
                if (!File.Exists(PhaseTwoBuilder.ScenePath)) PhaseTwoBuilder.Build();
                else EditorSceneManager.OpenScene(PhaseTwoBuilder.ScenePath);
                SessionState.SetBool("PhaseTwoValidate", true);
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Require(bool value, string message)
        { if (!value) throw new Exception(message); }
        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                Finish(false, message + "\n" + stack);
        }
        private static void Click(ProjectileCollisionDebugPanel panel, string name)
        { panel.transform.Find(name).GetComponent<Button>().onClick.Invoke(); }
        private static void StartCase(ProjectileTestRunner runner, ProjectileCollisionDebugPanel panel)
        {
            Click(panel, "Reset");
            for (int i = 0; i < 4 && runner.Config.ProjectileSpeed != speeds[caseIndex]; i++) Click(panel, "Speed +");
            for (int i = 0; i < 3 && !Mathf.Approximately(runner.Config.TargetThickness, thicknesses[caseIndex]); i++) Click(panel, "Thickness +");
            Require(runner.Config.ProjectileSpeed == speeds[caseIndex] && Mathf.Approximately(runner.Config.TargetThickness, thicknesses[caseIndex]), "Case config");
            Require(Mathf.Approximately(UnityEngine.Object.FindFirstObjectByType<TestTarget>().transform.localScale.x, thicknesses[caseIndex]), "Target thickness");
            Click(panel, "Start Test");
            completeAt = 0;
        }
        private static void Tick()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "PlayMode timeout");
                var runner = UnityEngine.Object.FindFirstObjectByType<ProjectileTestRunner>();
                var pool = UnityEngine.Object.FindFirstObjectByType<ProjectilePool>();
                var panel = UnityEngine.Object.FindFirstObjectByType<ProjectileCollisionDebugPanel>();
                if (runner == null || pool == null || panel == null || pool.CreatedCount == 0) return;
                if (stage == 0)
                {
                    Require(runner.UsesOnTrigger && Mathf.Approximately(Time.fixedDeltaTime, 0.02f), "Baseline configuration");
                    var projectile = pool.Get();
                    var body = projectile.GetComponent<Rigidbody>();
                    Require(body.isKinematic && !body.useGravity && body.collisionDetectionMode == CollisionDetectionMode.Discrete, "Discrete kinematic body");
                    Require(projectile.GetComponent<SphereCollider>().isTrigger, "Sphere trigger");
                    pool.Return(projectile);
                    records.Add("Target=" + UnityEngine.Object.FindFirstObjectByType<TestTarget>().transform.position + "; dt=0.02; radius=0.05; interval=0.1");
                    StartCase(runner, panel); stage = 1;
                }
                else if (stage == 1 && runner.State == TestState.Complete)
                {
                    if (completeAt == 0) { completeAt = EditorApplication.timeSinceStartup; return; }
                    if (EditorApplication.timeSinceStartup - completeAt < 0.15) return;
                    Require(runner.FiredCount == 100 && runner.CompletedCount == 100 && runner.Results.Count == 100, "100 completed shots");
                    Require(runner.DetectedCount + runner.MissedCount == 100, "Hit/Miss invariant");
                    Require(pool.ActiveCount == 0 && pool.CreatedCount == 32, "Pool reused and empty");
                    var ids = new HashSet<int>();
                    int detected = 0, missed = 0, duplicate = 0;
                    foreach (var result in runner.Results)
                    {
                        Require(ids.Add(result.ShotId) && result.IsResolved && (result.WasDetected != result.ReachedEndBoundary), "Unique exclusive result");
                        if (result.WasDetected) detected++; else missed++;
                        duplicate += Math.Max(0, result.HitCount - 1);
                    }
                    Require(detected == runner.DetectedCount && missed == runner.MissedCount && duplicate == runner.DuplicateCount, "Result totals");
                    Require(panel.StatusText.Contains("Strategy: OnTrigger") && panel.StatusText.Contains("Completed: 100 / 100") && panel.StatusText.Contains("Hit Rate: " + runner.HitRate.ToString("F2")), "UI result text");
                    records.Add($"Speed={speeds[caseIndex]}; Travel/Tick={speeds[caseIndex] * Time.fixedDeltaTime:F2}; Thickness={thicknesses[caseIndex]}; Shots=100; Detected={detected}; Missed={missed}; HitRate={runner.HitRate:F2}; Duplicate={duplicate}; Invariant=PASS");
                    if (caseIndex == 0)
                    {
                        Require(detected > 0, "Low speed trigger detected");
                        int id = runner.Results[0].ShotId;
                        int targetId = UnityEngine.Object.FindFirstObjectByType<TestTarget>().GetInstanceID();
                        runner.ReportMiss(id);
                        Require(runner.MissedCount == missed, "Hit cannot become Miss");
                        Require(!runner.ReportHit(id, targetId + 1), "Unexpected target rejected");
                        runner.ReportHit(id, targetId); runner.ReportHit(id, targetId);
                        Require(runner.DuplicateCount == duplicate + 2 && runner.DetectedCount == detected && runner.CompletedCount == 100, "Synthetic duplicate reports counted without double resolution");
                        records.Add("Synthetic integrity checks: duplicate +2, unexpected target rejected, Hit then Miss ignored (separate from measured case results).");
                    }
                    caseIndex++;
                    if (caseIndex < speeds.Length) StartCase(runner, panel);
                    else { Click(panel, "Reset"); Click(panel, "Start Test"); stage = 2; }
                }
                else if (stage == 2 && runner.FiredCount >= 3)
                {
                    Click(panel, "Stop");
                    Require(runner.State == TestState.Stopped && pool.ActiveCount == 0 && runner.Results.Count == runner.FiredCount, "Stop returns pending shots");
                    Click(panel, "Reset");
                    Require(runner.State == TestState.Idle && runner.Results.Count == 0 && runner.DetectedCount == 0 && runner.MissedCount == 0 && runner.DuplicateCount == 0 && runner.HitRate == 0, "Reset clears results");
                    caseIndex = 1; StartCase(runner, panel); stage = 3;
                }
                else if (stage == 3 && runner.State == TestState.Complete && pool.ActiveCount == 0)
                {
                    Require(runner.CompletedCount == 100 && runner.DetectedCount + runner.MissedCount == 100 && runner.Results[0].ShotId == 1 && pool.CreatedCount == 32, "Restart IDs and pool reuse");
                    records.Add("Stop/Reset/restart PASS; 100 shots repeated; Pool created=32 active=0; button listeners invoked; pointer input not tested here.");
                    Finish(true, "PHASE2_PLAYMODE_PASS");
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        private static void Finish(bool success, string message)
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            SessionState.SetBool("PhaseTwoValidate", false);
            records.Add(message);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/PhaseTwoValidation.txt", records);
            Debug.Log(message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
