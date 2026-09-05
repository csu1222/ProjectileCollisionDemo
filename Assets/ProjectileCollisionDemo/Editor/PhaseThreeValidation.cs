using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectileCollisionDemo.Editor
{
    // 대표 조건 B를 먼저 실행한다. 뒤의 진단용 Scene 변경은 PlayMode에만 존재한다.
    [InitializeOnLoad]
    public static class PhaseThreeValidation
    {
        private static readonly float[] speeds = { 40, 15, 100, 200, 40, 40, 15, 40 };
        private static readonly float[] thicknesses = { 0.2f, 1f, 0.2f, 0.05f, 0.2f, 0.2f, 1f, 0.2f };
        private static readonly string[] names = { "B representative", "A", "C", "D", "Multiple colliders", "No target collider", "Trigger isolation (query disabled)", "Restart representative" };
        private static readonly List<string> records = new List<string>();
        private static int stage;
        private static int caseIndex;
        private static double deadline;
        private static double completeAt;
        private static BoxCollider extraCollider;
        private static PhaseThreeMovementProbe probe;
        private static bool finishing;

        static PhaseThreeValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("PhaseThreeValidate", false)) return;
                records.Clear(); stage = 0; caseIndex = 0; finishing = false;
                deadline = EditorApplication.timeSinceStartup + 240;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            };
        }
        public static void BuildAndValidate()
        {
            try
            {
                if (!File.Exists(PhaseThreeBuilder.ScenePath)) PhaseThreeBuilder.Build();
                else EditorSceneManager.OpenScene(PhaseThreeBuilder.ScenePath);
                SessionState.SetBool("PhaseThreeValidate", true);
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
            Require(runner.Config.ProjectileSpeed == speeds[caseIndex] && Mathf.Approximately(runner.Config.TargetThickness, thicknesses[caseIndex]), "Case configuration");
            Require(Mathf.Approximately(UnityEngine.Object.FindFirstObjectByType<TestTarget>().transform.localScale.x, thicknesses[caseIndex]), "Target thickness");
            Physics.SyncTransforms();
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
                var target = UnityEngine.Object.FindFirstObjectByType<TestTarget>();
                if (runner == null || pool == null || panel == null || pool.CreatedCount == 0) return;
                var detector = pool.GetComponent<SphereCastAllDetector>();
                if (stage == 0)
                {
                    Require(runner.UsesSphereCastAll && !runner.UsesOnTrigger, "SphereCastAll is the sole hit strategy");
                    Require(Mathf.Approximately(Time.fixedDeltaTime, 0.02f) && runner.Config.ShotCount == 100 && Mathf.Approximately(runner.Config.ProjectileRadius, 0.05f) && Mathf.Approximately(runner.Config.ShotInterval, 0.1f), "Shared time/radius/shot configuration");
                    Require(GameObject.Find("SpawnPoint").transform.position == new Vector3(0, 1, 0) && target.transform.position == new Vector3(10, 1, 0) && UnityEngine.Object.FindFirstObjectByType<EndBoundary>().transform.position == new Vector3(12, 1, 0), "Phase 2 positions unchanged");
                    Require(target.gameObject.layer == 0 && !target.GetComponent<BoxCollider>().isTrigger, "Default layer and solid Target");
                    var projectile = pool.Get();
                    var body = projectile.GetComponent<Rigidbody>();
                    Require(body.isKinematic && !body.useGravity && body.collisionDetectionMode == CollisionDetectionMode.Discrete && body.interpolation == RigidbodyInterpolation.None, "Phase 2 Rigidbody unchanged");
                    Require(projectile.gameObject.layer == 0 && projectile.GetComponent<SphereCollider>().isTrigger, "Phase 2 projectile layer/collider unchanged");
                    pool.Return(projectile);
                    foreach (var button in panel.GetComponentsInChildren<Button>())
                        Require(button.onClick.GetPersistentTarget(0) == panel, "Button reference " + button.name);
                    probe = new PhaseThreeMovementProbe(runner, pool);
                    records.Add("Configuration: spawn=(0,1,0); target=(10,1,0); boundary=(12,1,0); dt=0.02; radius=0.05 (explicit sweep); interval=0.1; mask=1 (Default); trigger=Ignore; pool=32; direction=+X.");
                    StartCase(runner, panel); stage = 1;
                }
                else if (stage == 1 && runner.State == TestState.Complete)
                {
                    if (completeAt == 0) { completeAt = EditorApplication.timeSinceStartup; return; }
                    if (EditorApplication.timeSinceStartup - completeAt < 0.15) return;
                    CheckRun(runner, pool, panel);
                    records.Add($"{names[caseIndex]}: Speed={speeds[caseIndex]}; Travel/Tick={speeds[caseIndex] * Time.fixedDeltaTime:F2}; Thickness={thicknesses[caseIndex]}; Shots=100; Detected={runner.DetectedCount}; Missed={runner.MissedCount}; HitRate={runner.HitRate:F2}; Duplicate={runner.DuplicateCount}; Invariant=PASS; Pool=32/0.");
                    if (caseIndex == 0)
                    {
                        Require(probe.MovementChecks > 0 && probe.CastFirstChecks > 0, "Actual fixed-step movement and cast-first sampled");
                        records.Add($"Representative movement samples={probe.MovementChecks}; cast-first samples={probe.CastFirstChecks}; movement formula/origin/radius/direction/distance checked in actual FixedUpdate.");
                    }
                    if (caseIndex == 4) { extraCollider.enabled = false; target.GetComponent<BoxCollider>().enabled = false; }
                    if (caseIndex == 5) { Require(runner.MissedCount == 100, "No target: EndBoundary resolves every shot"); target.GetComponent<BoxCollider>().enabled = true; detector.enabled = false; }
                    if (caseIndex == 6)
                    {
                        Require(runner.MissedCount == 100 && runner.DetectedCount == 0, "OnTrigger cannot report hits in SphereCastAll scene");
                        detector.enabled = true;
                    }
                    caseIndex++;
                    if (caseIndex == 4)
                    {
                        CheckNearestSelection(runner, pool, target, detector);
                        extraCollider = target.gameObject.AddComponent<BoxCollider>();
                        extraCollider.center = new Vector3(0.25f, 0, 0);
                    }
                    if (caseIndex < speeds.Length) StartCase(runner, panel);
                    else { Click(panel, "Reset"); Click(panel, "Start Test"); stage = 2; }
                }
                else if (stage == 2 && runner.FiredCount >= 3)
                {
                    Click(panel, "Stop");
                    Require(runner.State == TestState.Stopped && pool.ActiveCount == 0 && runner.Results.Count == runner.FiredCount, "Stop returns pending shots");
                    Click(panel, "Reset");
                    Require(runner.State == TestState.Idle && runner.Results.Count == 0 && runner.DetectedCount == 0 && runner.MissedCount == 0 && runner.DuplicateCount == 0 && runner.HitRate == 0, "Reset clears results");
                    Click(panel, "Speed -"); Click(panel, "Speed +");
                    Click(panel, "Thickness -"); Click(panel, "Thickness +");
                    Require(runner.Config.ProjectileSpeed == 40 && Mathf.Approximately(runner.Config.TargetThickness, 0.2f), "Reverse setting controls");
                    Click(panel, "Start Test"); stage = 3; completeAt = 0;
                }
                else if (stage == 3 && runner.State == TestState.Complete)
                {
                    if (completeAt == 0) { completeAt = EditorApplication.timeSinceStartup; return; }
                    if (EditorApplication.timeSinceStartup - completeAt < 0.15) return;
                    CheckRun(runner, pool, panel);
                    records.Add("Stop/Reset/restart PASS; all seven button listeners invoked; unique IDs restart at 1; pool remains 32/0; PlayMode error/exception guard passed. Pointer input and rendered Game View are separate checks.");
                    Finish(true, "PHASE3_PLAYMODE_PASS");
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        private static void CheckRun(ProjectileTestRunner runner, ProjectilePool pool, ProjectileCollisionDebugPanel panel)
        {
            Require(runner.FiredCount == 100 && runner.CompletedCount == 100 && runner.Results.Count == 100, "100 completed shots");
            Require(runner.DetectedCount + runner.MissedCount == 100 && runner.DuplicateCount == 0, "Hit/Miss invariant and no duplicate");
            Require(pool.ActiveCount == 0 && pool.CreatedCount == 32, "Pool reused and empty");
            var ids = new HashSet<int>();
            int detected = 0, missed = 0;
            foreach (var result in runner.Results)
            {
                Require(ids.Add(result.ShotId) && result.IsResolved && result.WasDetected != result.ReachedEndBoundary && result.HitCount == (result.WasDetected ? 1 : 0), "Unique exclusive result");
                if (result.WasDetected) detected++; else missed++;
            }
            Require(ids.Contains(1) && ids.Contains(100) && detected == runner.DetectedCount && missed == runner.MissedCount, "Unique IDs 1-100 and totals");
            Require(panel.StatusText.Contains("Strategy: SphereCastAll") && panel.StatusText.Contains("Completed: 100 / 100") && panel.StatusText.Contains("Hit Rate: " + runner.HitRate.ToString("F2")), "UI completed text persists");
        }
        private static void CheckNearestSelection(ProjectileTestRunner runner, ProjectilePool pool, TestTarget target, SphereCastAllDetector detector)
        {
            var projectile = pool.Get();
            var boundary = UnityEngine.Object.FindFirstObjectByType<EndBoundary>();
            var nearer = new GameObject("Phase3 nearest valid child");
            var decoy = new GameObject("Phase3 wrong Target decoy");
            var own = projectile.GetComponent<SphereCollider>();
            try
            {
                projectile.Launch(pool, boundary, -1, new Vector3(8, 1, 0), 40, 0.05f);
                own.isTrigger = false;
                nearer.transform.SetParent(target.transform);
                nearer.transform.position = new Vector3(9.5f, 1, 0);
                var nearCollider = nearer.AddComponent<BoxCollider>();
                nearCollider.size = new Vector3(0.1f, 1, 1);
                decoy.transform.position = new Vector3(9, 1, 0);
                decoy.AddComponent<TestTarget>();
                decoy.AddComponent<BoxCollider>().size = new Vector3(0.1f, 1, 1);
                Physics.SyncTransforms();
                var raw = Physics.SphereCastAll(projectile.transform.position, 0.05f, Vector3.right, 3f, 1, QueryTriggerInteraction.Ignore);
                Require(raw.Length >= 3, "Multiple colliders actually returned");
                var method = typeof(SphereCastAllDetector).GetMethod("TryGetNearestHit", BindingFlags.Instance | BindingFlags.NonPublic);
                object[] args = { projectile, 0.05f, 3f, default(RaycastHit) };
                Require((bool)method.Invoke(detector, args), "Nearest valid hit found");
                RaycastHit selected = (RaycastHit)args[3];
                Require(selected.collider == nearCollider, "Nearest expected Target child wins over self/wrong Target/farther collider");
                float minimum = float.PositiveInfinity;
                foreach (var hit in raw)
                    if (!hit.collider.transform.IsChildOf(projectile.transform) && hit.collider.GetComponentInParent<TestTarget>() == target)
                        minimum = Mathf.Min(minimum, hit.distance);
                Require(Mathf.Abs(selected.distance - minimum) < 0.0001f, "Minimum valid distance independent of result order");
                records.Add($"Separate nearest-hit diagnostic: raw={raw.Length}; selected={selected.collider.name}; distance={selected.distance:F4}; nearest valid Target child selected; self and wrong Target excluded. Diagnostic radius/distance=0.05/3; excluded from representative measurements.");
            }
            finally
            {
                own.isTrigger = true;
                projectile.Resolve();
                UnityEngine.Object.DestroyImmediate(nearer);
                UnityEngine.Object.DestroyImmediate(decoy);
                Physics.SyncTransforms();
            }
        }
        private static void Finish(bool success, string message)
        {
            if (finishing) return;
            finishing = true;
            probe?.Dispose();
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            SessionState.SetBool("PhaseThreeValidate", false);
            records.Add(message);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/PhaseThreeValidation.txt", records);
            Debug.Log(message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
