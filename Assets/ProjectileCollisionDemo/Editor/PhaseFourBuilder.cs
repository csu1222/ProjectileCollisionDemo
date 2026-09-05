using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectileCollisionDemo.Editor
{
    public static class PhaseFourBuilder
    {
        public const string ScenePath = ProjectileTestbedBuilder.Root + "/Scenes/03_SphereCastNonAlloc.unity";
        [MenuItem("Tools/Projectile Collision Demo/Build Phase 4")]
        public static void Build()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists: " + ScenePath);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(PhaseThreeBuilder.ScenePath);
            var runner = UnityEngine.Object.FindFirstObjectByType<ProjectileTestRunner>();
            var pool = UnityEngine.Object.FindFirstObjectByType<ProjectilePool>();
            var target = UnityEngine.Object.FindFirstObjectByType<TestTarget>();
            Undo.RecordObject(runner, "Enable SphereCastNonAlloc");
            runner.EnableSphereCastNonAlloc();
            Undo.DestroyObjectImmediate(pool.GetComponent<SphereCastAllDetector>());
            var detector = Undo.AddComponent<SphereCastNonAllocDetector>(pool.gameObject);
            detector.Configure(runner, target, 1 << target.gameObject.layer);
            EditorUtility.SetDirty(runner);
            EditorUtility.SetDirty(detector);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath, true);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("PHASE4_BUILD_OK");
        }

        public static void OpenForInspection()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
