using System.Reflection;
using AdventureKing.Gameplay;
using UnityEngine;

namespace AdventureKing.QA
{
    /// <summary>
    /// INTERNAL QA ONLY. Builds grey-box test objects with the wiring described in
    /// Docs/UNITY_HANDOFF.md. Shared by the editor scene builder and the automated tests so that
    /// what is tested is exactly what the QA scene contains. Not final art, not a demo.
    /// </summary>
    public static class QaWorldFactory
    {
        public const int GroundLayer = 8;
        public const int PlayerLayer = 9;
        public const int EnemyLayer = 10;
        public const float PlayerGravity = 3f;

        public static readonly Color GroundColor = new Color(0.35f, 0.55f, 0.3f);
        public static readonly Color PlayerColor = new Color(0.25f, 0.5f, 0.95f);
        public static readonly Color EnemyColor = new Color(0.9f, 0.3f, 0.25f);
        public static readonly Color LadderColor = new Color(0.65f, 0.45f, 0.2f, 0.6f);
        public static readonly Color NpcColor = new Color(0.95f, 0.8f, 0.2f);

        public sealed class PlayerRig
        {
            public GameObject Root;
            public Rigidbody2D Body;
            public PlayerInput2D Input;
            public PlayerMotor2D Motor;
            public PlayerMelee2D Melee;
            public Health2D Health;
            public SpriteRenderer Visual;
        }

        public sealed class EnemyRig
        {
            public GameObject Root;
            public Rigidbody2D Body;
            public EnemyChase2D Chase;
            public Health2D Health;
        }

        public static void SetField(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) throw new System.MissingFieldException(target.GetType().Name, field);
            f.SetValue(target, value);
        }

        public static T GetField<T>(object target, string field)
        {
            FieldInfo f = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) throw new System.MissingFieldException(target.GetType().Name, field);
            return (T)f.GetValue(target);
        }

        public static Sprite CreateRuntimeSprite()
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        private static SpriteRenderer AddVisual(GameObject go, Sprite sprite, Color color, Vector2 size, int order)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.sortingOrder = order;
            return sr;
        }

        public static GameObject CreateGround(string name, Vector2 center, Vector2 size, Sprite sprite,
            Transform parent = null, bool oneWay = false)
        {
            var go = new GameObject(name) { layer = GroundLayer };
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            if (oneWay)
            {
                col.usedByEffector = true;
                var effector = go.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 160f;
            }
            if (sprite) AddVisual(go, sprite, GroundColor, size, 0);
            return go;
        }

        public static GameObject CreateWall(string name, Vector2 center, Vector2 size, Sprite sprite, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider2D>().size = size;
            if (sprite) AddVisual(go, sprite, GroundColor * 0.7f, size, 0);
            return go;
        }

        public static PlayerRig CreatePlayer(Vector2 position, Sprite sprite, PhysicsMaterial2D frictionless = null)
        {
            var go = new GameObject("QA_Player") { layer = PlayerLayer };
            go.transform.position = position;

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = PlayerGravity;
            body.freezeRotation = true;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.8f, 1.6f);
            capsule.direction = CapsuleDirection2D.Vertical;
            if (!frictionless)
                frictionless = new PhysicsMaterial2D("QA_PlayerNoFriction") { friction = 0f, bounciness = 0f };
            capsule.sharedMaterial = frictionless;

            var health = go.AddComponent<Health2D>();
            SetField(health, "faction", CombatFaction.Player);

            var input = go.AddComponent<PlayerInput2D>();
            var motor = go.AddComponent<PlayerMotor2D>();
            var melee = go.AddComponent<PlayerMelee2D>();

            var probe = new GameObject("GroundProbe").transform;
            probe.SetParent(go.transform, false);
            probe.localPosition = new Vector3(0f, -0.82f, 0f);

            var attackOrigin = new GameObject("AttackOrigin").transform;
            attackOrigin.SetParent(go.transform, false);
            attackOrigin.localPosition = new Vector3(0f, 0.1f, 0f);

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(go.transform, false);
            SpriteRenderer visual = sprite ? AddVisual(visualGo, sprite, PlayerColor, new Vector2(0.8f, 1.6f), 10) : null;

            SetField(motor, "body", body);
            SetField(motor, "controls", input);
            SetField(motor, "visual", visual);
            SetField(motor, "groundProbe", probe);
            SetField(motor, "groundMask", (LayerMask)(1 << GroundLayer));

            SetField(melee, "controls", input);
            SetField(melee, "motor", motor);
            SetField(melee, "attacker", health);
            SetField(melee, "attackOrigin", attackOrigin);
            // targets intentionally left at the script default (~0, everything) so the QA run
            // proves Health2D's self/faction guard, not just a layer filter, protects the player.

            return new PlayerRig
            {
                Root = go, Body = body, Input = input, Motor = motor, Melee = melee, Health = health, Visual = visual
            };
        }

        public static EnemyRig CreateEnemy(Vector2 position, Transform player, Sprite sprite, bool extraHitbox = false)
        {
            var go = new GameObject("QA_Enemy") { layer = EnemyLayer };
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = PlayerGravity;
            body.freezeRotation = true;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f);
            if (extraHitbox)
            {
                // A second collider on the same Health2D must still only be damaged once per swing.
                var hitbox = go.AddComponent<CircleCollider2D>();
                hitbox.radius = 0.5f;
                hitbox.isTrigger = true;
            }
            var health = go.AddComponent<Health2D>();
            SetField(health, "maxHealth", 60);
            SetField(health, "faction", CombatFaction.Enemy);
            health.RestoreFull();
            var chase = go.AddComponent<EnemyChase2D>();
            SetField(chase, "body", body);
            SetField(chase, "health", health);
            SetField(chase, "player", player);
            SpriteRenderer visual = sprite ? AddVisual(go, sprite, EnemyColor, new Vector2(0.9f, 0.9f), 9) : null;
            SetField(chase, "visual", visual);
            return new EnemyRig { Root = go, Body = body, Chase = chase, Health = health };
        }

        public static LadderZone2D CreateLadder(Vector2 center, Vector2 size, Sprite sprite)
        {
            var go = new GameObject("QA_Ladder");
            go.transform.position = center;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            col.isTrigger = true;
            var ladder = go.AddComponent<LadderZone2D>();
            if (sprite) AddVisual(go, sprite, LadderColor, size, 1);
            return ladder;
        }

        public static NpcInteraction2D CreateNpc(Vector2 position, Sprite sprite)
        {
            var go = new GameObject("QA_NPC");
            go.transform.position = position;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2.4f, 1.8f);
            col.isTrigger = true;
            var npc = go.AddComponent<NpcInteraction2D>();
            if (sprite)
            {
                var v = new GameObject("Visual");
                v.transform.SetParent(go.transform, false);
                AddVisual(v, sprite, NpcColor, new Vector2(0.8f, 1.5f), 5);
            }
            return npc;
        }

        public static UnityEngine.Events.UnityEvent GetNpcEvent(NpcInteraction2D npc)
        {
            // A freshly AddComponent'ed script has not been through serialization yet, so a
            // serialized UnityEvent field without an initializer can still be null.
            var evt = GetField<UnityEngine.Events.UnityEvent>(npc, "onInteracted");
            if (evt == null)
            {
                evt = new UnityEngine.Events.UnityEvent();
                SetField(npc, "onInteracted", evt);
            }
            return evt;
        }
    }
}
