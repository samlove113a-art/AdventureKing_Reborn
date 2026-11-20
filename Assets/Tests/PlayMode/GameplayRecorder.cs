using System.IO;
using UnityEngine;

namespace AdventureKing.Tests
{
    /// <summary>
    /// INTERNAL QA: records what the player would see (world camera + HUD/dialog/loading UI) at a fixed 30 fps
    /// game clock (Time.captureFramerate), so the encoded video plays back at true speed even though batchmode
    /// renders faster or slower than real time. Frames are JPGs in a temp folder; Tools/blender/encode_video.py
    /// turns them into an MP4 in QA/Video.
    /// </summary>
    public sealed class GameplayRecorder : MonoBehaviour
    {
        public const int Fps = 30;
        private string dir;
        private int frame;
        private RenderTexture rt;
        private Texture2D buffer;
        private int width = 960, height = 540;

        public string Directory => dir;
        public int Frames => frame;

        public static GameplayRecorder Begin(string name, int w, int h)
        {
            var r = Begin(name);
            r.rt.Release();
            Destroy(r.buffer);
            r.width = w; r.height = h;
            r.rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            r.buffer = new Texture2D(w, h, TextureFormat.RGB24, false);
            return r;
        }

        public static GameplayRecorder Begin(string name)
        {
            var go = new GameObject("QA_GameplayRecorder");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<GameplayRecorder>();
            r.dir = Path.Combine(Path.GetTempPath(), "ak_video_" + name);
            if (System.IO.Directory.Exists(r.dir)) System.IO.Directory.Delete(r.dir, true);
            System.IO.Directory.CreateDirectory(r.dir);
            r.rt = new RenderTexture(r.width, r.height, 24) { antiAliasing = 2 };
            r.buffer = new Texture2D(r.width, r.height, TextureFormat.RGB24, false);
            Time.captureFramerate = Fps;
            return r;
        }

        public void End()
        {
            Time.captureFramerate = 0;
            if (rt) rt.Release();
            if (buffer) Destroy(buffer);
            Destroy(gameObject);
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (!cam) return;
            Capture(cam, rt);
            RenderTexture.active = rt;
            buffer.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            buffer.Apply(false);
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, $"f{frame:00000}.jpg"), buffer.EncodeToJPG(82));
            frame++;
        }

        /// <summary>Renders the camera plus every overlay canvas (temporarily moved into camera space).</summary>
        public static void Capture(Camera cam, RenderTexture target)
        {
            var overlays = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) overlays.Add(c);
            float aspect = cam.aspect;
            cam.targetTexture = target;
            cam.aspect = target.width / (float)target.height;
            int mask = cam.cullingMask;
            cam.cullingMask = mask | (1 << 5);
            foreach (Canvas c in overlays)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f;
            }
            Canvas.ForceUpdateCanvases();
            // batchmode has no regular frame render: skin with the bones as they are NOW (after LateUpdate poses such
            // as PoseOverlay's cake cut / kiss lean), not a matrix palette cached from the Animator pass
            var skins = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude);
            foreach (var s in skins) s.forceMatrixRecalculationPerRender = true;
            cam.Render();
            foreach (var s in skins) if (s) s.forceMatrixRecalculationPerRender = false;
            foreach (Canvas c in overlays) c.renderMode = RenderMode.ScreenSpaceOverlay;
            cam.cullingMask = mask;
            cam.targetTexture = null;
            cam.aspect = aspect;
        }
    }
}
