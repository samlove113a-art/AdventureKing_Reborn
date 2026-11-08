using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// INTERNAL QA: renders AK_Village from the gameplay camera at several points along the map with a hero
    /// standing there (idle pose sampled from its clip). Output: QA/WorldCaptures/*.png.
    /// </summary>
    public static class WorldQaCapture
    {
        public static void CaptureFromCommandLine()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("AdventureKing/QA/Capture Village")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(VillageSceneBuilder.ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory("QA/WorldCaptures");
            Camera cam = Camera.main;
            var shots = new (string name, string hero, Vector2 at)[]
            {
                ("01_spawn_swordsman", "Swordsman", new Vector2(0f, 0f)),
                ("02_ladder_mage", "Mage", new Vector2(10.5f, 0f)),
                ("03_bridge_assassin", "Assassin", new Vector2(25f, 4f)),
                ("04_high_archer", "Archer", new Vector2(40f, 8f)),
                ("05_portal_swordsman", "Swordsman", new Vector2(62f, 0f)),
            };
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            // Batchmode: the very first render can sample unbound material constants; warm up twice.
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            cam.targetTexture = null;
            foreach (var s in shots)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Heroes/{s.hero}_Visual.prefab");
                var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                hero.transform.position = new Vector3(s.at.x, s.at.y, 0f);
                hero.transform.rotation = Quaternion.Euler(0f, 110f, 0f);
                hero.transform.localScale = Vector3.one * 1.55f;
                var anim = hero.GetComponentInChildren<Animator>();
                var ctrl = anim.runtimeAnimatorController;
                AnimationClip idle = System.Array.Find(ctrl.animationClips, c => c.name == "Idle") ?? ctrl.animationClips[0];
                idle.SampleAnimation(anim.gameObject, idle.length * 0.3f);

                // Same framing as CameraFollow2D in the scene (player root is 0.8 above the feet, offset 1.8).
                float camX = Mathf.Clamp(s.at.x, -24f, 64f), camY = Mathf.Clamp(s.at.y + 0.8f + 1.8f, 2.6f, 11f);
                cam.transform.position = new Vector3(camX, camY, cam.transform.position.z);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"QA/WorldCaptures/{s.name}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(hero);
            }
            rt.Release();
            Debug.Log("[WORLDQA] captures written");
        }
    }
}
