using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectileCollisionDemo.Editor
{
    public static class PhaseTwoBuilder
    {
        public const string ScenePath = ProjectileTestbedBuilder.Root + "/Scenes/01_OnTrigger.unity";
        public static void OpenForInspection()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }
        [MenuItem("Tools/Projectile Collision Demo/Build Phase 2")]
        public static void Build()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists: " + ScenePath);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ProjectileTestbedBuilder.Root + "/Scenes/ProjectileCollisionTestScene.unity");
            var runner = UnityEngine.Object.FindFirstObjectByType<ProjectileTestRunner>();
            var pool = UnityEngine.Object.FindFirstObjectByType<ProjectilePool>();
            string prefabPath = ProjectileTestbedBuilder.Root + "/Prefabs/OnTriggerProjectile.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) throw new InvalidOperationException("Prefab already exists");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectileTestbedBuilder.Root + "/Prefabs/Projectile.prefab");
            var projectile = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                projectile.name = "OnTriggerProjectile";
                var collider = projectile.AddComponent<SphereCollider>();
                collider.radius = 0.5f;
                collider.isTrigger = true;
                var body = projectile.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.isKinematic = true;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.interpolation = RigidbodyInterpolation.None;
                var prefab = PrefabUtility.SaveAsPrefabAsset(projectile, prefabPath);
                pool.Configure(prefab.GetComponent<TestProjectile>());
            }
            finally { UnityEngine.Object.DestroyImmediate(projectile); }
            runner.EnableOnTrigger();
            var target = UnityEngine.Object.FindFirstObjectByType<TestTarget>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            var panel = UnityEngine.Object.FindFirstObjectByType<ProjectileCollisionDebugPanel>();
            var label = panel.transform.Find("Status").GetComponent<Text>();
            label.fontSize = 18;
            label.rectTransform.sizeDelta = new Vector2(395, 445);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label.rectTransform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath, true);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("PHASE2_BUILD_OK");
        }
    }
}
