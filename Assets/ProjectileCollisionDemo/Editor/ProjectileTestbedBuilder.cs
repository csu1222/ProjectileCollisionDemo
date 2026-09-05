using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectileCollisionDemo.Editor
{
    // Scene/Prefabは必ずEditor APIで生成し、既存アセットは上書きしない。
    public static class ProjectileTestbedBuilder
    {
        public const string Root = "Assets/ProjectileCollisionDemo";
        [MenuItem("Tools/Projectile Collision Demo/Build Phase 1")]
        public static void Build()
        {
            string scenePath = Root + "/Scenes/ProjectileCollisionTestScene.unity";
            if (File.Exists(scenePath)) throw new InvalidOperationException("Scene already exists: " + scenePath);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string folder in new[] { "Scenes", "Prefabs", "Materials" })
                if (!AssetDatabase.IsValidFolder(Root + "/" + folder)) AssetDatabase.CreateFolder(Root, folder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material blue = Material("Projectile", new Color(0.1f, 0.8f, 1f));
            Material orange = Material("Target", new Color(1f, 0.45f, 0.12f));
            Material gray = Material("Ground", new Color(0.16f, 0.21f, 0.28f));
            Material green = Material("Boundary", new Color(0.2f, 0.95f, 0.5f));
            GameObject projectileObject = Primitive("Projectile", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.1f, blue);
            UnityEngine.Object.DestroyImmediate(projectileObject.GetComponent<Collider>());
            projectileObject.AddComponent<TestProjectile>();
            projectileObject.SetActive(false);
            TestProjectile template = PrefabUtility.SaveAsPrefabAsset(projectileObject, Root + "/Prefabs/Projectile.prefab").GetComponent<TestProjectile>();
            UnityEngine.Object.DestroyImmediate(projectileObject);
            GameObject environment = new GameObject("Environment");
            GameObject targetObject = Primitive("Target", PrimitiveType.Cube, new Vector3(10, 1, 0), new Vector3(0.2f, 2, 2), orange);
            targetObject.AddComponent<TestTarget>();
            GameObject targetPrefab = PrefabUtility.SaveAsPrefabAsset(targetObject, Root + "/Prefabs/Target.prefab");
            UnityEngine.Object.DestroyImmediate(targetObject);
            TestTarget target = ((GameObject)PrefabUtility.InstantiatePrefab(targetPrefab)).GetComponent<TestTarget>();
            target.transform.SetParent(environment.transform);
            GameObject endObject = Primitive("EndBoundary", PrimitiveType.Cube, new Vector3(12, 1, 0), new Vector3(0.04f, 2, 2), green);
            UnityEngine.Object.DestroyImmediate(endObject.GetComponent<Collider>());
            endObject.transform.SetParent(environment.transform);
            EndBoundary end = endObject.AddComponent<EndBoundary>();
            Primitive("Ground", PrimitiveType.Cube, new Vector3(6, -0.15f, 0), new Vector3(16, 0.2f, 4), gray).transform.SetParent(environment.transform);
            Primitive("LauncherVisual", PrimitiveType.Cube, new Vector3(-0.55f, 1, 0), new Vector3(0.7f, 0.4f, 0.4f), blue).transform.SetParent(environment.transform);
            Transform spawn = new GameObject("SpawnPoint").transform;
            spawn.position = new Vector3(0, 1, 0);
            GameObject systems = new GameObject("Systems");
            ProjectilePool pool = Child("ProjectilePool", systems.transform).AddComponent<ProjectilePool>();
            pool.Configure(template);
            ProjectileLauncher launcher = Child("ProjectileLauncher", systems.transform).AddComponent<ProjectileLauncher>();
            launcher.Configure(pool, spawn, end);
            GameObject runnerObject = Child("ProjectileTestRunner", systems.transform);
            ProjectileTestConfig config = runnerObject.AddComponent<ProjectileTestConfig>();
            ProjectileTestRunner runner = runnerObject.AddComponent<ProjectileTestRunner>();
            runner.Configure(config, launcher, pool, end, target);
            Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(6, 7, -16);
            camera.transform.LookAt(new Vector3(6, 0.7f, 0));
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.rect = new Rect(0.34f, 0, 0.66f, 1);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.09f);
            Light light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            GameObject panelObject = new GameObject("ProjectileCollisionDebugPanel", typeof(RectTransform), typeof(Image), typeof(ProjectileCollisionDebugPanel));
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(0.34f, 1);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            panelObject.GetComponent<Image>().color = new Color(0.07f, 0.1f, 0.16f);
            Text status = Label("Status", panelObject.transform, "", 20);
            Place(status.rectTransform, 20, 20, 395, 430);
            ProjectileCollisionDebugPanel panel = panelObject.GetComponent<ProjectileCollisionDebugPanel>();
            panel.Configure(null, status);
            Button(panelObject.transform, "Start Test", 20, 475, panel.StartTest);
            Button(panelObject.transform, "Stop", 155, 475, panel.StopTest);
            Button(panelObject.transform, "Reset", 290, 475, panel.ResetTest);
            Button(panelObject.transform, "Speed -", 20, 535, panel.SpeedDown);
            Button(panelObject.transform, "Speed +", 155, 535, panel.SpeedUp);
            Button(panelObject.transform, "Thickness -", 20, 595, panel.ThicknessDown);
            Button(panelObject.transform, "Thickness +", 155, 595, panel.ThicknessUp);
            GameObject panelPrefab = PrefabUtility.SaveAsPrefabAsset(panelObject, Root + "/Prefabs/ProjectileCollisionDebugPanel.prefab");
            UnityEngine.Object.DestroyImmediate(panelObject);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, canvasObject.transform);
            instance.GetComponent<ProjectileCollisionDebugPanel>().Configure(runner, instance.GetComponentInChildren<Text>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.GetComponent<ProjectileCollisionDebugPanel>());
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("PHASE1_BUILD_OK");
        }
        private static Material Material(string name, Color color)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            AssetDatabase.CreateAsset(material, Root + "/Materials/" + name + ".mat");
            return material;
        }
        private static GameObject Child(string name, Transform parent)
        { GameObject child = new GameObject(name); child.transform.SetParent(parent); return child; }
        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(type); obj.name = name;
            obj.transform.position = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
        }
        private static Text Label(string name, Transform parent, string value, int size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text text = obj.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = size; text.color = Color.white;
            text.raycastTarget = false; return text;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private static void Button(Transform parent, string title, float x, float y, UnityAction action)
        {
            GameObject obj = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false); Place(obj.GetComponent<RectTransform>(), x, y, 125, 44);
            obj.GetComponent<Image>().color = new Color(0.16f, 0.29f, 0.43f);
            UnityEventTools.AddPersistentListener(obj.GetComponent<Button>().onClick, action);
            Text label = Label("Label", obj.transform, title, 17);
            label.alignment = TextAnchor.MiddleCenter; Place(label.rectTransform, 0, 0, 125, 44);
        }
    }
}
