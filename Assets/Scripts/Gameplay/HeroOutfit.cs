using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Dresses a hero visual with cosmetics without rebuilding the character: every piece is a SkinnedMeshRenderer
    /// on the hero's OWN skeleton (same bones and bind poses as the body, so Run/Jump/Climb/Attack/Skill clips,
    /// the face layer and the finger rig all keep working). Pieces may add a few extra joints (cape chain, wing
    /// roots, bell pivot) that are animated here on top of the clip: capes trail with speed, wings flap faster in
    /// the air, charms swing. Applying a loadout replaces the previous one; Apply(null) undresses.
    /// </summary>
    public sealed class HeroOutfit : MonoBehaviour
    {
        [SerializeField] private HeroClass heroClass;
        [SerializeField] private SkinnedMeshRenderer body;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Transform> extras = new List<Transform>();
        private readonly List<Driven> driven = new List<Driven>();
        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private OutfitLoadout current = new OutfitLoadout();
        private Vector3 lastPos;
        private float speed, vy, airTime;

        private sealed class Driven
        {
            public PieceMotion motion;
            public Transform[] joints;
            public Quaternion[] rest;
            public float[] angle, velocity;
            public float phase;
            public float side; // wings: +1 left, -1 right
        }

        public HeroClass Class => heroClass;
        public OutfitLoadout Current => current;
        public int PieceCount => spawned.Count;
        public IReadOnlyList<GameObject> Pieces => spawned;

        public void Bind(HeroClass c, SkinnedMeshRenderer bodyRenderer)
        {
            heroClass = c;
            body = bodyRenderer;
        }

        private void Awake()
        {
            if (!body)
                foreach (SkinnedMeshRenderer s in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (!s.name.StartsWith("Weapon_") && !s.name.StartsWith("Outfit_")) { body = s; break; }
            lastPos = transform.position;
        }

        /// <summary>Wears exactly this loadout (items the library cannot draw for this class are skipped).</summary>
        public void Apply(OutfitLoadout loadout)
        {
            Clear();
            current = loadout != null ? loadout.Clone() : new OutfitLoadout();
            CosmeticLibrary lib = CosmeticLibrary.Load();
            if (!lib || !body) return;
            CacheBones();

            var covered = new HashSet<string>();
            CosmeticArt set = lib.Get(current.set);
            if (set != null)
            {
                foreach (string s in set.coversSlots) covered.Add(s);
                Build(set);
            }
            if (current.slots != null)
                foreach (OutfitSlotItem s in current.slots)
                    if (!covered.Contains(s.slot)) Build(lib.Get(s.itemId));
        }

        public void Clear()
        {
            foreach (GameObject g in spawned) if (g) Kill(g);
            foreach (Transform t in extras) if (t) Kill(t.gameObject);
            spawned.Clear();
            extras.Clear();
            driven.Clear();
            current = new OutfitLoadout();
        }

        private static void Kill(GameObject g)
        {
            // Detach first: Destroy is deferred, and a re-Apply in the same frame must not find the old joints.
            g.SetActive(false);
            g.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);
        }

        private void CacheBones()
        {
            bones.Clear();
            foreach (Transform t in body.rootBone ? body.rootBone.GetComponentsInChildren<Transform>(true) : body.bones)
                if (t && !bones.ContainsKey(t.name)) bones[t.name] = t;
        }

        private void Build(CosmeticArt art)
        {
            ClassFit fit = art?.For(heroClass);
            if (fit == null) return;
            foreach (OutfitPiece p in fit.pieces) BuildPiece(art.itemId, p);
        }

        private void BuildPiece(string itemId, OutfitPiece p)
        {
            if (p.prefab)
            {
                Transform mount = !string.IsNullOrEmpty(p.mountBone) && bones.TryGetValue(p.mountBone, out Transform b) ? b : transform;
                GameObject fx = Instantiate(p.prefab, mount, false);
                fx.name = "Outfit_" + itemId + "_fx";
                SetLayer(fx, body.gameObject.layer);
                spawned.Add(fx);
                return;
            }
            if (!p.mesh || !p.material) return;

            // Extra joints first (parents always precede children in the list).
            var made = new List<Transform>();
            foreach (ExtraBone e in p.extraBones)
            {
                if (bones.ContainsKey(e.name)) continue; // already made by a sibling piece of the same item
                if (!bones.TryGetValue(e.parent, out Transform parent)) return;
                var t = new GameObject(e.name).transform;
                t.SetParent(parent, false);
                t.localPosition = e.localPosition;
                t.localRotation = e.localRotation;
                t.localScale = e.localScale;
                bones[e.name] = t;
                extras.Add(t);
                made.Add(t);
            }

            var resolved = new Transform[p.boneNames.Length];
            for (int i = 0; i < resolved.Length; i++)
                if (!bones.TryGetValue(p.boneNames[i], out resolved[i]))
                {
                    Debug.LogWarning($"[OUTFIT] {itemId}: bone {p.boneNames[i]} missing on {heroClass}");
                    return;
                }

            var go = new GameObject("Outfit_" + itemId);
            go.layer = body.gameObject.layer;
            go.transform.SetParent(body.transform.parent, false);
            go.transform.localPosition = body.transform.localPosition;
            go.transform.localRotation = body.transform.localRotation;
            go.transform.localScale = body.transform.localScale;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = p.mesh;
            smr.sharedMaterial = p.material;
            smr.bones = resolved;
            smr.rootBone = body.rootBone;
            smr.quality = SkinQuality.Bone4;
            Bounds bb = body.localBounds;
            bb.Expand(bb.size.magnitude * 0.6f); // capes / wings reach beyond the body
            smr.localBounds = bb;
            smr.updateWhenOffscreen = false;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            spawned.Add(go);

            if (p.motion != PieceMotion.None && made.Count > 0)
            {
                if (p.motion == PieceMotion.WingFlap)
                    foreach (Transform w in made) driven.Add(NewDriven(p.motion, new[] { w }, w.name.EndsWith("L") ? 1f : -1f));
                else driven.Add(NewDriven(p.motion, made.ToArray(), 1f));
            }
        }

        private static Driven NewDriven(PieceMotion m, Transform[] joints, float side)
        {
            var d = new Driven { motion = m, joints = joints, side = side, rest = new Quaternion[joints.Length], angle = new float[joints.Length], velocity = new float[joints.Length], phase = Random.value * 6.28f };
            for (int i = 0; i < joints.Length; i++) d.rest[i] = joints[i].localRotation;
            return d;
        }

        private static void SetLayer(GameObject g, int layer)
        {
            foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        /// <summary>Optional: the motor tells us when the hero is airborne (otherwise vertical speed is used).</summary>
        public bool? Airborne { get; set; }

        /// <summary>Fitting room: a standing preview "runs" in place, so capes/wings get the speed it shows.</summary>
        public float? SpeedOverride { get; set; }

        private void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 p = transform.position;
            Vector3 v = (p - lastPos) / dt;
            lastPos = p;
            if (v.sqrMagnitude > 400f) v = Vector3.zero; // teleports
            speed = Mathf.Lerp(speed, SpeedOverride ?? new Vector2(v.x, v.z).magnitude, 1f - Mathf.Exp(-dt * 10f));
            vy = Mathf.Lerp(vy, v.y, 1f - Mathf.Exp(-dt * 10f));
            bool air = Airborne ?? Mathf.Abs(vy) > 0.6f;
            airTime = air ? airTime + dt : 0f;

            foreach (Driven d in driven)
            {
                d.phase += dt;
                switch (d.motion)
                {
                    case PieceMotion.WingFlap:
                    {
                        float freq = air ? 7.5f : 2.1f + speed * 0.5f, amp = air ? 28f : 9f + speed * 3f;
                        float a = Mathf.Sin(d.phase * freq * Mathf.PI * 2f) * amp;
                        d.angle[0] = Mathf.Lerp(d.angle[0], a, 1f - Mathf.Exp(-dt * 20f));
                        d.joints[0].localRotation = d.rest[0] * Quaternion.Euler(0f, d.side * d.angle[0], 0f);
                        break;
                    }
                    case PieceMotion.CapeSway:
                    case PieceMotion.Pendulum:
                    {
                        bool cape = d.motion == PieceMotion.CapeSway;
                        // Joint frames are the model frame at bind (x right, y up, z forward); the piece hangs along
                        // local -y and a positive pitch about local x swings its tip backwards. The first joint is
                        // pitched so the piece hangs with gravity whatever the torso/head does (lean, attack, climb),
                        // trailing behind the hero with speed and lifting while falling; later joints add flutter.
                        Transform j0 = d.joints[0];
                        Quaternion parentRot = j0.parent ? j0.parent.rotation : Quaternion.identity;
                        Quaternion restWorld = parentRot * d.rest[0];
                        Vector3 axis = restWorld * Vector3.right, restDown = restWorld * Vector3.down;
                        float trail = cape ? Mathf.Clamp(speed * 0.32f + Mathf.Max(0f, -vy) * 0.22f, 0f, 1.4f) : Mathf.Clamp(speed * 0.12f, 0f, 0.5f);
                        Vector3 back = -transform.forward;
                        back.y = 0f;
                        Vector3 want = Vector3.ProjectOnPlane((Vector3.down + back.normalized * trail).normalized, axis);
                        float gravityPitch = want.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(restDown, want, axis) : 0f;
                        gravityPitch = Mathf.Clamp(gravityPitch, cape ? -25f : -70f, cape ? 80f : 70f);
                        float flutter = cape ? Mathf.Sin(d.phase * 1.7f) * (2f + speed * 2.5f) : Mathf.Sin(d.phase * 3.1f) * (2f + speed * 5f);
                        for (int i = 0; i < d.joints.Length; i++)
                        {
                            float target = i == 0 ? gravityPitch + flutter * 0.4f : flutter + (cape ? speed * 3f : 0f);
                            float k = cape ? 60f : 90f, damp = cape ? 9f : 6f;
                            float acc = k * (target - d.angle[i]) - damp * d.velocity[i];
                            d.velocity[i] += acc * dt;
                            d.angle[i] += d.velocity[i] * dt;
                            d.joints[i].localRotation = d.rest[i] * Quaternion.Euler(d.angle[i], 0f, 0f);
                        }
                        break;
                    }
                }
            }
        }
    }
}
