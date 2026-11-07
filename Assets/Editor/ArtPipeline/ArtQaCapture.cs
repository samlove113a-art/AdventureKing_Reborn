using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// INTERNAL QA: renders the real hero prefabs with URP in a temporary lit scene, sampling each
    /// clip at key times (contact frames included), and writes PNG contact sheets to QA/ArtCaptures.
    /// Used to verify orientation, materials, outline, weapon grip and pose quality in-engine.
    /// </summary>
    public static class ArtQaCapture
    {
        private const string OutDir = "QA/ArtCaptures";

        public static void PipelineFromCommandLine()
        {
            try
            {
                RenderPipelineSetup.Setup();
                HeroArtPipeline.RebuildAll();
                CaptureHeroes();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void CaptureFromCommandLine()
        {
            try { CaptureHeroes(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("AdventureKing/QA (internal)/Capture Hero Sheets")]
        public static void CaptureHeroes()
        {
            Directory.CreateDirectory(OutDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.28f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(3, 1, 3);
            var gm = new Material(Shader.Find(HeroArtPipeline.ToonShader));
            gm.SetColor("_BaseColor", new Color(0.55f, 0.68f, 0.5f));
            gm.SetFloat("_OutlineWidth", 0f);
            ground.GetComponent<Renderer>().sharedMaterial = gm;

            var camGo = new GameObject("QACam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.8f, 0.9f);
            cam.fieldOfView = 22f;
            camGo.transform.position = new Vector3(0f, 0.78f, -4.4f);
            camGo.transform.rotation = Quaternion.Euler(6f, 0f, 0f);
            const int cell = 360;
            var rt = new RenderTexture(cell, cell, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;

            foreach (HeroArtDefinitions.Hero h in HeroArtDefinitions.Heroes)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroArtPipeline.PrefabPath(h.Class));
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Animator anim = inst.GetComponentInChildren<Animator>();
                var clips = anim.runtimeAnimatorController.animationClips.ToDictionary(c => c.name);
                // Our PlayableGraph drives the Animator alone; the controller graph would otherwise
                // also evaluate (Idle) and override the sampled pose.
                anim.runtimeAnimatorController = null;
                var shots = new List<(string clip, float t, float yaw)>
                {
                    ("Idle", 0.2f, 200f), ("Idle", 0.2f, 110f), ("Idle", 0.2f, 0f),
                    ("Run", 0.25f, 110f), ("Jump", 0.9f, 110f), ("Climb", 0.3f, 0f),
                };
                foreach (HeroArtDefinitions.Clip c in h.Clips.Where(c => c.Name == "Attack" || c.Name == "Skill"))
                    foreach (var e in c.Events.Where(e => e.fn != "OnFootstep"))
                        shots.Add((c.Name, (e.frame - c.First) / (c.Last - c.First), 110f));
                shots.Add(("Hit", 0.3f, 110f));
                shots.Add(("Death", 1f, 110f));

                var tiles = new List<Texture2D>();
                foreach (var (clipName, t, yaw) in shots)
                {
                    inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    // Edit-mode capture: SampleAnimation writes the imported (root-baked) clip curves to
                    // the bones. (PlayableGraph/Animator evaluation does not write transforms in edit
                    // mode and would only show the FBX default pose.)
                    AnimationClip clip = clips[clipName];
                    clip.SampleAnimation(anim.gameObject, Mathf.Clamp01(t) * clip.length * 0.999f);
                    cam.Render();
                    tiles.Add(ReadRT(rt));
                }
                SaveSheet(tiles, cell, 6, $"{OutDir}/hero_{h.Class}.png");
                Object.DestroyImmediate(inst);
                Debug.Log($"[QA] captured {h.Class}: {string.Join(", ", shots.Select(s => $"{s.clip}@{s.t:0.00}"))}");
            }
            cam.targetTexture = null;
            rt.Release();
        }

        public static Texture2D ReadRT(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        public static void SaveSheet(List<Texture2D> tiles, int cell, int cols, string path)
        {
            int rows = Mathf.CeilToInt(tiles.Count / (float)cols);
            var sheet = new Texture2D(cell * cols, cell * rows, TextureFormat.RGB24, false);
            var fill = Enumerable.Repeat(new Color32(40, 40, 48, 255), sheet.width * sheet.height).ToArray();
            sheet.SetPixels32(fill);
            for (int i = 0; i < tiles.Count; i++)
            {
                int x = (i % cols) * cell, y = (rows - 1 - i / cols) * cell;
                sheet.SetPixels(x, y, cell, cell, tiles[i].GetPixels());
                Object.DestroyImmediate(tiles[i]);
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }
    }
}
