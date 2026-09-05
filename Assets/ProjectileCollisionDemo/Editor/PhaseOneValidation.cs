using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectileCollisionDemo.Editor
{
    // 실제 PlayMode를 두 차례 실행해 결과와 중단 후 재실행을 검증한다.
    [InitializeOnLoad]
    public static class PhaseOneValidation
    {
        private static double deadline;
        private static int stage;
        private static int created;
        private static TestProjectile observed;
        private static Vector3 previousPosition;
        private static float previousFixedTime;
        private static bool movementChecked;
        private static readonly List<string> checks = new List<string>();
        static PhaseOneValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PhaseOneValidate", false))
                {
                    deadline = EditorApplication.timeSinceStartup + 90;
                    EditorApplication.update += Tick;
                }
            };
        }
        public static void BuildAndValidate()
        {
            try
            {
                if (!File.Exists(ProjectileTestbedBuilder.Root + "/Scenes/ProjectileCollisionTestScene.unity")) ProjectileTestbedBuilder.Build();
                else EditorSceneManager.OpenScene(ProjectileTestbedBuilder.Root + "/Scenes/ProjectileCollisionTestScene.unity");
                SessionState.SetBool("PhaseOneValidate", true);
                EditorApplication.isPlaying = true;
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        private static void Require(bool condition, string description)
        { if (!condition) throw new Exception(description); checks.Add(description); }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("PlayMode timeout");
                var runner = UnityEngine.Object.FindFirstObjectByType<ProjectileTestRunner>();
                var pool = UnityEngine.Object.FindFirstObjectByType<ProjectilePool>();
                var panel = UnityEngine.Object.FindFirstObjectByType<ProjectileCollisionDebugPanel>();
                var target = UnityEngine.Object.FindFirstObjectByType<TestTarget>();
                if (runner == null || pool == null || panel == null) return;
                if (stage == 1 && !movementChecked)
                {
                    if (observed != null && observed.gameObject.activeSelf && Time.fixedTime > previousFixedTime)
                    {
                        float expected = 40f * (Time.fixedTime - previousFixedTime);
                        Require(Mathf.Abs(observed.transform.position.x - previousPosition.x - expected) < 0.001f &&
                            observed.transform.position.y == 1f && observed.transform.position.z == 0f &&
                            observed.Direction == Vector3.right, "Observed fixed-step straight movement at speed 40");
                        movementChecked = true;
                    }
                    else
                    {
                        observed = UnityEngine.Object.FindFirstObjectByType<TestProjectile>();
                        if (observed != null) { previousPosition = observed.transform.position; previousFixedTime = Time.fixedTime; }
                    }
                }
                if (stage == 0)
                {
                    Require(Mathf.Approximately(Time.fixedDeltaTime, 0.02f), "Fixed timestep 0.02");
                    Require(runner.Config.ProjectileSpeed == 40 && runner.Config.ShotCount == 100 && Mathf.Approximately(runner.Config.ProjectileRadius, 0.05f), "Default configuration");
                    Require(target.GetComponent<BoxCollider>() != null, "Target BoxCollider exists");
                    foreach (Vector3 point in new[] { new Vector3(0, 1, 0), new Vector3(10, 1, 0), new Vector3(12, 1, 0) })
                    {
                        Vector3 viewport = Camera.main.WorldToViewportPoint(point);
                        Require(viewport.z > 0 && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1, "Path point in camera viewport: " + point);
                    }
                    foreach (var button in panel.GetComponentsInChildren<Button>())
                        Require(button.onClick.GetPersistentTarget(0) == panel, "Button reference: " + button.name);
                    panel.ThicknessUp(); Require(Mathf.Approximately(target.transform.localScale.x, 1f), "Thickness changes geometry");
                    panel.ThicknessDown(); panel.SpeedUp(); Require(runner.Config.ProjectileSpeed == 100, "Speed control"); panel.SpeedDown();
                    panel.StartTest(); stage = 1;
                }
                else if (stage == 1 && runner.State == TestState.Complete)
                {
                    CheckRun(runner, pool);
                    Require(movementChecked, "Movement sampled during PlayMode");
                    if (!panel.StatusText.Contains("Completed: 100 / 100")) return;
                    Require(panel.StatusText.Contains("Travel Per Tick: 0.800") && panel.StatusText.Contains("Target Thickness: 0.200") && panel.StatusText.Contains("Projectile Radius: 0.050"), "UI displayed values");
                    created = pool.CreatedCount;
                    Require(created == 32, "Prewarm 32 reused without growth");
                    panel.ResetTest(); Require(runner.Results.Count == 0 && runner.FiredCount == 0 && pool.ActiveCount == 0, "Reset clears state");
                    panel.StartTest(); stage = 2;
                }
                else if (stage == 2 && runner.FiredCount >= 3)
                {
                    Require(runner.State == TestState.Running, "Waits for outstanding shots");
                    panel.StopTest(); Require(pool.ActiveCount == 0 && runner.State == TestState.Stopped, "Stop returns all projectiles");
                    Require(runner.Results.Count == runner.FiredCount, "Stop records outstanding shots");
                    panel.ResetTest(); panel.StartTest(); stage = 3;
                }
                else if (stage == 3 && runner.State == TestState.Complete)
                {
                    CheckRun(runner, pool);
                    Require(runner.Results[0].ShotId == 1 && pool.CreatedCount == created, "Reset restarts IDs and reuses pool");
                    Directory.CreateDirectory("Logs");
                    File.WriteAllLines("Logs/PhaseOneValidation.txt", checks);
                    Debug.Log("PHASE1_PLAYMODE_PASS\n" + string.Join("\n", checks));
                    SessionState.SetBool("PhaseOneValidate", false);
                    EditorApplication.update -= Tick;
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception e)
            {
                SessionState.SetBool("PhaseOneValidate", false);
                Debug.LogException(e); EditorApplication.update -= Tick; EditorApplication.Exit(1);
            }
        }
        private static void CheckRun(ProjectileTestRunner runner, ProjectilePool pool)
        {
            Require(runner.FiredCount == 100 && runner.CompletedCount == 100 && runner.Results.Count == 100 && pool.ActiveCount == 0, "100 shots completed and returned");
            var ids = new HashSet<int>();
            foreach (var result in runner.Results) Require(result.ReachedEndBoundary && ids.Add(result.ShotId), "Unique boundary result " + result.ShotId);
        }
    }
}
