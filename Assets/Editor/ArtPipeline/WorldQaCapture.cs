using System.IO;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// INTERNAL QA: renders the world maps from the gameplay camera at key spots with a hero standing there
    /// (idle pose sampled from its clip), framed exactly like CameraFollow2D, parallax backdrop included.
    /// Output: QA/WorldCaptures/*.png at 1600x900 (16:9 PC) and a 2340x1080 phone-ratio shot.
    /// </summary>
    public static class WorldQaCapture
    {
        public static void CaptureFromCommandLine()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("AdventureKing/QA/Capture World Maps")]
        public static void Capture()
        {
            Directory.CreateDirectory("QA/WorldCaptures");
            foreach (string f in Directory.GetFiles("QA/WorldCaptures", "*.png")) File.Delete(f);
            Shoot(VillageSceneBuilder.ScenePath, TownLayout.Hub.MinX, TownLayout.Hub.MaxX, 14f, new (string, string, Vector2, int, int)[]
            {
                ("hub_01_spawn_plaza", "Swordsman", new Vector2(0f, 0f), 1600, 900),
                ("hub_02_stream_boardwalk", "Mage", new Vector2(-16f, 0f), 1600, 900),
                ("hub_03_smithy_street", "Assassin", new Vector2(-42f, 0f), 1600, 900),
                ("hub_04_bell_terrace", "Archer", new Vector2(2f, 5f), 1600, 900),
                ("hub_05_wind_lookout", "Swordsman", new Vector2(5f, 10f), 1600, 900),
                ("hub_06_rope_bridge", "Mage", new Vector2(23f, 5f), 1600, 900),
                ("hub_07_teahouse_veranda", "Assassin", new Vector2(42f, 5f), 1600, 900),
                ("hub_08_portal", "Archer", new Vector2(63.5f, 0f), 1600, 900),
                ("hub_09_phone_ratio_plaza", "Mage", new Vector2(6f, 0f), 2340, 1080),
            });
            Shoot(VillageSceneBuilder.MeadowScenePath, TownLayout.Meadow.MinX, TownLayout.Meadow.MaxX, 11f, new (string, string, Vector2, int, int)[]
            {
                ("meadow_01_arrival_portal", "Swordsman", new Vector2(-36f, 0f), 1600, 900),
                ("meadow_02_stone_tier", "Archer", new Vector2(22f, 7f), 1600, 900),
            });
            Debug.Log("[WORLDQA] captures written");
        }

        private static void Shoot(string scenePath, float minX, float maxX, float maxCamY, (string name, string hero, Vector2 at, int w, int h)[] shots)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Camera cam = Camera.main;
            var layers = Object.FindObjectsByType<ParallaxLayer>(FindObjectsInactive.Exclude);
            var basePos = new Vector3[layers.Length];
            for (int i = 0; i < layers.Length; i++) basePos[i] = layers[i].transform.position;
            // Show portal prompts in captures so the interaction is visible.
            foreach (var p in Object.FindObjectsByType<PortalGate2D>(FindObjectsInactive.Exclude))
            {
                var prompt = new SerializedObject(p).FindProperty("prompt").objectReferenceValue as GameObject;
                if (prompt) prompt.SetActive(true);
                var label = new SerializedObject(p).FindProperty("promptLabel").objectReferenceValue as UnityEngine.UI.Text;
                if (label) label.text = "▲ / W  ·  Đến " + p.DestinationName;
            }
            foreach (var s in shots)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Heroes/{s.hero}_Visual.prefab");
                var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                hero.transform.position = new Vector3(s.at.x, s.at.y, 0f);
                hero.transform.rotation = Quaternion.Euler(0f, 110f, 0f);
                hero.transform.localScale = Vector3.one * VillageSceneBuilder.HeroScale;
                var anim = hero.GetComponentInChildren<Animator>();
                var ctrl = anim.runtimeAnimatorController;
                anim.runtimeAnimatorController = null;
                AnimationClip idle = System.Array.Find(ctrl.animationClips, c => c.name == "Idle") ?? ctrl.animationClips[0];
                idle.SampleAnimation(anim.gameObject, idle.length * 0.3f);

                float aspect = s.w / (float)s.h, halfW = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 14f * aspect;
                float camX = Mathf.Clamp(s.at.x, minX + Mathf.Max(8f, halfW), maxX - Mathf.Max(8f, halfW));
                float camY = Mathf.Clamp(s.at.y + 0.8f + VillageSceneBuilder.CamOffsetY, VillageSceneBuilder.CamMinY, maxCamY);
                cam.transform.position = new Vector3(camX, camY, cam.transform.position.z);
                for (int i = 0; i < layers.Length; i++)
                    layers[i].transform.position = basePos[i] + new Vector3(camX * layers[i].Follow.x, camY * layers[i].Follow.y, 0f);

                var rt = new RenderTexture(s.w, s.h, 24) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.aspect = aspect;
                cam.Render();
                cam.Render(); // warm-up: the first batchmode render can miss material constants
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"QA/WorldCaptures/{s.name}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                RenderTexture.active = null;
                cam.targetTexture = null;
                cam.ResetAspect();
                rt.Release();
                Object.DestroyImmediate(hero);
            }
        }
    }
}
