using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectileCollisionDemo.Editor
{
    // PlayMode 안에서만 조건을 바꾸며, 측정 종료 후 Profiler 원본 Sample을 읽는다.
    [InitializeOnLoad]
    public static class PhaseSixAllocationMeasurement
    {
        private const string Key = "PhaseSixAllocationMeasurement";
        private const string Output = "Logs/Phase6A";
        private const int Shots = 1000;
        private static readonly string[] Scenes = { "02_SphereCastAll", "03_SphereCastNonAlloc" };
        private static readonly string[] Markers = { "SphereCastAll Query", "SphereCastNonAlloc Query" };
        private static ProjectileTestRunner runner;
        private static ProjectilePool pool;
        private static int strategy, run, stage, completedFrame;
        private static long expectedQueries;
        private static double deadline;
        private static bool finishing;

        static PhaseSixAllocationMeasurement()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Tools/Projectile Collision Demo/Measure Phase 6-A GC")]
        public static void Begin()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit PlayMode first.");
            if (ProfilerDriver.deepProfiling) throw new InvalidOperationException("Turn Deep Profile OFF first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Output);
            SessionState.SetString(Key + "Scene", SceneManager.GetActiveScene().path);
            SessionState.SetBool(Key + "Profiler", ProfilerDriver.enabled);
            SessionState.SetBool(Key + "ProfileEditor", ProfilerDriver.profileEditor);
            ProfilerDriver.enabled = false;
            EditorSceneManager.OpenScene(ScenePath(0));
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        private static string ScenePath(int index) => "Assets/ProjectileCollisionDemo/Scenes/" + Scenes[index] + ".unity";

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                strategy = 0; run = 0; stage = 0; expectedQueries = 0; finishing = false;
                deadline = EditorApplication.timeSinceStartup + 180;
                File.WriteAllText(Output + "/Results.txt", "Unity " + Application.unityVersion + "; Editor PlayMode; Deep Profile OFF\n");
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.ExitingPlayMode && !finishing) Finish(false, "PlayMode interrupted.");
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                ProfilerDriver.enabled = SessionState.GetBool(Key + "Profiler", false);
                ProfilerDriver.profileEditor = SessionState.GetBool(Key + "ProfileEditor", false);
                string path = SessionState.GetString(Key + "Scene", "");
                if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
            }
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                Finish(false, message + "\n" + stack);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private static void Prepare()
        {
            runner = UnityEngine.Object.FindFirstObjectByType<ProjectileTestRunner>();
            pool = UnityEngine.Object.FindFirstObjectByType<ProjectilePool>();
            Require(runner != null && pool != null, "Runner/Pool missing.");
            Require(Mathf.Approximately(Time.fixedDeltaTime, 0.02f), "Fixed DT must already be 0.02.");
            Require(!runner.UsesOnTrigger && runner.UsesSphereCastAll == (strategy == 0) && runner.UsesSphereCastNonAlloc == (strategy == 1), "Wrong strategy.");
            var config = new SerializedObject(runner.Config);
            config.FindProperty("<ProjectileSpeed>k__BackingField").floatValue = 40;
            config.FindProperty("<ProjectileRadius>k__BackingField").floatValue = 0.05f;
            config.FindProperty("<TargetThickness>k__BackingField").floatValue = 0.2f;
            config.FindProperty("<ShotCount>k__BackingField").intValue = Shots;
            config.FindProperty("<ShotInterval>k__BackingField").floatValue = 0;
            config.ApplyModifiedPropertiesWithoutUndo();
            foreach (var panel in UnityEngine.Object.FindObjectsByType<ProjectileCollisionDebugPanel>(FindObjectsSortMode.None)) panel.enabled = false;
            pool.Prewarm(Shots);
            Require(pool.CreatedCount == Shots, "Unexpected pool size.");
            runner.StartTest();
            stage = 1;
        }

        private static void CheckCompleted()
        {
            Require(runner.FiredCount == Shots && runner.CompletedCount == Shots && runner.DetectedCount == Shots && runner.MissedCount == 0 && runner.DuplicateCount == 0, "Shot results differ.");
            Require(pool.CreatedCount == Shots && pool.ActiveCount == 0, "Pool grew or shots remain active.");
        }

        private static void Tick()
        {
            if (finishing) return;
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Measurement timeout.");
                if (stage == 0) { Prepare(); return; }
                if (stage == 1)
                {
                    if (runner.State != TestState.Complete) return;
                    CheckCompleted();
                    completedFrame = Time.frameCount;
                    stage = 2;
                    return;
                }
                if (stage == 2)
                {
                    if (Time.frameCount < completedFrame + 3) return;
                    runner.ResetTest();
                    ProfilerDriver.ClearAllFrames();
                    ProfilerDriver.profileEditor = false;
                    ProfilerDriver.enabled = true;
                    runner.StartTest();
                    stage = 3;
                    return;
                }
                if (stage == 3)
                {
                    if (runner.State != TestState.Complete) return;
                    CheckCompleted();
                    completedFrame = Time.frameCount;
                    stage = 4;
                    return;
                }
                if (stage == 4)
                {
                    if (Time.frameCount < completedFrame + 3) return;
                    ProfilerDriver.enabled = false;
                    stage = 5;
                    return;
                }
                Analyze();
                run++;
                if (run < 3) { completedFrame = Time.frameCount; stage = 2; return; }
                strategy++;
                if (strategy == Scenes.Length) { Finish(true, "Six captures complete; query counts equal; no PlayMode Error/Exception/Assert received."); return; }
                run = 0;
                EditorSceneManager.LoadSceneInPlayMode(ScenePath(strategy), new LoadSceneParameters(LoadSceneMode.Single));
                stage = 0;
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void Analyze()
        {
            string stem = Output + "/" + Scenes[strategy] + "-Run" + (run + 1);
            Require(ProfilerDriver.SaveProfile(stem + ".data"), "Profiler capture save failed.");
            long queries = 0, bytes = 0, allocations = 0;
            var lines = new List<string> { "Frame | Query count | Query GC bytes | Main-thread GC bytes (includes external allocations)" };
            var distribution = new SortedDictionary<long, long>();
            int queryFrames = 0;
            for (int frame = ProfilerDriver.firstFrameIndex; frame <= ProfilerDriver.lastFrameIndex; frame++)
            {
                using (RawFrameDataView data = ProfilerDriver.GetRawFrameDataView(frame, 0))
                {
                    if (!data.valid) continue;
                    int queryId = data.GetMarkerId(Markers[strategy]);
                    int gcId = data.GetMarkerId("GC.Alloc");
                    long frameQueries = 0, frameBytes = 0, frameGc = 0;
                    for (int sample = 0; sample < data.sampleCount; sample++)
                    {
                        int id = data.GetSampleMarkerId(sample);
                        if (id == gcId && data.GetSampleMetadataCount(sample) > 0) frameGc += data.GetSampleMetadataAsLong(sample, 0);
                        if (id != queryId) continue;
                        frameQueries++;
                        long queryBytes = 0;
                        int end = sample + data.GetSampleChildrenCountRecursive(sample);
                        for (int child = sample + 1; child <= end; child++)
                        {
                            if (data.GetSampleMarkerId(child) != gcId) continue;
                            Require(data.GetSampleMetadataCount(child) > 0, "GC.Alloc byte metadata missing.");
                            queryBytes += data.GetSampleMetadataAsLong(child, 0);
                            allocations++;
                        }
                        frameBytes += queryBytes;
                        distribution.TryGetValue(queryBytes, out long count);
                        distribution[queryBytes] = count + 1;
                    }
                    if (frameQueries == 0) continue;
                    queryFrames++;
                    queries += frameQueries; bytes += frameBytes;
                    lines.Add(frame + " | " + frameQueries + " | " + frameBytes + " | " + frameGc);
                }
            }
            Require(queries >= Shots, "Missing query samples.");
            if (expectedQueries == 0) expectedQueries = queries;
            Require(queries == expectedQueries, "Query counts differ between captures.");
            string summary = Scenes[strategy] + " Run " + (run + 1) + ": shots=" + Shots + "; queries=" + queries + "; queryFrames=" + queryFrames + "; Query GC=" + bytes + " B; GC.Alloc samples=" + allocations + "; poolCreated=" + pool.CreatedCount + "; detected=" + runner.DetectedCount;
            lines.Add(summary);
            foreach (var pair in distribution) lines.Add("Query GC " + pair.Key + " B: " + pair.Value + " queries");
            File.WriteAllLines(stem + ".txt", lines);
            File.AppendAllText(Output + "/Results.txt", string.Join("\n", lines.GetRange(lines.Count - distribution.Count - 1, distribution.Count + 1)) + "\n");
        }

        private static void Finish(bool success, string message)
        {
            if (finishing) return;
            finishing = true;
            ProfilerDriver.enabled = false;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            File.AppendAllText(Output + "/Results.txt", (success ? "PASS: " : "FAIL: ") + message + "\n");
            EditorApplication.isPlaying = false;
        }

        [MenuItem("Tools/Projectile Collision Demo/View Phase 6-A All Run 3 %#F6")]
        public static void ViewAll() => ViewCapture(0);

        [MenuItem("Tools/Projectile Collision Demo/View Phase 6-A NonAlloc Run 3 %#F7")]
        public static void ViewNonAlloc() => ViewCapture(1);

        private static void ViewCapture(int index)
        {
            Require(!EditorApplication.isPlaying, "Exit PlayMode before loading a capture.");
            var window = EditorWindow.GetWindow<ProfilerWindow>();
            ProfilerDriver.enabled = false;
            Require(ProfilerDriver.LoadProfile(Output + "/" + Scenes[index] + "-Run3.data", false), "Capture load failed.");
            for (int frame = ProfilerDriver.firstFrameIndex; frame <= ProfilerDriver.lastFrameIndex; frame++)
            {
                using (var data = ProfilerDriver.GetRawFrameDataView(frame, 0))
                {
                    if (!data.valid) continue;
                    int marker = data.GetMarkerId(Markers[index]);
                    for (int sample = 0; sample < data.sampleCount; sample++)
                    {
                        if (data.GetSampleMarkerId(sample) != marker) continue;
                        window.selectedFrameIndex = frame;
                        window.Repaint();
                        return;
                    }
                }
            }
            throw new InvalidOperationException("No query frame found.");
        }
    }
}
