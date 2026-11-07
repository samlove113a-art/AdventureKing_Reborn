using System.IO;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AdventureKing.QA.Editor
{
    /// <summary>
    /// INTERNAL QA ONLY. Generates Assets/QA_Internal/Scenes/QA_GameplaySandbox.unity, a grey-box
    /// sandbox wired per Docs/UNITY_HANDOFF.md. The scene is deliberately NOT added to Build Settings
    /// and uses flat-colour placeholders: it is a mechanics test bench, not the mushroom village.
    /// </summary>
    public static class QaSceneBuilder
    {
        public const string Root = "Assets/QA_Internal";
        public const string ScenePath = Root + "/Scenes/QA_GameplaySandbox.unity";
        public const string SpritePath = Root + "/Art/qa_placeholder_white.png";
        public const string MaterialPath = Root + "/Art/QA_PlayerNoFriction.physicsMaterial2D";

        [MenuItem("AdventureKing/QA (internal)/Rebuild Gameplay Sandbox Scene")]
        public static void BuildMenu()
        {
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        // Unity -batchmode -executeMethod AdventureKing.QA.Editor.QaSceneBuilder.BuildFromCommandLine
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log("[QA] Sandbox scene generated: " + ScenePath);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            EnsureLayers();
            Sprite sprite = EnsureSprite();
            PhysicsMaterial2D noFriction = EnsureMaterial();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- Level geometry (grey-box) -------------------------------------------------------
            var level = new GameObject("QA_Level").transform;
            QaWorldFactory.CreateGround("Ground_Main", new Vector2(5f, -0.5f), new Vector2(42f, 1f), sprite, level);
            QaWorldFactory.CreateGround("Platform_Low", new Vector2(4f, 1.25f), new Vector2(4f, 0.5f), sprite, level);
            QaWorldFactory.CreateGround("Platform_High", new Vector2(9.5f, 2.75f), new Vector2(3.5f, 0.5f), sprite, level);
            QaWorldFactory.CreateGround("Platform_LadderTop_OneWay", new Vector2(-4.5f, 4.25f), new Vector2(4f, 0.5f), sprite, level, oneWay: true);
            QaWorldFactory.CreateWall("Wall_Left", new Vector2(-16.5f, 4f), new Vector2(1f, 10f), sprite, level);
            QaWorldFactory.CreateWall("Wall_Right", new Vector2(26.5f, 4f), new Vector2(1f, 10f), sprite, level);
            QaWorldFactory.CreateLadder(new Vector2(-6f, 2.6f), new Vector2(0.8f, 5.2f), sprite).transform.SetParent(level, true);

            // ---- Actors ------------------------------------------------------------------------
            QaWorldFactory.PlayerRig player = QaWorldFactory.CreatePlayer(new Vector2(0f, 1f), sprite, noFriction);
            QaWorldFactory.EnemyRig enemy = QaWorldFactory.CreateEnemy(new Vector2(15f, 0.5f), player.Root.transform, sprite);
            NpcInteraction2D npc = QaWorldFactory.CreateNpc(new Vector2(-12f, 0.75f), sprite);

            // ---- Camera ------------------------------------------------------------------------
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 2.25f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.75f, 0.9f);
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CameraFollow2D>();
            QaWorldFactory.SetField(follow, "target", player.Root.transform);

            // ---- UI: touch controls + NPC dialogue ------------------------------------------------
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("QA_TouchCanvas") { layer = 5 };
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Bottom-left: D-pad. Bottom-right: action buttons.
            AddTouchButton(canvasGo.transform, "Btn_Left", "<", PlayerInput2D.TouchAction.Left, new Vector2(0, 0), new Vector2(110, 170), player.Input, font);
            AddTouchButton(canvasGo.transform, "Btn_Right", ">", PlayerInput2D.TouchAction.Right, new Vector2(0, 0), new Vector2(330, 170), player.Input, font);
            AddTouchButton(canvasGo.transform, "Btn_Up", "UP", PlayerInput2D.TouchAction.Up, new Vector2(0, 0), new Vector2(220, 280), player.Input, font);
            AddTouchButton(canvasGo.transform, "Btn_Down", "DN", PlayerInput2D.TouchAction.Down, new Vector2(0, 0), new Vector2(220, 60), player.Input, font);
            AddTouchButton(canvasGo.transform, "Btn_Jump", "JUMP", PlayerInput2D.TouchAction.Jump, new Vector2(1, 0), new Vector2(-130, 130), player.Input, font);
            AddTouchButton(canvasGo.transform, "Btn_Attack", "ATK", PlayerInput2D.TouchAction.Attack, new Vector2(1, 0), new Vector2(-350, 110), player.Input, font);
            AddTouchButton(canvasGo.transform, "Btn_Interact", "TALK", PlayerInput2D.TouchAction.Interact, new Vector2(1, 0), new Vector2(-240, 300), player.Input, font);

            var panel = new GameObject("NPC_DialogPanel", typeof(RectTransform)) { layer = 5 };
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -40f);
            prt.sizeDelta = new Vector2(900f, 120f);
            panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            Text label = CreateLabel(panel.transform, "Text", "", font, 34);
            panel.SetActive(false);

            var dialog = canvasGo.AddComponent<QaNpcDialog>();
            QaWorldFactory.SetField(dialog, "panel", panel);
            QaWorldFactory.SetField(dialog, "label", label);
            UnityEventTools.AddPersistentListener(QaWorldFactory.GetNpcEvent(npc), dialog.Show);

            // ---- Diagnostics -------------------------------------------------------------------
            var hudGo = new GameObject("QA_Hud");
            var hud = hudGo.AddComponent<QaHud>();
            QaWorldFactory.SetField(hud, "motor", player.Motor);
            QaWorldFactory.SetField(hud, "input", player.Input);
            QaWorldFactory.SetField(hud, "playerHealth", player.Health);
            QaWorldFactory.SetField(hud, "enemyHealth", enemy.Health);
            QaWorldFactory.SetField(hud, "npcDialog", dialog);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Failed to save " + ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void AddTouchButton(Transform parent, string name, string caption, PlayerInput2D.TouchAction action,
            Vector2 anchor, Vector2 position, PlayerInput2D target, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(170f, 170f);
            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);
            var button = go.AddComponent<TouchButton2D>();
            QaWorldFactory.SetField(button, "target", target);
            QaWorldFactory.SetField(button, "action", action);
            CreateLabel(go.transform, "Label", caption, font, 40).raycastTarget = false;
        }

        private static Text CreateLabel(Transform parent, string name, string text, Font font, int size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return t;
        }

        private static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SetLayer(layers, QaWorldFactory.GroundLayer, "Ground");
            SetLayer(layers, QaWorldFactory.PlayerLayer, "Player");
            SetLayer(layers, QaWorldFactory.EnemyLayer, "Enemy");
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            SerializedProperty p = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(p.stringValue)) p.stringValue = name;
            else if (p.stringValue != name)
                throw new System.InvalidOperationException($"Layer {index} is '{p.stringValue}', expected '{name}'.");
        }

        private static Sprite EnsureSprite()
        {
            if (!File.Exists(SpritePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            bool dirty = importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 4f ||
                         importer.filterMode != FilterMode.Point || importer.spriteBorder != new Vector4(1, 1, 1, 1);
            if (dirty)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 4f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteBorder = new Vector4(1, 1, 1, 1);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        private static PhysicsMaterial2D EnsureMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
            if (mat) return mat;
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            mat = new PhysicsMaterial2D("QA_PlayerNoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }
    }
}
