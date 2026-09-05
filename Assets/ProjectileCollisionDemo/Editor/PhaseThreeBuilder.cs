using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectileCollisionDemo.Editor
{
    public static class PhaseThreeBuilder
    {
        public const string ScenePath = ProjectileTestbedBuilder.Root + "/Scenes/02_SphereCastAll.unity";
        [MenuItem("Tools/Projectile Collision Demo/Build Phase 3")]
        public static void Build()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists: " + ScenePath);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(PhaseTwoBuilder.ScenePath);
            var runner = UnityEngine.Object.FindFirstObjectByType<ProjectileTestRunner>();
            var pool = UnityEngine.Object.FindFirstObjectByType<ProjectilePool>();
            var target = UnityEngine.Object.FindFirstObjectByType<TestTarget>();
            Undo.RecordObject(runner, "Enable SphereCastAll");
            runner.EnableSphereCastAll();
            var detector = Undo.AddComponent<SphereCastAllDetector>(pool.gameObject);
            detector.Configure(runner, target, 1 << target.gameObject.layer);
            EditorUtility.SetDirty(runner);
            EditorUtility.SetDirty(detector);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath, true);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("PHASE3_BUILD_OK");
        }

        public static void OpenForInspection()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
