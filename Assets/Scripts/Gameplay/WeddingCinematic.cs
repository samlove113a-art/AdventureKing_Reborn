using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// The 180-second wedding (8 scenes) for two real players, evaluated every frame as a pure function of the
    /// ceremony clock t (server time since the start). Every client in the Wedding Hall computes the same poses,
    /// so the couple, viewers and late joiners all see the same thing, in sync, without extra network traffic.
    /// Bodies: clips (walk, kneel, heart, sway, celebrate) + <see cref="ArmIK"/> so hands really meet hands, the knife,
    /// the goblets and the mouth; faces from <see cref="HeroFace"/>; props from <see cref="WeddingProps"/>.
    /// Camera, captions, guests, musician, lights and sound follow the same clock.
    /// </summary>
    [DefaultExecutionOrder(300)] // LateUpdate after ArmIK (150): props follow the solved hands
    public sealed class WeddingCinematic : MonoBehaviour
    {
        // ------------------------------------------------------------------ setup
        public sealed class Setup
        {
            public CeremonyState Ceremony;
            public NpcActor Officiant, Musician;
            public PetalFall Petals;
            public Material Template;           // hall toon material (props clone it)
            public GameObject[] GuestPrefabs = Array.Empty<GameObject>();
            public AudioClip Music, Footstep, Chime, Sparkle, KnifeCut, Clink, Applause, Cheer;
            public Func<float> Clock;            // ceremony seconds (null = server clock)
            public bool Preview;                 // QA: no server checks, banner at the end
        }

        public sealed class Actor
        {
            public string Id, Name;
            public bool Local;
            public CharacterVisual3D Visual;
            public RemoteAvatar Remote;
            public Transform Root, Model;
            public Animator Anim;
            public ArmIK Ik;
            public PoseOverlay Pose;
            public HeroFace Face;
            public HeroFingers Fingers;
            public HeroMotionProfile Profile;
            public float FeetOffset, Scale = 1f;
            public float ShoulderY, HandRestY, FaceY, ArmLen;
            public Vector3 Feet; public float Yaw;
            /// <summary>Small step toward a world target the hand cannot quite reach (servoed, max 15 cm, decays).</summary>
            public Vector3 Reach; public bool ReachUsed;
            public string State = "";
            public HeroFace.Expression Expr = HeroFace.Expression.Neutral;
            public bool WasMotorEnabled, WasSimulated;
            public Vector3 Forward => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
            public Vector3 RightDir => Quaternion.Euler(0f, Yaw, 0f) * Vector3.right;
            public Vector3 FaceFront => Ik ? Ik.FaceFront : Feet + Vector3.up * FaceY;
        }

        public static WeddingCinematic Current { get; private set; }
        public Actor A { get; private set; }
        public Actor B { get; private set; }
        public float T { get; private set; } = -1f;
        public string SceneId { get; private set; } = "";
        public bool Running { get; private set; }
        public bool Finished { get; private set; }
        public bool Aborted { get; private set; }
        public string AbortReason { get; private set; } = "";

        // ---- QA facts (tests read these to prove the interactions really happened)
        public bool RingOnB { get; private set; }
        public bool RingOnA { get; private set; }
        public float RingToFingerB { get; private set; } = 9f;
        public float KissFaceDistanceMin { get; private set; } = 9f;
        public float KissHeldSeconds { get; private set; }
        public float KnifeDepthMax { get; private set; }  // how deep the blade tip went below the bottom tier's top
        public int Cuts { get; private set; }
        public bool SliceOnPlate { get; private set; }
        public float ClinkRimDistanceMin { get; private set; } = 9f;
        public bool Clinked { get; private set; }
        public float WineLevelA => gobletA != null ? gobletA.Level : 1f;
        public float WineLevelB => gobletB != null ? gobletB.Level : 1f;
        public float GobletToMouthMinA { get; private set; } = 9f;
        public float GobletToMouthMinB { get; private set; } = 9f;
        public float HandsHeldSeconds { get; private set; }
        public List<string> Events { get; } = new List<string>();

        private Setup s;
        private CinematicDirector dir;
        private readonly Dictionary<string, float> scene = new Dictionary<string, float>();
        private float endT = 180f;
        private bool clockStarted;
        private float lastT = -1f;
        private AudioSource music, sfx;
        private float duckedVolume = -1f;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<(Light l, float i)> lights = new List<(Light, float)>();
        private readonly List<Renderer> hiddenLocal = new List<Renderer>();
        private readonly List<RemoteAvatar> hiddenRemotes = new List<RemoteAvatar>();
        private bool bannerShown;

        // props
        private float propScale = 1f;
        private WeddingProps.Cake cake;
        private GameObject table, knife, plate, ringA, ringB, glow;
        private WeddingProps.Goblet gobletA, gobletB;
        private Vector3 T0;  // cake table centre (floor)
        private float tableTop;

        // places
        private Vector3 stage = new Vector3(2f, 0f, 1.15f);
        private const float AisleStartX = -13.4f, AisleEndX = 0.2f;
        private float apart = 0.76f, toastApart = 0.8f;

        // guests
        private sealed class Guest { public GameObject Go; public Transform Model; public Animator Anim; public ArmIK Ik; public float Yaw; public string State = ""; public float WaveAt = -1f; public Vector3 Pos; public HeroFace Face; }
        private readonly List<Guest> guests = new List<Guest>();
        private ArmIK musicianIk;
        private Bounds harp;
        private Vector3 musicianHome;
        private bool musicianMoved;

        // ================================================================== start / stop
        public static WeddingCinematic Play(GameObject host, Setup setup, Actor a, Actor b)
        {
            var w = host.AddComponent<WeddingCinematic>();
            w.Begin(setup, a, b);
            return w;
        }

        /// <summary>Wraps a participant (the local hero or a remote player) so the timeline can drive it.</summary>
        public static Actor Wrap(string id, string name)
        {
            var world = AdventureWorldSession.Current;
            var act = new Actor { Id = id, Name = name };
            if (world && world.Character != null && world.Character.characterId == id && world.HeroModel)
            {
                act.Local = true;
                act.Visual = world.PlayerVisual;
                act.Root = world.Player.transform;
                act.Model = world.HeroModel.transform;
            }
            else
            {
                RemoteAvatar r = WorldSync.Current ? WorldSync.Current.Avatar(id) : null;
                if (!r) return null;
                act.Remote = r;
                act.Visual = r.Visual;
                act.Root = r.transform;
                act.Model = r.Model.transform;
            }
            return Complete(act);
        }

        /// <summary>QA: wraps any spawned hero body (remote avatar created locally from a fake peer).</summary>
        public static Actor WrapRemote(RemoteAvatar r, string id, string name) =>
            Complete(new Actor { Id = id, Name = name, Remote = r, Visual = r.Visual, Root = r.transform, Model = r.Model.transform });

        private static Actor Complete(Actor act)
        {
            act.Anim = act.Model.GetComponentInChildren<Animator>(true);
            act.Ik = act.Model.GetComponent<ArmIK>();
            if (!act.Ik) { act.Ik = act.Model.gameObject.AddComponent<ArmIK>(); act.Ik.Bind(act.Model); }
            act.Pose = act.Model.GetComponent<PoseOverlay>();
            if (!act.Pose) { act.Pose = act.Model.gameObject.AddComponent<PoseOverlay>(); act.Pose.Bind(act.Model); }
            act.Face = act.Model.GetComponentInChildren<HeroFace>(true);
            act.Fingers = act.Model.GetComponentInChildren<HeroFingers>(true);
            act.Profile = act.Model.GetComponentInParent<HeroMotionProfile>();
            if (!act.Profile) act.Profile = act.Model.GetComponentInChildren<HeroMotionProfile>(true);
            act.Scale = Mathf.Abs(act.Model.lossyScale.y);
            act.FeetOffset = act.Root.position.y - act.Model.position.y;
            act.Feet = act.Model.position;
            return act;
        }

        private void Begin(Setup setup, Actor a, Actor b)
        {
            Current = this;
            s = setup; A = a; B = b;
            dir = CinematicDirector.Current;
            float at = 0f;
            foreach (CeremonyStepState st in s.Ceremony.steps)
            {
                scene[st.id] = (st.startMs - s.Ceremony.startMs) / 1000f;
                at = Mathf.Max(at, scene[st.id] + st.ms / 1000f);
            }
            if (at > 1f) endT = at;
            foreach (Actor x in new[] { A, B }) Take(x);
            Measure();
            BuildProps();
            BuildGuests();
            Musician();
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude)) lights.Add((l, l.intensity));
            HideBystanders(true);
            Audio();
            if (dir) { dir.Skipped += OnSkip; dir.BeginExternal(); }
            Running = true;
        }

        private float S(string id, float fallback) => scene.TryGetValue(id, out float v) ? v : fallback;

        private void Take(Actor x)
        {
            if (x.Visual) { x.Visual.Cinematic = true; x.Visual.SetWeaponsVisible(false); x.Visual.HoldInRightHand(null); }
            if (x.Local)
            {
                var motor = x.Root.GetComponent<PlayerMotor2D>();
                if (motor) { x.WasMotorEnabled = motor.enabled; motor.enabled = false; }
                var rb = x.Root.GetComponent<Rigidbody2D>();
                if (rb) { x.WasSimulated = rb.simulated; rb.linearVelocity = Vector2.zero; rb.simulated = false; }
            }
            if (x.Anim) { x.Anim.SetFloat("Speed", 0f); x.Anim.SetBool("Grounded", true); x.Anim.SetBool("Climbing", false); x.Anim.SetBool("Dead", false); }
        }

        private void Release(Actor x)
        {
            if (x == null) return;
            if (x.Ik) x.Ik.ReleaseAll();
            if (x.Pose) x.Pose.Lean(0f);
            if (x.Fingers) { x.Fingers.Override(true, null); x.Fingers.Override(false, null); }
            if (x.Face) x.Face.SetExpression(HeroFace.Expression.Neutral);
            if (x.Anim) { x.Anim.speed = 1f; x.Anim.CrossFadeInFixedTime(Animator.StringToHash("Idle"), 0.25f, 0); }
            if (x.Visual) { x.Visual.Cinematic = false; x.Visual.SetWeaponsVisible(true); }
            if (x.Local)
            {
                Vector3 p = x.Root.position;
                x.Root.position = new Vector3(p.x, p.y, 0f);
                var rb = x.Root.GetComponent<Rigidbody2D>();
                if (rb) { rb.position = new Vector2(p.x, p.y + 0.05f); rb.simulated = x.WasSimulated || true; rb.linearVelocity = Vector2.zero; }
                var motor = x.Root.GetComponent<PlayerMotor2D>();
                if (motor) motor.enabled = true;
            }
            else if (x.Remote) x.Remote.Pin(null);
        }

        private void HideBystanders(bool hide)
        {
            if (hide)
            {
                if (WorldSync.Current)
                    foreach (var kv in WorldSync.Current.Avatars)
                        if (kv.Value && kv.Key != A.Id && kv.Key != B.Id) { kv.Value.SetHidden(true); hiddenRemotes.Add(kv.Value); }
                var world = AdventureWorldSession.Current;
                if (world && world.Player && !A.Local && !B.Local) // a viewer: their own hero steps out of the shot
                    foreach (Renderer r in world.Player.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hiddenLocal.Add(r); }
            }
            else
            {
                foreach (RemoteAvatar r in hiddenRemotes) if (r) r.SetHidden(false);
                foreach (Renderer r in hiddenLocal) if (r) r.enabled = true;
                hiddenRemotes.Clear(); hiddenLocal.Clear();
            }
        }

        private void OnSkip() => Stop("skipped");

        /// <summary>Ends the local presentation (skip, cancel, end). Server data is never touched from here.</summary>
        public void Stop(string reason)
        {
            if (!Running) return;
            Running = false;
            Finished = reason == "done";
            Aborted = reason != "done";
            AbortReason = reason;
            Events.Add("stop:" + reason);
            if (dir) { dir.Skipped -= OnSkip; dir.SetCaption(""); dir.EndExternal(); }
            Release(A); Release(B);
            foreach (Guest g in guests) if (g.Go) Destroy(g.Go);
            guests.Clear();
            foreach (GameObject go in spawned) if (go) Destroy(go);
            spawned.Clear();
            foreach (var (l, i) in lights) if (l) l.intensity = i;
            if (musicianMoved && s.Musician) { s.Musician.transform.position = musicianHome; s.Musician.Play("Idle"); }
            if (musicianIk) musicianIk.ReleaseAll();
            if (s.Officiant) { s.Officiant.Play("Idle"); s.Officiant.FaceFront(); }
            if (music) { music.Stop(); Destroy(music); }
            if (sfx) Destroy(sfx);
            Unduck();
            HideBystanders(false);
            if (Current == this) Current = null;
            Destroy(this, 2f);
        }

        // ================================================================== measuring the bodies
        private void Measure()
        {
            foreach (Actor x in new[] { A, B })
            {
                Vector3 feet = x.Model.position;
                // from bone lengths, not the current pose (a body that just spawned may still be in its bind pose)
                x.ShoulderY = x.Ik.ShoulderMid.y - feet.y;
                x.ArmLen = Mathf.Max(0.2f, x.Ik.ArmLength);
                x.HandRestY = Mathf.Max(0.15f, x.ShoulderY - x.ArmLen * 0.9f);
                x.FaceY = x.Ik.Head ? x.Ik.Head.position.y - feet.y : x.ShoulderY + 0.3f;
            }
            float arm = Mathf.Min(A.ArmLen, B.ArmLen);
            propScale = Mathf.Clamp(arm / 0.42f, 0.7f, 1.4f);
            // close enough that short chibi arms meet in the middle, far enough that the big heads never touch by accident
            float head = Mathf.Max(A.FaceY - A.ShoulderY, B.FaceY - B.ShoulderY);
            apart = Mathf.Clamp(arm * 2.0f + 0.12f, 0.55f, 0.95f);
            toastApart = Mathf.Max(apart, head * 2.6f + 0.1f);
            Events.Add($"measure arm={arm:0.00} shoulder={A.ShoulderY:0.00} hand={A.HandRestY:0.00} face={A.FaceY:0.00} scale={propScale:0.00}");
        }

        // ================================================================== props / guests / musician
        private void BuildProps()
        {
            var root = new GameObject("WeddingProps");
            spawned.Add(root);
            T0 = new Vector3(5.2f, 0f, 0.75f); // east of the stage: never in the aisle
            // chibi arms are short (~0.27 m): the table is compact and high (chest height) so hands really reach
            tableTop = Mathf.Min(A.ShoulderY, B.ShoulderY) - 0.18f;
            table = WeddingProps.Table(root.transform, s.Template, 0.3f, tableTop);
            table.transform.position = T0;
            cake = WeddingProps.BuildCake(root.transform, s.Template, 0.72f, 148f); // first slice on the back-left, toward the knife hand
            cake.Root.transform.position = T0 + new Vector3(0f, tableTop, -0.06f);
            plate = WeddingProps.Plate(s.Template, propScale);
            plate.transform.SetParent(root.transform, true);
            plate.transform.localScale = Vector3.one * 0.8f;
            plate.transform.position = T0 + new Vector3(-0.17f, tableTop + 0.002f, 0.21f);
            knife = WeddingProps.Knife(s.Template, knifeScale = 0.7f);
            knife.transform.SetParent(root.transform, true);
            gobletA = WeddingProps.BuildGoblet(s.Template, propScale);
            gobletB = WeddingProps.BuildGoblet(s.Template, propScale);
            gobletA.Root.transform.SetParent(root.transform, true);
            gobletB.Root.transform.SetParent(root.transform, true);
            ringA = WeddingProps.Ring(s.Template, 1.6f, new Color(0.75f, 0.9f, 1f));   // A puts it on B (diamond); large enough to read on screen
            ringB = WeddingProps.Ring(s.Template, 1.6f, new Color(1f, 0.55f, 0.7f));   // B puts it on A (rose gem)
            ringA.transform.SetParent(root.transform, true);
            ringB.transform.SetParent(root.transform, true);
            ringA.SetActive(false); ringB.SetActive(false);
            glow = new GameObject("KissGlow");
            glow.transform.SetParent(root.transform, false);
            var sr = glow.AddComponent<SpriteRenderer>();
            sr.sprite = WeddingProps.Glow();
            sr.color = new Color(1f, 0.85f, 0.9f, 0f);
            sr.sortingOrder = -5;
        }

        private void BuildGuests()
        {
            var spots = new[] { new Vector3(-10.4f, 0f, 1.8f), new Vector3(-8.2f, 0f, 1.9f), new Vector3(-6.0f, 0f, 1.8f), new Vector3(-4.3f, 0f, 2.05f) };
            for (int i = 0; i < s.GuestPrefabs.Length && i < spots.Length; i++)
            {
                GameObject p = s.GuestPrefabs[i];
                if (!p) continue;
                GameObject go = Instantiate(p);
                go.name = "Guest_" + p.name;
                spawned.Add(go);
                go.transform.position = spots[i];
                var g = new Guest { Go = go, Pos = spots[i], Anim = go.GetComponentInChildren<Animator>(true) };
                g.Model = g.Anim ? g.Anim.transform : go.transform;
                g.Ik = g.Model.gameObject.AddComponent<ArmIK>();
                g.Ik.Bind(g.Model);
                g.Face = go.GetComponentInChildren<HeroFace>(true);
                g.Yaw = 180f;
                go.transform.rotation = Quaternion.Euler(0f, g.Yaw, 0f);
                guests.Add(g);
                PlayGuest(g, "Idle");
            }
        }

        private void PlayGuest(Guest g, string state, float fade = 0.25f)
        {
            if (g.State == state || !g.Anim) return;
            int h = Animator.StringToHash(state);
            if (!g.Anim.HasState(0, h)) return;
            g.Anim.CrossFadeInFixedTime(h, fade, 0);
            g.State = state;
        }

        private void Musician()
        {
            if (!s.Musician) return;
            Renderer harpR = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).FirstOrDefault(r => r.name.Contains("wh_harp"));
            if (!harpR) return;
            harp = harpR.bounds;
            musicianHome = s.Musician.transform.position;
            s.Musician.transform.position = new Vector3(harp.min.x - 0.22f, musicianHome.y, harp.center.z - 0.05f);
            musicianMoved = true;
            Transform vis = s.Musician.Animator ? (s.Musician.Animator.transform.parent ? s.Musician.Animator.transform.parent : s.Musician.Animator.transform) : s.Musician.transform;
            vis.localRotation = Quaternion.Euler(0f, 105f, 0f);
            musicianIk = s.Musician.Animator.gameObject.AddComponent<ArmIK>();
            musicianIk.Bind(s.Musician.Animator.transform);
            s.Musician.Play("Idle");
        }

        // ================================================================== audio
        private void Audio()
        {
            var aud = AdventureAudioDirector.Instance;
            float vol = aud ? (aud.Muted || !aud.MusicOn ? 0f : aud.MusicVolume) : 0.7f;
            float fx = aud ? (aud.Muted ? 0f : aud.EffectsVolume) : 0.8f;
            Duck();
            if (s.Music)
            {
                music = gameObject.AddComponent<AudioSource>();
                music.clip = s.Music; music.loop = false; music.playOnAwake = false; music.spatialBlend = 0f;
                music.volume = 0.9f * vol;
            }
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false; sfx.spatialBlend = 0f; sfx.volume = fx;
        }

        private void Duck()
        {
            var aud = AdventureAudioDirector.Instance;
            if (!aud) return;
            AudioSource src = aud.MusicSource;
            if (src) { duckedVolume = src.volume; src.volume = 0f; }
        }

        private void Unduck()
        {
            var aud = AdventureAudioDirector.Instance;
            if (!aud || duckedVolume < 0f) return;
            AudioSource src = aud.MusicSource;
            if (src) src.volume = duckedVolume;
            duckedVolume = -1f;
        }

        private void Sfx(AudioClip c, float gain = 1f)
        {
            if (!c) return;
            if (sfx) sfx.PlayOneShot(c, gain);
            if (c != s.Footstep) Events.Add($"sfx:{c.name}@{T:0.000}:{gain:0.00}");
        }

        // ================================================================== clock
        private float Clock()
        {
            if (s.Clock != null) return s.Clock();
            long now = WorldSync.Current ? WorldSync.Current.ServerNowMs : s.Ceremony.startMs;
            return (now - s.Ceremony.startMs) / 1000f;
        }

        private void Update()
        {
            if (!Running) return;
            float server = Clock();
            if (s.Clock != null) T = server; // QA / preview clock: exact (it may seek)
            else if (!clockStarted) { T = server; clockStarted = true; }
            else
            {
                T += Time.deltaTime;
                // follow the server clock gently (never while a fixed-step recorder slows the game clock down)
                if (Time.captureFramerate == 0 && s.Clock == null)
                {
                    float diff = server - T;
                    T += Mathf.Abs(diff) > 2f ? diff : diff * Mathf.Min(1f, Time.deltaTime * 0.8f);
                }
            }
            if (!s.Preview && WorldSync.Current && WorldSync.Current.Last != null)
            {
                CeremonyState c = WorldSync.Current.Last.ceremony;
                if (c != null && c.Valid && c.id == s.Ceremony.id && c.state == "cancelled")
                {
                    if (dir) dir.ShowBanner("Lễ cưới đã tạm dừng: một người đã rời Sảnh Kết Hôn.", 4f);
                    Stop("cancelled");
                    return;
                }
                if (!bannerShown && c != null && c.id == s.Ceremony.id && c.state == "done" && T > endT - 3f)
                {
                    bannerShown = true;
                    if (dir) dir.ShowBanner($"Chúc mừng {A.Name} ♥ {B.Name} đã chính thức thành hôn!", 4.5f);
                    Events.Add("banner");
                }
            }
            if (s.Preview && !bannerShown && T >= endT)
            {
                bannerShown = true;
                if (dir) dir.ShowBanner($"Chúc mừng {A.Name} ♥ {B.Name} đã chính thức thành hôn!", 4.5f);
            }
            if (T < 0f) { Stage(0f, idleOnly: true); Cameras(0f); return; }
            if (T > endT + 3.5f) { Stop("done"); return; }
            Evaluate(Mathf.Min(T, endT));
            lastT = T;
        }

        // ================================================================== helpers
        private static float Ramp(float t, float t0, float t1) => t <= t0 ? 0f : t >= t1 ? 1f : Mathf.SmoothStep(0f, 1f, (t - t0) / (t1 - t0));
        private static float Bump(float t, float t0, float t1, float t2, float t3) => Ramp(t, t0, t1) * (1f - Ramp(t, t2, t3));
        private bool Crossed(float at) => lastT < at && T >= at && (lastT >= 0f || at - T < 0.5f);
        private static float LerpAngle(float a, float b, float k) => Mathf.LerpAngle(a, b, Mathf.Clamp01(k));

        private void Place(Actor x, Vector3 feet, float yaw)
        {
            feet += x.Reach;
            // never snap round: turn at most ~400°/s (a seek / late join may jump straight there)
            bool jumped = lastT < 0f || Mathf.Abs(T - lastT) > 0.5f;
            if (!jumped && !float.IsNaN(x.Yaw)) yaw = Mathf.MoveTowardsAngle(x.Yaw, yaw, 400f * Mathf.Max(Time.deltaTime, 1e-3f));
            x.Feet = feet; x.Yaw = yaw;
            if (x.Local) x.Root.position = feet + Vector3.up * x.FeetOffset;
            else if (x.Remote) x.Remote.Pin(feet);
            Transform vr = x.Visual && x.Visual.VisualRoot ? x.Visual.VisualRoot : x.Model;
            vr.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void State(Actor x, string state, float fade = 0.25f, bool reverse = false)
        {
            if (x.State == state || !x.Anim) return;
            int h = Animator.StringToHash(state);
            if (!x.Anim.HasState(0, h)) { if (state != "Idle") State(x, "Idle", fade); return; }
            if (reverse) x.Anim.CrossFade(h, 0.12f, 0, 1f);
            else x.Anim.CrossFadeInFixedTime(h, fade, 0);
            x.State = state;
            Events.Add($"{(x == A ? "A" : "B")}:{state}@{T:0.0}");
        }

        private void Expr(Actor x, HeroFace.Expression e)
        {
            if (x.Expr == e || !x.Face) return;
            x.Face.SetExpression(e);
            x.Expr = e;
        }

        /// <summary>Walk speed playback that keeps the planted foot still on the floor.</summary>
        private void WalkPlayback(Actor x, float speed)
        {
            if (!x.Anim) return;
            float stride = (x.Profile ? x.Profile.walkStrideSpeed : 0.28f) * x.Scale;
            x.Anim.speed = x.State == "Walk" && stride > 0.01f ? Mathf.Clamp(speed / stride, 0.6f, 2.4f) : 1f;
        }

        /// <summary>Moves along a polyline of feet positions between t0 and t1; yaw follows the path (smoothed).</summary>
        private (Vector3 p, float yaw, float sp) Path(float t, float t0, float t1, params Vector3[] pts)
        {
            float total = 0f;
            for (int i = 1; i < pts.Length; i++) total += Vector3.Distance(pts[i - 1], pts[i]);
            float k = Mathf.Clamp01((t - t0) / Mathf.Max(0.01f, t1 - t0));
            // ease in/out over the first and last 0.6 s
            float dur = t1 - t0, ease = Mathf.Min(0.6f, dur * 0.25f);
            float u = t - t0;
            float dist;
            float v = total / Mathf.Max(0.01f, dur - ease);
            if (u <= 0f) dist = 0f;
            else if (u >= dur) dist = total;
            else if (u < ease) dist = 0.5f * v / ease * u * u;
            else if (u > dur - ease) { float r = dur - u; dist = total - 0.5f * v / ease * r * r; }
            else dist = 0.5f * v * ease + v * (u - ease);
            dist = Mathf.Clamp(dist, 0f, total);
            float speed = u <= 0f || u >= dur ? 0f : u < ease ? v * u / ease : u > dur - ease ? v * (dur - u) / ease : v;
            float acc = 0f;
            for (int i = 1; i < pts.Length; i++)
            {
                float seg = Vector3.Distance(pts[i - 1], pts[i]);
                if (acc + seg >= dist || i == pts.Length - 1)
                {
                    Vector3 p = Vector3.Lerp(pts[i - 1], pts[i], seg > 1e-4f ? (dist - acc) / seg : 1f);
                    Vector3 d = pts[i] - pts[i - 1];
                    float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    return (p, yaw, k >= 1f ? 0f : speed);
                }
                acc += seg;
            }
            return (pts[pts.Length - 1], 90f, 0f);
        }

        // ================================================================== the timeline
        private float E, O, V, R, K, C, D, F;

        private void Stage(float t, bool idleOnly = false)
        {
            E = S("entrance", 0f); O = S("opening", 20f); V = S("vows", 45f); R = S("rings", 70f); K = S("kiss", 100f);
            C = S("cake", 120f); D = S("toast", 145f); F = S("finale", 165f);
            if (idleOnly) { Place(A, new Vector3(AisleStartX, 0f, 0.15f), 90f); Place(B, new Vector3(AisleStartX, 0f, 0.75f), 90f); State(A, "Idle"); State(B, "Idle"); }
        }

        private void Evaluate(float t)
        {
            Stage(t);
            string id = t < O ? "entrance" : t < V ? "opening" : t < R ? "vows" : t < K ? "rings" : t < C ? "kiss" : t < D ? "cake" : t < F ? "toast" : "finale";
            if (id != SceneId) { SceneId = id; Events.Add("scene:" + id + "@" + t.ToString("0.0")); }
            // defaults each frame; scenes override
            foreach (Actor x in new[] { A, B })
            {
                x.Ik.Release(true); x.Ik.Release(false);
                x.Ik.Look(Vector3.zero, 0f);
                x.Pose.Lean(0f);
                if (x.Fingers) { x.Fingers.Override(true, null); x.Fingers.Override(false, null); }
            }
            Bodies(t);
            foreach (Actor x in new[] { A, B })
            {
                if (!x.ReachUsed) x.Reach = Vector3.MoveTowards(x.Reach, Vector3.zero, Time.deltaTime * 0.25f);
                x.ReachUsed = false;
            }
            Props(t);
            Guests(t);
            Officiant(t);
            MusicianPlay(t);
            Lights(t);
            Captions(t);
            Cameras(t);
            Sounds(t);
            if (music && music.clip && !music.isPlaying && t < music.clip.length - 0.5f) { music.time = Mathf.Clamp(t, 0f, music.clip.length - 0.1f); music.Play(); }
            if (music && music.isPlaying && Mathf.Abs(music.time - t) > 0.6f && t < music.clip.length - 0.5f) music.time = t;
        }

        // ------------------------------------------------------------------ bodies
        private Vector3 SpotA => stage + Vector3.left * (apart * 0.5f);
        private Vector3 SpotB => stage + Vector3.right * (apart * 0.5f);
        // behind the cake table, facing the guests / camera (the classic cake-cutting photo)
        private Vector3 CakeA => T0 + new Vector3(-0.22f, 0f, 0.36f);
        private Vector3 CakeB => T0 + new Vector3(0.21f, 0f, 0.38f);
        private Vector3 GobletRestA => T0 + new Vector3(-0.03f, tableTop + 0.035f, 0.26f);
        private Vector3 GobletRestB => T0 + new Vector3(0.12f, tableTop + 0.035f, 0.24f);
        private Vector3 ToastA => T0 + new Vector3(0.62f, 0f, -0.05f);
        private Vector3 ToastB => T0 + new Vector3(1.05f, 0f, -0.03f);
        private Vector3 FinaleA => new Vector3(1.66f, 0f, 0.85f);
        private Vector3 FinaleB => new Vector3(2.34f, 0f, 0.95f);
        private float kissGap = -1f;

        private void Bodies(float t)
        {
            // ---------------- 1. entrance: stand at the arch, walk down the aisle hand in hand, step in front of the stage
            Vector3 aStart = new Vector3(AisleStartX, 0f, 0.15f), bStart = new Vector3(AisleStartX, 0f, 0.75f);
            Vector3 aEnd = new Vector3(AisleEndX, 0f, 0.15f), bEnd = new Vector3(AisleEndX, 0f, 0.75f);
            if (t < O)
            {
                float w0 = E + 3.5f, w1 = E + 16.5f, w2 = E + 19.2f;
                (Vector3 p, float yaw, float sp) a, b;
                if (t < w1) { a = Path(t, w0, w1, aStart, aEnd); b = Path(t, w0, w1, bStart, bEnd); }
                else { a = Path(t, w1, w2, aEnd, SpotA); b = Path(t, w1, w2, bEnd, SpotB); }
                float turn = Ramp(t, w2 - 0.3f, O);
                Place(A, a.p, LerpAngle(a.sp > 0.05f ? a.yaw : 90f, 20f, turn));
                Place(B, b.p, LerpAngle(b.sp > 0.05f ? b.yaw : 90f, -20f, turn));
                foreach (var (x, m) in new[] { (A, a), (B, b) }) { State(x, m.sp > 0.05f ? "Walk" : "Idle"); WalkPlayback(x, m.sp); }
                // hand in hand (A's left, B's right meet between them), released for the last steps
                float hold = Bump(t, E + 1.0f, E + 2.0f, w1 - 0.4f, w1 + 0.3f);
                HoldHands(A, true, B, false, hold, between: true); // facing +X: A (front) left hand, B (behind) right hand
                Expr(A, HeroFace.Expression.Happy); Expr(B, HeroFace.Expression.Happy);
                if (t < w0) { A.Ik.Look(B.FaceFront, 0.6f); B.Ik.Look(A.FaceFront, 0.6f); }
                return;
            }
            foreach (Actor x in new[] { A, B }) WalkPlayback(x, 0f);

            // ---------------- 2. opening: facing the officiant, glances at each other
            if (t < V)
            {
                Place(A, SpotA, 20f); Place(B, SpotB, -20f);
                State(A, "Idle"); State(B, "Idle");
                Expr(A, HeroFace.Expression.Tender); Expr(B, HeroFace.Expression.Tender);
                Vector3 off = s.Officiant ? s.Officiant.transform.position + Vector3.up * 1.4f : stage + new Vector3(0f, 1.9f, 3f);
                float glance = Bump(t, O + 15f, O + 15.8f, O + 18.5f, O + 19.3f) + Bump(t, O + 21f, O + 21.7f, O + 23.5f, O + 24.3f);
                A.Ik.Look(Vector3.Lerp(off, B.FaceFront, glance), 0.55f + 0.3f * glance);
                B.Ik.Look(Vector3.Lerp(off, A.FaceFront, glance), 0.55f + 0.3f * glance);
                HoldHands(A, false, B, true, Bump(t, O + 2f, O + 3f, V - 1f, V), between: true);
                return;
            }

            // ---------------- 3. vows: face each other, hold the camera-side hands, speak in turn
            if (t < R)
            {
                float turn = Ramp(t, V, V + 1.6f);
                Place(A, SpotA, LerpAngle(20f, 90f, turn)); Place(B, SpotB, LerpAngle(-20f, -90f, turn));
                State(A, "Idle"); State(B, "Idle");
                HoldHands(A, false, B, true, Bump(t, V + 1.6f, V + 2.6f, R - 1.5f, R - 0.5f), between: false);
                A.Ik.Look(B.FaceFront, turn * 0.9f); B.Ik.Look(A.FaceFront, turn * 0.9f);
                Expr(A, HeroFace.Expression.Tender); Expr(B, HeroFace.Expression.Tender);
                if (Crossed(V + 3f) && A.Face) A.Face.Speak(6.5f);
                if (Crossed(V + 10.5f) && B.Face) B.Face.Speak(6.5f);
                if (t > V + 18f) { Expr(A, HeroFace.Expression.Happy); Expr(B, HeroFace.Expression.Happy); }
                return;
            }

            // ---------------- 4. rings: A kneels and slides the ring on B's finger, then B gives A the other ring
            if (t < K)
            {
                Place(A, SpotA, 90f); Place(B, SpotB, -90f);
                float down0 = R + 4.2f, downEnd = down0 + 1.35f, up0 = R + 16.2f, upEnd = up0 + 1.4f;
                if (t < down0) State(A, "Idle");
                else if (t < downEnd) State(A, "KneelDown", reverse: true);
                else if (t < up0) State(A, "KneelHold", 0.12f);
                else if (t < upEnd) State(A, "KneelUp", 0.12f);
                else State(A, "Idle", 0.3f);
                State(B, "Idle");
                Vector3 mid = (A.Feet + B.Feet) * 0.5f;
                Vector3 camSide = Vector3.back * 0.1f;
                // ring 1: B's left hand offered palm down, A's right hand brings the ring onto the ring finger
                // offered low, in front of the kneeling partner's chest (never in front of the face)
                Vector3 p1 = mid + Vector3.right * 0.07f + Vector3.up * (B.HandRestY - 0.03f) + Vector3.back * 0.13f;
                float offer = Bump(t, R + 6f, R + 7.6f, R + 13.2f, R + 14.4f);
                B.Pose.Lean(11f * offer);
                B.Ik.Reach(true, p1, offer, Vector3.down + Vector3.back * 0.3f);
                if (B.Fingers) B.Fingers.Override(true, offer > 0.2f ? 0.08f : (float?)null);
                float bring = Bump(t, R + 6.6f, R + 8.6f, R + 12.8f, R + 14.0f);
                Vector3 ringPath = RingPathPoint(t, R + 9.0f, R + 12.3f, p1, B, true);
                A.Ik.Reach(false, ringPath + Vector3.left * 0.03f * propScale + Vector3.down * 0.015f, bring, Vector3.down);
                if (A.Fingers) A.Fingers.Override(false, bring > 0.2f ? 0.55f : (float?)null);
                // B admires the ring, then ring 2: A's right hand offered, B's left hand slides the ring on
                float admire = Bump(t, R + 14.0f, R + 15.0f, R + 17.2f, R + 18.0f);
                if (admire > 0.01f) B.Ik.Reach(true, B.FaceFront + B.Forward * 0.16f * propScale + Vector3.down * 0.12f * propScale + B.RightDir * -0.05f, admire, Vector3.down);
                Vector3 p2 = mid + Vector3.left * 0.05f + Vector3.up * (A.HandRestY + 0.04f) + Vector3.back * 0.13f;
                float offer2 = Bump(t, R + 19.2f, R + 20.6f, R + 25.4f, R + 26.4f);
                if (offer2 > 0.01f) { A.Ik.Reach(false, p2, offer2, Vector3.down + Vector3.back * 0.3f); if (A.Fingers) A.Fingers.Override(false, 0.08f); }
                float bring2 = Bump(t, R + 19.8f, R + 21.6f, R + 24.8f, R + 26.0f);
                Vector3 ringPath2 = RingPathPoint(t, R + 22.0f, R + 24.4f, p2, A, false);
                if (bring2 > 0.01f) B.Ik.Reach(true, ringPath2 + Vector3.right * 0.03f * propScale + Vector3.down * 0.015f, bring2, Vector3.down);
                if (bring2 > 0.2f && B.Fingers) B.Fingers.Override(true, 0.55f);
                // eyes: on the hands while the ring goes on, then on each other
                Vector3 ringFocus = t < R + 18f ? p1 : p2;
                float onHands = Bump(t, R + 7f, R + 8f, R + 12.6f, R + 13.6f) + Bump(t, R + 20f, R + 21f, R + 24.6f, R + 25.6f);
                A.Ik.Look(Vector3.Lerp(B.FaceFront, ringFocus, onHands), 0.9f);
                B.Ik.Look(Vector3.Lerp(A.FaceFront, ringFocus, onHands), 0.9f);
                Expr(A, t > R + 12.3f ? HeroFace.Expression.Happy : HeroFace.Expression.Tender);
                Expr(B, t > R + 12.3f && t < R + 14.5f ? HeroFace.Expression.Surprised : t > R + 12.3f ? HeroFace.Expression.Happy : HeroFace.Expression.Tender);
                HoldHands(A, false, B, true, Bump(t, R + 27f, R + 28f, K + 6f, K + 7f), between: false);
                return;
            }

            // ---------------- 5. kiss: step close, lean in, faces meet for ~4 s, part shyly; heart + sway
            if (t < C)
            {
                float close = Ramp(t, K + 4f, K + 6.5f);
                float leanIn = Bump(t, K + 6.5f, K + 7.6f, K + 11.6f, K + 12.6f);
                float gap = Mathf.Lerp(apart, KissGap(), close) + (apart - KissGap()) * 0.6f * Ramp(t, K + 12f, K + 13.5f);
                Vector3 mid = stage;
                Place(A, mid + Vector3.left * gap * 0.5f, 90f); Place(B, mid + Vector3.right * gap * 0.5f, -90f);
                bool steps = (close > 0.02f && close < 0.98f) || (t > K + 12f && t < K + 13.5f);
                State(A, steps ? "Walk" : t > K + 14.5f && t < K + 19f ? "Sway" : "Idle", 0.2f);
                State(B, steps ? "Walk" : t > K + 14.5f && t < K + 19.3f ? "Heart" : "Idle", 0.2f);
                WalkPlayback(A, steps ? 0.25f : 0f); WalkPlayback(B, steps ? 0.25f : 0f);
                A.Pose.Lean(15f * leanIn); B.Pose.Lean(15f * leanIn);
                Vector3 kissPoint = (A.FaceFront + B.FaceFront) * 0.5f;
                A.Ik.Look(kissPoint + Vector3.down * 0.01f, 0.85f, 7f * leanIn);
                B.Ik.Look(kissPoint + Vector3.down * 0.01f, 0.85f, -7f * leanIn);
                HoldHands(A, false, B, true, Bump(t, K, K + 0.5f, K + 13.5f, K + 14.3f), between: false);
                bool kissing = leanIn > 0.95f;
                Expr(A, kissing ? HeroFace.Expression.Kiss : t > K + 12.6f ? HeroFace.Expression.Shy : HeroFace.Expression.Tender);
                Expr(B, kissing ? HeroFace.Expression.Kiss : t > K + 12.6f ? HeroFace.Expression.Shy : HeroFace.Expression.Tender);
                float d = Vector3.Dot(B.FaceFront - A.FaceFront, Vector3.right); // > 0 while the faces have not met
                if (leanIn > 0.9f)
                {
                    KissFaceDistanceMin = Mathf.Min(KissFaceDistanceMin, d);
                    if (d < 0.05f && d > -0.03f) KissHeldSeconds += Time.deltaTime;
                    // close the last centimetres from the measured faces (each class has its own head shape / hat)
                    kissGap = Mathf.Clamp(KissGap() - (d - 0.012f) * Mathf.Min(1f, Time.deltaTime * 3f), 0.3f, apart);
                }
                return;
            }

            // ---------------- 6. cake: walk to the cake table (east of the stage), stand behind it facing the guests,
            // inner hands joined; the outer hand cuts the back-left of the cake and serves the slice onto the plate
            if (t < D)
            {
                float w0 = C + 1.0f, w1 = C + 6.2f;
                var a = Path(t, w0, w1, SpotA, new Vector3(CakeA.x - 0.55f, 0f, CakeA.z + 0.05f), CakeA);
                var b = Path(t, w0 - 0.3f, w1 - 0.4f, SpotB, new Vector3(CakeB.x - 0.55f, 0f, CakeB.z + 0.05f), CakeB);
                float face = Ramp(t, w1 - 0.5f, w1 + 0.9f);
                float turnEast = Ramp(t, C, C + 0.9f);
                Place(A, a.p, LerpAngle(a.sp > 0.05f ? a.yaw : LerpAngle(90f, 90f, turnEast), 172f, face));
                Place(B, b.p, LerpAngle(b.sp > 0.05f ? b.yaw : LerpAngle(-90f, 90f, turnEast), 190f, face));
                foreach (var (x, m) in new[] { (A, a), (B, b) }) { State(x, m.sp > 0.05f ? "Walk" : t > C + 21.6f && t < C + 24.6f && x == A ? "Celebrate" : "Idle"); WalkPlayback(x, m.sp); }
                CakeHands(t);
                // inner hands joined while the outer hand cuts (A's left, B's right)
                HoldHands(A, true, B, false, Bump(t, C + 7.6f, C + 8.6f, C + 20.6f, C + 21.4f), between: true);
                Expr(A, t > C + 8f ? HeroFace.Expression.Happy : HeroFace.Expression.Tender);
                Expr(B, HeroFace.Expression.Happy);
                float atEach = Bump(t, C + 19.6f, C + 20.4f, C + 25f, C + 26f);
                Vector3 cakeFocus = cake.Root.transform.position + Vector3.up * 0.1f;
                A.Ik.Look(Vector3.Lerp(cakeFocus, B.FaceFront, atEach), face * 0.35f);
                B.Ik.Look(Vector3.Lerp(cakeFocus, A.FaceFront, atEach), face * 0.35f);
                return;
            }

            // ---------------- 7. toast: inner hands take the goblets, step to the open side of the table, clink, drink
            if (t < F)
            {
                float m0 = D + 2.5f, m1 = D + 4.6f;
                var a = Path(t, m0, m1, CakeA, new Vector3(CakeA.x + 0.2f, 0f, CakeA.z - 0.35f), ToastA);
                var b = Path(t, m0, m1, CakeB, new Vector3(CakeB.x + 0.35f, 0f, CakeB.z - 0.15f), ToastB);
                float settle = Ramp(t, m1 - 0.4f, m1 + 0.6f);
                Place(A, a.p, LerpAngle(t < m0 ? 165f : a.sp > 0.05f ? a.yaw : 165f, 165f, settle));
                Place(B, b.p, LerpAngle(t < m0 ? 195f : b.sp > 0.05f ? b.yaw : 195f, 195f, settle));
                State(A, a.sp > 0.05f ? "Walk" : "Idle"); State(B, b.sp > 0.05f ? "Walk" : "Idle");
                WalkPlayback(A, a.sp); WalkPlayback(B, b.sp);
                ToastHands(t);
                bool drinking = t > D + 9.4f && t < D + 12.6f;
                Expr(A, drinking || t > D + 6.5f ? HeroFace.Expression.Happy : HeroFace.Expression.Tender);
                Expr(B, drinking || t > D + 6.5f ? HeroFace.Expression.Happy : HeroFace.Expression.Tender);
                return;
            }

            // ---------------- 8. finale: back in front of the stage, celebrate with the guests
            {
                float w0 = F + 0.3f, w1 = F + 4.3f;
                var a = Path(t, w0, w1, ToastA, new Vector3(ToastA.x - 1.6f, 0f, 0.8f), FinaleA);
                var b = Path(t, w0 + 0.25f, w1, ToastB, new Vector3(ToastB.x - 1.6f, 0f, 1.25f), FinaleB);
                float face = Ramp(t, w1 - 0.3f, w1 + 0.9f);
                float turnWest = Ramp(t, F, F + 0.5f);
                Place(A, a.p, LerpAngle(a.sp > 0.05f ? a.yaw : LerpAngle(165f, -90f, turnWest), 162f, face));
                Place(B, b.p, LerpAngle(b.sp > 0.05f ? b.yaw : LerpAngle(195f, 270f, turnWest), 198f, face));
                foreach (var (x, m) in new[] { (A, a), (B, b) })
                {
                    string st = m.sp > 0.05f ? "Walk" : t > F + 5f && t < F + 8.2f ? (x == A ? "Celebrate" : "Heart") : t > F + 9f && t < F + 12f ? "Cheer" : "Idle";
                    State(x, st); WalkPlayback(x, m.sp);
                }
                HoldHands(A, true, B, false, Bump(t, F + 12.2f, F + 13f, F + 20f, F + 21f), between: true); // facing the camera: A left, B right
                Expr(A, HeroFace.Expression.Happy); Expr(B, HeroFace.Expression.Happy);
                float glanceEach = Bump(t, F + 12.5f, F + 13.2f, F + 20f, F + 21f);
                A.Ik.Look(Vector3.Lerp(Camera.main.transform.position, B.FaceFront, glanceEach), 0.45f);
                B.Ik.Look(Vector3.Lerp(Camera.main.transform.position, A.FaceFront, glanceEach), 0.45f);
            }
        }
        private float KissGap()
        {
            if (kissGap < 0f) kissGap = apart - Mathf.Max(0f, Vector3.Dot(B.FaceFront - A.FaceFront, Vector3.right)) - 0.02f;
            return kissGap;
        }

        /// <summary>Two hands meet (fingers interlaced). between = the hands between two people walking side by side.</summary>
        private void HoldHands(Actor x, bool xLeft, Actor y, bool yLeft, float w, bool between)
        {
            if (w < 0.01f) return;
            Vector3 mid = (x.Feet + y.Feet) * 0.5f;
            float h = Mathf.Min(x.HandRestY, y.HandRestY) + 0.07f * propScale;
            Vector3 p = between ? mid + Vector3.up * h + (x.Forward + y.Forward).normalized * 0.06f * propScale
                                : mid + Vector3.up * h + Vector3.back * 0.12f * propScale;
            Vector3 toY = (y.Feet - x.Feet); toY.y = 0f; toY.Normalize();
            x.Ik.Reach(xLeft, p - toY * 0.025f * propScale, w, Vector3.down + Vector3.back * 0.2f);
            y.Ik.Reach(yLeft, p + toY * 0.025f * propScale, w, Vector3.down + Vector3.back * 0.2f);
            if (x.Fingers) x.Fingers.Override(xLeft, Mathf.Lerp(0.32f, 0.62f, w));
            if (y.Fingers) y.Fingers.Override(yLeft, Mathf.Lerp(0.32f, 0.62f, w));
            if (w > 0.95f) HandsHeldSeconds += Time.deltaTime * 0.5f;
        }

        private Vector3 RingPathPoint(float t, float t0, float t1, Vector3 offer, Actor receiver, bool receiverLeft)
        {
            Transform knuckle = receiver.Fingers ? receiver.Fingers.Knuckle(receiverLeft) : null;
            Transform tip = receiver.Fingers ? receiver.Fingers.FingerTip(receiverLeft) : null;
            if (!knuckle || !tip) return offer;
            Vector3 along = (tip.position - knuckle.position);
            Vector3 start = tip.position + along.normalized * 0.05f * propScale;
            return Vector3.Lerp(start, knuckle.position, Ramp(t, t0, t1));
        }

        // ------------------------------------------------------------------ cake choreography
        private Vector3 knifeRest, knifeReady;
        private void CakeHands(float t)
        {
            float top = cake.Root.transform.position.y + cake.TopOfBottomTier;
            knifeRest = KnifeRest();
            float grab = C + 8.4f, ready = C + 10.2f, cut1 = C + 11.6f, cut1End = C + 13.4f, lift1 = C + 14.2f, cut2 = C + 15.0f, cut2End = C + 16.8f, serve = C + 17.4f, served = C + 19.6f, down = C + 20.8f;
            // orientation first (the grip positions below depend on it): flat on the table, then blade down along
            // the cut line, tilted so the handle stays low and in front of the hands
            bool second = t >= lift1;
            Vector3 radial = cake.CutEdge(!second, 0f) - cake.Root.transform.position; radial.y = 0f; radial.Normalize();
            Quaternion bladeDown = Quaternion.LookRotation(radial, Vector3.Slerp(Vector3.down, -radial, 0.38f).normalized);
            float upright = t < grab ? 0f : t < served ? Ramp(t, grab, ready) : 1f - Ramp(t, served, down);
            knife.transform.rotation = Quaternion.Slerp(KnifeFlat, bladeDown, upright);
            knifeReady = Above(true) + Vector3.up * 0.06f * propScale;
            Vector3 knifeGrip;
            float depth = 0f;
            int edge = 0;
            if (t < grab) knifeGrip = knifeRest;
            else if (t < ready) knifeGrip = Vector3.Lerp(knifeRest + Vector3.up * 0.05f, knifeReady, Ramp(t, grab, ready));
            else if (t < cut1) { knifeGrip = Vector3.Lerp(knifeReady, Above(true), Ramp(t, ready, cut1)); }
            else if (t < cut1End) { depth = Ramp(t, cut1, cut1End); knifeGrip = CutGrip(true, depth); edge = 1; }
            else if (t < lift1) { knifeGrip = Vector3.Lerp(CutGrip(true, 1f), Above(false), Ramp(t, cut1End, lift1)); }
            else if (t < cut2) knifeGrip = Above(false);
            else if (t < cut2End) { depth = Ramp(t, cut2, cut2End); knifeGrip = CutGrip(false, depth); edge = 2; }
            else if (t < serve) knifeGrip = Vector3.Lerp(CutGrip(false, 1f), Above(false) + Vector3.up * 0.02f, Ramp(t, cut2End, serve));
            else if (t < served) knifeGrip = Vector3.Lerp(Above(false), plate.transform.position + new Vector3(0.12f, 0.16f, 0f) * propScale, Ramp(t, serve, served));
            else knifeGrip = Vector3.Lerp(plate.transform.position + new Vector3(0.12f, 0.16f, 0f) * propScale, knifeRest, Ramp(t, served, down));
            knife.transform.position = knifeGrip;
            bool held = t >= grab - 0.6f && t < down + 0.3f;
            float w = held ? Bump(t, grab - 1.4f, grab, down - 0.2f, down + 0.6f) : 0f;
            knifeHeld = t >= grab && t < down; // LateUpdate puts the knife in the solved hand
            cutting = edge;
            carrying = t >= cut2End && t < served;
            A.Ik.Reach(false, knifeGrip, w, Vector3.down + Vector3.back * 0.4f);
            if (A.Fingers) A.Fingers.Override(false, w > 0.3f ? 0.82f : (float?)null);
            // (the partners' inner hands stay joined while the outer hand cuts: see Bodies)
            // lean a little toward the cake while cutting
            float lean = Bump(t, grab - 1.2f, grab - 0.4f, served, served + 0.8f);
            A.Pose.Lean(6f * lean);
            // cake reacts: cut lines appear, the slice separates and slides onto the plate
            Grip(A, false, knifeGrip, w);
            cake.CutLineA.SetActive(t > cut1 + 0.25f);
            cake.CutLineB.SetActive(t > cut2 + 0.25f);
            if (Crossed(cut1End)) { Cuts = Mathf.Max(Cuts, 1); Events.Add("cut1"); }
            if (Crossed(cut2End)) { Cuts = 2; Events.Add("cut2"); }
            Vector3 home = cake.Root.transform.TransformPoint(cake.SliceHome);
            Vector3 onPlate = plate.transform.position + new Vector3(0f, 0.012f * propScale, 0f) - (cake.SliceCentreWorld - cake.Slice.transform.position) + Vector3.up * 0.0f;
            float sk = Ramp(t, serve, served);
            Vector3 lifted = Vector3.Lerp(home, onPlate, sk) + Vector3.up * Mathf.Sin(sk * Mathf.PI) * 0.06f * propScale;
            if (t > cut2End) lifted += (cake.SliceCentreWorld - home).normalized * 0.004f * Ramp(t, cut2End, serve); // a hairline gap shows the cut
            cake.Slice.transform.position = t < cut2End || t >= served ? (t >= served ? onPlate : home) : lifted;
            sliceHome = home; sliceOnPlatePos = onPlate;
            if (sk >= 1f && !SliceOnPlate) { SliceOnPlate = true; Events.Add("slice_on_plate"); }
            // goblets wait on the table until the toast
            if (t < D)
            {
                gobletA.Root.transform.SetPositionAndRotation(GobletRestA, Quaternion.identity);
                gobletB.Root.transform.SetPositionAndRotation(GobletRestB, Quaternion.identity);
            }
        }

        private float knifeScale = 1f;
        private static readonly Quaternion KnifeFlat = Quaternion.LookRotation(Vector3.right, Vector3.back); // lying on the left of the cloth, blade toward the guests
        private Vector3 KnifeRest() => T0 + new Vector3(-0.27f, tableTop + 0.006f, 0.12f);
        private Vector3 Above(bool first) => cake.CutEdge(first, 0f) + Vector3.up * 0.05f * propScale - KnifeTipFromGrip();
        /// <summary>Grip position that puts the blade TIP at this depth along the cut line (0 = frosting, 1 = bottom).</summary>
        private Vector3 CutGrip(bool first, float depth) => cake.CutEdge(first, Mathf.Lerp(-0.1f, 0.92f, depth)) - KnifeTipFromGrip();
        private Vector3 KnifeTipFromGrip() => knife.transform.rotation * new Vector3(0f, (0.055f + 0.24f) * knifeScale, 0f);

        // ------------------------------------------------------------------ toast choreography
        private void ToastHands(float t)
        {
            float pick = D + 0.8f, picked = D + 1.5f, lifted = D + 2.3f, raise = D + 5.0f, clink = D + 6.5f, back = D + 7.6f, sip0 = D + 8.6f, sip1 = D + 9.6f, sip2 = D + 12.4f, low = D + 13.6f, set0 = D + 16f, set1 = D + 17.4f;
            Vector3 restA = GobletRestA, restB = GobletRestB;
            Vector3 mid = (A.Feet + B.Feet) * 0.5f;
            float chest = Mathf.Min(A.ShoulderY, B.ShoulderY) - 0.02f;
            float bowl = 0.11f * propScale; // grip -> bowl centre
            Vector3 clinkPoint = mid + Vector3.up * (chest + 0.04f) + Vector3.back * 0.17f;
            float rim = 0.056f * propScale;
            foreach (bool isA in new[] { true, false })
            {
                Actor x = isA ? A : B;
                WeddingProps.Goblet g = isA ? gobletA : gobletB;
                bool leftHand = isA;       // inner hands: A's left, B's right (standing side by side facing the guests)
                float side = isA ? -1f : 1f;
                Vector3 rest = isA ? restA : restB;
                Vector3 handSide = leftHand ? -x.RightDir : x.RightDir;
                Vector3 held = x.Feet + Vector3.up * (x.HandRestY + 0.14f) + x.Forward * 0.13f + handSide * 0.07f;
                Vector3 clinkGrip = clinkPoint + Vector3.right * side * (rim + 0.03f) + Vector3.down * bowl;
                Vector3 mouth = x.FaceFront + Vector3.down * 0.05f + x.Forward * 0.015f;
                float tilt = 66f * Ramp(t, sip0 + 0.3f, sip1 + 0.4f) * (1f - Ramp(t, sip2, low));
                Quaternion rot = Quaternion.AngleAxis(-tilt, Vector3.Cross(Vector3.up, x.Forward)) * Quaternion.AngleAxis(side * 10f * Bump(t, raise, clink - 0.3f, back, back + 0.4f), Vector3.forward);
                Vector3 rimFromGrip = rot * Vector3.up * (0.15f * propScale) - x.Forward * (0.05f * propScale);
                Vector3 sipGrip = mouth - rimFromGrip;
                Transform shoulder = x.Ik.Arm(leftHand).upper;
                if (shoulder) sipGrip = shoulder.position + Vector3.ClampMagnitude(sipGrip - shoulder.position, x.ArmLen * 0.93f);
                x.Pose.Lean(20f * Bump(t, sip0, sip1, sip2, low));
                Vector3 grip;
                if (t < pick) grip = rest;
                else if (t < picked) grip = Vector3.Lerp(rest, rest + Vector3.up * 0.05f, Ramp(t, pick, picked));
                else if (t < raise) grip = Vector3.Lerp(rest + Vector3.up * 0.05f, held, Ramp(t, picked, lifted)); // lifted before the couple walks
                else if (t < clink) grip = Vector3.Lerp(held, clinkGrip, Ramp(t, raise, clink));
                else if (t < back) grip = Vector3.Lerp(clinkGrip, held, Ramp(t, clink + 0.3f, back));
                else if (t < sip0) grip = held;
                else if (t < sip2) grip = Vector3.Lerp(held, sipGrip, Ramp(t, sip0, sip1));
                else if (t < low) grip = Vector3.Lerp(sipGrip, held, Ramp(t, sip2, low));
                else if (t < set0) grip = held;
                else grip = Vector3.Lerp(held, rest, Ramp(t, set0, set1));
                g.Root.transform.SetPositionAndRotation(grip, t < picked ? Quaternion.identity : rot);
                float w = Bump(t, pick - 0.9f, pick, set1 - 0.2f, set1 + 0.5f);
                if (isA) gobletAHeld = t >= pick && t < set1; else gobletBHeld = t >= pick && t < set1;
                if (isA) mouthA = mouth; else mouthB = mouth;
                x.Ik.Reach(leftHand, grip, w, Vector3.down + Vector3.back * 0.3f);
                bool selfAnchored = t > back; // the sip follows the drinker's own face: stepping would not help
                Grip(x, leftHand, grip, w, !selfAnchored);
                if (x.Fingers) x.Fingers.Override(leftHand, w > 0.3f ? 0.78f : (float?)null);
                // drinking: wine goes down, head tips back a little
                g.SetLevel(1f - 0.62f * Ramp(t, sip1, sip2));
                sipping = t > sip1 && t < sip2;
                float sipLook = Bump(t, sip0, sip1, sip2, low);
                Actor other = isA ? B : A;
                Vector3 look = Vector3.Lerp(other.FaceFront, g.RimWorld + Vector3.down * 0.05f, sipLook); // eyes and face down to the cup
                x.Ik.Look(Bump(t, raise - 0.5f, raise, set0, set1) > 0.01f ? look : clinkPoint, Mathf.Lerp(0.45f, 0.85f, sipLook));
            }
            clinking = t > clink - 0.4f && t < clink + 0.4f;
            if (Crossed(clink)) { Clinked = true; Events.Add("clink"); Sparkles(clinkPoint, 10); }
        }

        // ------------------------------------------------------------------ props per frame
        private void Props(float t)
        {
            // rings: hidden in a pocket until taken out, follow the giving hand, then stay on the receiver's finger
            Ring(ringA, t, R + 3.0f, R + 12.3f, A, false, B, true, out bool onB);
            Ring(ringB, t, R + 18.6f, R + 24.4f, B, true, A, false, out bool onA);
            if (onB && !RingOnB) { RingOnB = true; Events.Add("ring_on_B"); Sfx(s.Chime, 0.9f); Sparkles(ringA.transform.position, 12); }
            if (onA && !RingOnA) { RingOnA = true; Events.Add("ring_on_A"); Sfx(s.Chime, 0.9f); Sparkles(ringB.transform.position, 12); }
            if (B.Fingers && B.Fingers.Knuckle(true)) RingToFingerB = Vector3.Distance(ringA.transform.position, B.Fingers.Knuckle(true).position);
            // kiss glow behind the couple's heads
            var sr = glow.GetComponent<SpriteRenderer>();
            float g = t >= K && t < C ? Bump(t, K + 6.5f, K + 7.6f, K + 11.6f, K + 13f) : 0f;
            glow.transform.position = (A.FaceFront + B.FaceFront) * 0.5f + Vector3.forward * 0.35f;
            glow.transform.rotation = Camera.main ? Camera.main.transform.rotation : Quaternion.identity;
            glow.transform.localScale = Vector3.one * (0.9f + 0.15f * Mathf.Sin(t * 2f));
            sr.color = new Color(1f, 0.86f, 0.9f, 0.55f * g);
            if (t < C) { CakeHandsIdle(); }
        }

        /// <summary>QA: how far a hand that should hold a prop ended up from its grip (previous frame's solve).</summary>
        public float GripErrorMax { get; private set; }
        public string GripErrorWhere { get; private set; } = "";
        private readonly Dictionary<string, Vector3> gripWanted = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, float> heldSince = new Dictionary<string, float>();
        private void Grip(Actor x, bool left, Vector3 grip, float weight, bool worldAnchored = true)
        {
            string key = (x == A ? "A" : "B") + (left ? "L" : "R");
            Transform hand = x.Ik.Arm(left).hand;
            if (hand && worldAnchored && weight > 0.5f)
            {
                // step toward a target the short chibi arm cannot reach (the IK clamps at full extension)
                Vector3 miss = grip - Palm(x, left); miss.y = 0f;
                if (miss.magnitude > 0.02f) x.Reach = Vector3.ClampMagnitude(x.Reach + miss * Mathf.Min(1f, Time.deltaTime * 5f), 0.18f);
                x.ReachUsed = true;
            }
            if (weight > 0.98f) { if (!heldSince.ContainsKey(key)) heldSince[key] = T; } else heldSince.Remove(key);
            bool settled = heldSince.TryGetValue(key, out float since) && T - since > 0.6f;
            if (hand && settled && gripWanted.TryGetValue(key, out Vector3 prev))
            {
                float e = Vector3.Distance(hand.position, prev);
                if (e > GripErrorMax) { GripErrorMax = e; GripErrorWhere = $"{key}@{T:0.0}"; }
            }
            if (weight > 0.98f) gripWanted[key] = grip; else gripWanted.Remove(key);
        }

        private void CakeHandsIdle()
        {
            if (cake == null) return;
            knife.transform.position = KnifeRest();
            knife.transform.rotation = KnifeFlat;
            cake.Slice.transform.position = cake.Root.transform.TransformPoint(cake.SliceHome);
            gobletA.Root.transform.SetPositionAndRotation(T0 + new Vector3(-0.15f * propScale, tableTop + 0.05f * propScale, 0.17f * propScale), Quaternion.identity);
            gobletB.Root.transform.SetPositionAndRotation(T0 + new Vector3(0.16f * propScale, tableTop + 0.05f * propScale, 0.12f * propScale), Quaternion.identity);
        }

        private void Ring(GameObject ring, float t, float appear, float onFinger, Actor giver, bool giverLeft, Actor receiver, bool receiverLeft, out bool on)
        {
            on = t >= onFinger;
            ring.SetActive(t >= appear);
            if (t < appear) return;
            Transform knuckle = receiver.Fingers ? receiver.Fingers.Knuckle(receiverLeft) : null;
            Transform tip = receiver.Fingers ? receiver.Fingers.FingerTip(receiverLeft) : null;
            Transform hand = giver.Ik.Arm(giverLeft).hand;
            if (on && knuckle)
            {
                Vector3 along = tip ? (tip.position - knuckle.position).normalized : knuckle.forward;
                ring.transform.position = knuckle.position + along * 0.004f;
                ring.transform.rotation = Quaternion.LookRotation(along, Vector3.up);
                return;
            }
            // in the giver's fingers, gem up; while sliding on, along the receiver's finger
            float slide = Ramp(t, onFinger - 3.3f, onFinger);
            if (slide > 0f && knuckle && tip)
            {
                Vector3 along = (tip.position - knuckle.position).normalized;
                Vector3 start = tip.position + along * 0.05f * propScale;
                ring.transform.position = Vector3.Lerp(start, knuckle.position, slide);
                ring.transform.rotation = Quaternion.LookRotation(along, Vector3.up);
            }
            else if (hand)
            {
                Transform ft = giver.Fingers ? giver.Fingers.FingerTip(giverLeft) : null;
                ring.transform.position = ft ? Vector3.Lerp(hand.position, ft.position, 0.8f) + Vector3.up * 0.02f * propScale : hand.position;
                ring.transform.rotation = Quaternion.LookRotation(giver.RightDir * (giverLeft ? 1f : -1f), Vector3.up);
            }
        }

        // ---- held props follow the SOLVED hands (after ArmIK): a hand that cannot quite reach never leaves a prop
        // floating; the QA facts below are measured from where the props really are.
        private bool knifeHeld, carrying, gobletAHeld, gobletBHeld, sipping, clinking;
        private int cutting;
        private Vector3 mouthA, mouthB, sliceHome, sliceOnPlatePos;

        private Vector3 Palm(Actor x, bool left)
        {
            Transform hand = x.Ik.Arm(left).hand;
            if (!hand) return x.Feet;
            Transform tip = x.Fingers ? x.Fingers.FingerTip(left) : null;
            return tip ? Vector3.Lerp(hand.position, tip.position, 0.42f) : hand.position;
        }

        private void LateUpdate()
        {
            if (!Running || knife == null) return;
            if (knifeHeld) knife.transform.position = Palm(A, false);
            if (gobletAHeld) gobletA.Root.transform.position = Palm(A, true);   // inner hands: A's left
            if (gobletBHeld) gobletB.Root.transform.position = Palm(B, false);  // B's right
            if (carrying) cake.Slice.transform.position += (WeddingProps.KnifeEdgeMid(knife, knifeScale) - cake.SliceCentreWorld) * 0.85f;
            float top = cake.Root.transform.position.y + cake.TopOfBottomTier;
            if (cutting > 0)
            {
                Vector3 tip = WeddingProps.KnifeTip(knife, knifeScale);
                Vector3 edge = cake.CutEdge(cutting == 1, 0f);
                // the blade must be INSIDE the cake (near the cut line), not swinging in the air beside it
                Vector2 off = new Vector2(tip.x - edge.x, tip.z - edge.z);
                if (off.magnitude < 0.06f) KnifeDepthMax = Mathf.Max(KnifeDepthMax, top - tip.y);
            }
            if (clinking) ClinkRimDistanceMin = Mathf.Min(ClinkRimDistanceMin, Vector3.Distance(gobletA.RimWorld, gobletB.RimWorld) - 2f * 0.055f * gobletA.Scale);
            if (sipping)
            {
                GobletToMouthMinA = Mathf.Min(GobletToMouthMinA, Vector3.Distance(gobletA.RimWorld, mouthA));
                GobletToMouthMinB = Mathf.Min(GobletToMouthMinB, Vector3.Distance(gobletB.RimWorld, mouthB));
            }
            cutting = 0; clinking = sipping = false;
        }

        // ------------------------------------------------------------------ guests, officiant, musician, lights
        private void Guests(float t)
        {
            Vector3 couple = (A.Feet + B.Feet) * 0.5f;
            foreach (Guest g in guests)
            {
                // turn toward the couple (slowly), look at them, wave as they pass, clap at the big moments
                Vector3 to = couple - g.Pos; to.y = 0f;
                float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                g.Yaw = Mathf.LerpAngle(g.Yaw, want, Time.deltaTime * 1.6f);
                g.Go.transform.rotation = Quaternion.Euler(0f, g.Yaw, 0f);
                g.Ik.Look(couple + Vector3.up * 1.1f, 0.7f);
                bool passing = t < O && couple.x > g.Pos.x - 2.5f && couple.x < g.Pos.x + 1.5f;
                if (passing && g.WaveAt < 0f) g.WaveAt = t;
                bool waving = g.WaveAt >= 0f && t - g.WaveAt < 2.6f;
                bool clap = (t > R + 25f && t < R + 29.5f) || (t > K + 12.5f && t < K + 19f) || (t > C + 19.6f && t < C + 24.5f) || (t > D + 6.6f && t < D + 9f) || (t > F + 3.5f);
                PlayGuest(g, waving ? "Wave" : "Idle");
                if (clap && !waving) Clap(g.Ik, t, g.Pos.x);
                else { g.Ik.Release(true); g.Ik.Release(false); }
                if (g.Face) g.Face.SetExpression(clap || waving ? HeroFace.Expression.Happy : HeroFace.Expression.Neutral);
            }
        }

        private static void Clap(ArmIK ik, float t, float phase)
        {
            if (!ik || !ik.Model) return;
            Vector3 fwd = ik.Model.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float arm = Mathf.Max(0.15f, ik.ArmLength);
            Vector3 centre = ik.ShoulderMid + fwd * arm * 0.62f + Vector3.down * arm * 0.22f;
            float k = 0.5f + 0.5f * Mathf.Cos((t * 3.2f + phase) * Mathf.PI * 2f); // ~3 claps per second
            float apartNow = Mathf.Lerp(0.015f, arm * 0.32f, k);
            ik.Reach(true, centre - right * apartNow, 1f, Vector3.down - right * 0.4f);
            ik.Reach(false, centre + right * apartNow, 1f, Vector3.down + right * 0.4f);
        }

        private void Officiant(float t)
        {
            if (!s.Officiant) return;
            bool talk = (t > O + 1f && t < O + 22.5f) || (t > R && t < R + 3.5f) || (t > K && t < K + 4f) || (t > F + 12f && t < F + 15f);
            string want = talk ? "Talk" : t > V + 21f && t < V + 23f ? "Bow" : "Idle";
            if (want != officiantState) { s.Officiant.Play(want); officiantState = want; }
            HeroFace f = s.Officiant.GetComponentInChildren<HeroFace>();
            if (f)
            {
                if (talk && !f.Speaking) f.Speak(1.2f);
                f.SetExpression(HeroFace.Expression.Happy);
            }
        }

        private string officiantState = "";

        private void MusicianPlay(float t)
        {
            if (!musicianIk || !s.Musician) return;
            // hands on the strings, plucking with the waltz (72 bpm): alternate hands, small inward strokes
            Vector3 strings = new Vector3(harp.center.x - harp.extents.x * 0.25f, harp.min.y + harp.size.y * 0.5f, harp.center.z);
            float beat = t * 72f / 60f;
            float l = Mathf.Pow(Mathf.Abs(Mathf.Sin(beat * Mathf.PI)), 4f), r = Mathf.Pow(Mathf.Abs(Mathf.Sin((beat + 0.5f) * Mathf.PI)), 4f);
            musicianIk.Reach(true, strings + new Vector3(-0.02f - 0.04f * l, 0.12f, -0.07f), 1f);
            musicianIk.Reach(false, strings + new Vector3(-0.02f - 0.04f * r, -0.08f, 0.06f), 1f);
            musicianIk.Look(strings + Vector3.up * 0.1f, 0.5f);
        }

        private void Lights(float t)
        {
            float boost = 1f + 0.18f * Bump(t, K + 6.5f, K + 8f, K + 12f, K + 14f) + 0.4f * Ramp(t, F + 3f, F + 9f);
            foreach (var (l, i) in lights) if (l) l.intensity = i * boost;
        }

        // ------------------------------------------------------------------ captions
        private void Captions(float t)
        {
            if (!dir) return;
            string a = A.Name, b = B.Name, cap = "";
            if (t < O) cap = t < E + 3.5f ? $"Lễ thành hôn của {a} và {b}" : t < E + 16.5f ? "Cô dâu, chú rể nắm tay bước vào lễ đường" : "";
            else if (t < V)
                cap = t < O + 7.5f ? "Chủ Hôn: “Kính chào quý khách đã đến chung vui cùng đôi uyên ương hôm nay.”"
                    : t < O + 14.5f ? $"Chủ Hôn: “Hôm nay, {a} và {b} sẽ cùng nhau nói lời thề hẹn trăm năm.”"
                    : t < O + 22.5f ? "Chủ Hôn: “Mong tình yêu của hai con luôn ấm áp như tiếng chuông gió nơi này.”" : "";
            else if (t < R)
                cap = t < V + 3f ? "Lời thề hôn nhân"
                    : t < V + 10f ? $"{a}: “Từ hôm nay, tôi sẽ nắm tay {b} qua mọi chuyến phiêu lưu.”"
                    : t < V + 17.5f ? $"{b}: “Và tôi sẽ luôn ở bên {a}, dù đường xa đến đâu.”"
                    : t < V + 23f ? "Chủ Hôn: “Lời thề của hai con đã được mọi người chứng giám.”" : "";
            else if (t < K)
                cap = t < R + 4f ? "Chủ Hôn: “Mời hai con trao nhẫn cưới cho nhau.”"
                    : t < R + 13f ? $"{a} quỳ gối, đeo nhẫn cưới cho {b}"
                    : t < R + 18f ? "♥" : t < R + 26f ? $"{b} đeo nhẫn cho {a}" : "";
            else if (t < C)
                cap = t < K + 4f ? "Chủ Hôn: “Ta tuyên bố hai con nên duyên vợ chồng. Hai con có thể hôn nhau!”" : "";
            else if (t < D)
                cap = t < C + 6f ? "Cùng nhau cắt bánh cưới" : t > C + 19.6f && t < C + 24f ? "Miếng bánh đầu tiên cho hạnh phúc ngọt ngào" : "";
            else if (t < F)
                cap = t < D + 5f ? "Nâng ly chúc mừng" : t < D + 8.5f ? "Chúc cho trăm năm hạnh phúc!" : "";
            else
                cap = t < F + 12f ? "Chúc mừng đôi uyên ương!" : "Chủ Hôn: “Chúc hai con mãi mãi hạnh phúc bên nhau!”";
            if (dir.Caption != cap) dir.SetCaption(cap);
        }

        // ------------------------------------------------------------------ camera
        private struct Cam { public Vector3 pos, look; public float fov; public Cam(Vector3 p, Vector3 l, float f) { pos = p; look = l; fov = f; } }
        private sealed class ShotDef
        {
            public float t0, t1, blend, push = 1.4f; public Func<float, Cam> at;
            /// <summary>Character shots were laid out for ~1.6 m bodies; the chibi heroes are ~1.1 m: pull back.</summary>
            public Cam Eval(float t) { Cam c = at(t); return new Cam(c.look + (c.pos - c.look) * push, c.look, c.fov); }
        }
        private List<ShotDef> shots;

        private List<ShotDef> Shots()
        {
            var l = new List<ShotDef>();
            void Add(float t0, float t1, float blend, Func<float, Cam> f, float push = 1.4f) => l.Add(new ShotDef { t0 = t0, t1 = t1, blend = blend, at = f, push = push });
            Vector3 Mid() => (A.Feet + B.Feet) * 0.5f;
            float faceY = Mathf.Max(A.FaceY, B.FaceY);
            // 1 entrance
            Add(E, E + 4f, 0f, t => { float k = Ramp(t, E, E + 4f); return new Cam(Vector3.Lerp(new Vector3(-5.5f, 3.4f, -9.2f), new Vector3(-7.2f, 2.6f, -7.6f), k), new Vector3(-11.4f, 1.0f, 0.9f), 38f); }, 1f);
            Add(E + 4f, E + 10f, 1.2f, t => new Cam(Mid() + new Vector3(2.6f, 1.45f, -3.7f), Mid() + new Vector3(0.5f, 0.95f, 0.3f), 34f));
            Add(E + 10f, E + 15f, 1.4f, t => new Cam(Mid() + new Vector3(1.75f, 1.2f, -2.3f), Mid() + new Vector3(0.1f, faceY * 0.8f, 0.25f), 32f));
            Add(E + 15f, O, 1.5f, t => new Cam(new Vector3(-0.6f, 2.0f, -4.6f), new Vector3(1.7f, 1.0f, 1.6f), 38f), 1.15f);
            // 2 opening
            Add(O, O + 7f, 1.6f, t => new Cam(new Vector3(2f, 2.4f, -5.2f) + Vector3.forward * 0.4f * Ramp(t, O, O + 7f), new Vector3(2f, 1.45f, 2.6f), 36f), 1.1f);
            Add(O + 7f, O + 13f, 0f, t => new Cam(new Vector3(2.7f, 1.45f, -1.4f), (s.Officiant ? s.Officiant.transform.position : stage + Vector3.forward * 3f) + Vector3.up * 1.3f, 30f));
            Add(O + 13f, O + 19f, 0f, t => { Vector3 m = s.Musician ? s.Musician.transform.position : new Vector3(8f, 0f, 3f); return new Cam(m + new Vector3(0.9f, 1.25f, -2.4f), m + new Vector3(0.45f, 0.9f, 0.1f), 32f); });
            Add(O + 19f, V, 0f, t => new Cam(stage + new Vector3(1.5f, 1.25f, -2.1f), stage + new Vector3(0f, faceY * 0.85f, 0f), 30f));
            // 3 vows
            Add(V, V + 4f, 1.4f, t => new Cam(stage + new Vector3(0f, 1.3f, -2.2f), stage + new Vector3(0f, faceY * 0.75f, 0f), 32f));
            Add(V + 4f, V + 10.5f, 0f, t => new Cam(A.FaceFront + new Vector3(0.95f, 0.05f, -1.05f), A.FaceFront + new Vector3(0.12f, -0.06f, 0f), 26f));
            Add(V + 10.5f, V + 17.5f, 0f, t => new Cam(B.FaceFront + new Vector3(-0.95f, 0.05f, -1.05f), B.FaceFront + new Vector3(-0.12f, -0.06f, 0f), 26f));
            Add(V + 17.5f, R, 0f, t => new Cam(stage + new Vector3(0f, 1.25f, -1.95f + 0.25f * Ramp(t, V + 17.5f, R)), stage + new Vector3(0f, faceY * 0.72f, 0f), 30f));
            // 4 rings
            Add(R, R + 4f, 1.2f, t => new Cam(stage + new Vector3(0f, 1.65f, -2.4f), stage + new Vector3(0f, 1.25f, 1.2f), 34f));
            Add(R + 4f, R + 7.5f, 0f, t => new Cam(stage + new Vector3(0.95f, 0.75f, -1.55f), stage + new Vector3(-0.05f, 0.62f, 0f), 34f));
            Add(R + 7.5f, R + 14.5f, 1.0f, t => { Vector3 p = HandsFocus(true); return new Cam(p + new Vector3(0.26f, 0.02f, -0.85f), p + new Vector3(-0.03f, 0.05f, 0f), 30f); }, 1.05f);
            Add(R + 14.5f, R + 19f, 0f, t => new Cam(stage + new Vector3(0.2f, 1.15f, -1.75f), stage + new Vector3(0f, faceY * 0.7f, 0f), 30f));
            Add(R + 19f, R + 26.5f, 1.0f, t => { Vector3 p = HandsFocus(false); return new Cam(p + new Vector3(-0.26f, 0.04f, -0.85f), p + new Vector3(0.03f, 0.05f, 0f), 30f); }, 1.05f);
            Add(R + 26.5f, K, 1.0f, t => new Cam(stage + new Vector3(0f, 1.3f, -1.5f), stage + new Vector3(0f, faceY * 0.85f, 0f), 30f));
            // 5 kiss: one continuous push-in, never cut away during the kiss
            Add(K, K + 4f, 1.0f, t => new Cam(stage + new Vector3(0.6f, 1.55f, -2.1f), stage + new Vector3(0f, 1.25f, 1.0f), 32f));
            Add(K + 4f, K + 13.5f, 1.2f, t =>
            {
                Vector3 kp = (A.Ik.Head.position + B.Ik.Head.position) * 0.5f + Vector3.up * 0.06f;
                float d = Mathf.Lerp(1.35f, 1.05f, Ramp(t, K + 4f, K + 11f));
                return new Cam(kp + new Vector3(0.04f, -0.1f, -d), kp + new Vector3(0f, -0.07f, 0f), 30f);
            });
            Add(K + 13.5f, C, 1.2f, t => new Cam(stage + new Vector3(0f, 1.55f, -2.3f), stage + new Vector3(0f, faceY * 0.95f, 0f), 34f));
            // 6 cake
            Add(C, C + 6.5f, 1.2f, t => new Cam(Mid() + new Vector3(0.9f, 1.4f, -3.1f), Mid() + new Vector3(0.5f, 0.8f, 0f), 34f));
            Add(C + 6.5f, C + 10f, 1.4f, t => new Cam(T0 + new Vector3(0.1f, tableTop + 0.4f, -1.4f), T0 + new Vector3(0f, tableTop + 0.25f, 0.3f), 34f));
            Add(C + 10f, C + 19.8f, 1.2f, t => new Cam(T0 + new Vector3(-0.3f, tableTop + 0.3f, -0.95f), T0 + new Vector3(-0.08f, tableTop + 0.2f, 0.16f), 36f));
            Add(C + 19.8f, D, 1.2f, t => new Cam(T0 + new Vector3(-0.1f, tableTop + 0.42f, -1.5f), T0 + new Vector3(-0.05f, tableTop + 0.26f, 0.25f), 34f));
            // 7 toast: one take from the raise through the clink to the end of the sip
            Add(D, D + 4.6f, 1.2f, t => new Cam(T0 + new Vector3(0.45f, tableTop + 0.45f, -1.6f), T0 + new Vector3(0.35f, tableTop + 0.15f, 0.2f), 36f));
            Add(D + 4.6f, D + 14.2f, 1.0f, t =>
            {
                Vector3 m = (A.Feet + B.Feet) * 0.5f;
                float d = Mathf.Lerp(1.4f, 1.15f, Ramp(t, D + 4.6f, D + 12f));
                return new Cam(m + new Vector3(0f, Mathf.Min(A.ShoulderY, B.ShoulderY) + 0.18f, -d), m + new Vector3(0f, Mathf.Min(A.ShoulderY, B.ShoulderY) + 0.04f, 0f), 32f);
            });
            Add(D + 14.2f, F, 1.2f, t => new Cam(Mid() + new Vector3(0f, 1.4f, -2.2f), Mid() + new Vector3(0f, faceY * 0.75f, 0f), 34f));
            // 8 finale
            Add(F, F + 4.6f, 1.2f, t => new Cam(Mid() + new Vector3(1.0f, 1.6f, -3.2f), Mid() + new Vector3(0.4f, 0.95f, 0.2f), 36f));
            Add(F + 4.6f, F + 11f, 1.4f, t => new Cam(new Vector3(-0.8f, 2.4f, -6.4f), new Vector3(0.6f, 1.1f, 1.3f), 42f), 1f);
            Add(F + 11f, endT + 4f, 1.6f, t => { float k = Ramp(t, F + 11f, endT); return new Cam(Vector3.Lerp(new Vector3(2f, 1.6f, -2.8f), new Vector3(2f, 3.6f, -8.5f), k), new Vector3(2f, Mathf.Lerp(0.9f, 1.4f, k), 1f), Mathf.Lerp(32f, 40f, k)); }, 1.2f);
            return l;
        }

        private Vector3 HandsFocus(bool first)
        {
            Transform k = first ? (B.Fingers ? B.Fingers.Knuckle(true) : null) : (A.Fingers ? A.Fingers.Knuckle(false) : null);
            return k ? k.position : (A.Feet + B.Feet) * 0.5f + Vector3.up * 0.6f;
        }

        private Cam? lastCam;
        private void Cameras(float t)
        {
            if (!dir) return;
            shots ??= Shots();
            int i = shots.FindIndex(x => t >= x.t0 && t < x.t1);
            if (i < 0) i = t < shots[0].t0 ? 0 : shots.Count - 1;
            ShotDef sh = shots[i];
            Cam c = sh.Eval(t);
            if (sh.blend > 0f && i > 0 && t < sh.t0 + sh.blend)
            {
                Cam p = shots[i - 1].Eval(t);
                float k = Ramp(t, sh.t0, sh.t0 + sh.blend);
                c = new Cam(Vector3.Lerp(p.pos, c.pos, k), Vector3.Lerp(p.look, c.look, k), Mathf.Lerp(p.fov, c.fov, k));
            }
            // a touch of damping: tracked points never jitter the frame
            if (lastCam.HasValue && i == lastShot)
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime * 14f);
                c = new Cam(Vector3.Lerp(lastCam.Value.pos, c.pos, k), Vector3.Lerp(lastCam.Value.look, c.look, k), Mathf.Lerp(lastCam.Value.fov, c.fov, k));
            }
            lastCam = c; lastShot = i;
            dir.SetCamera(c.pos, Quaternion.LookRotation(c.look - c.pos, Vector3.up), c.fov);
        }
        private int lastShot = -1;
        public int ShotIndex => lastShot;

        /// <summary>QA: where everyone is right now.</summary>
        public string DebugState()
        {
            var cam = Camera.main ? Camera.main.transform : null;
            return $"T={T:0.0} scene={SceneId} shot={lastShot} A:{A.Feet} yaw={A.Yaw:0} {A.State} B:{B.Feet} yaw={B.Yaw:0} {B.State} cam={(cam ? cam.position.ToString() : "-")} fwd={(cam ? cam.forward.ToString() : "-")} apart={apart:0.00} kissGap={kissGap:0.00} faces={Vector3.Distance(A.FaceFront, B.FaceFront):0.000}";
        }

        // ------------------------------------------------------------------ sound + particles
        private void Sounds(float t)
        {
            // footsteps on the walks (two per walk cycle)
            foreach (Actor x in new[] { A, B })
                if (x.State == "Walk" && x.Anim)
                {
                    float nt = x.Anim.GetCurrentAnimatorStateInfo(0).normalizedTime * 2f;
                    int step = Mathf.FloorToInt(nt);
                    if (x == A ? step != stepA : step != stepB) { if (x == A) stepA = step; else stepB = step; Sfx(s.Footstep, 0.22f); }
                }
            if (Crossed(E + 3.6f) && s.Petals) s.Petals.Burst(new Vector3(-9f, 0f, 1f), 80);
            if (Crossed(E + 9f) && s.Petals) s.Petals.Burst(new Vector3(-3f, 0f, 1f), 70);
            if (Crossed(K + 7.4f)) { Sfx(s.Sparkle, 0.9f); StartHearts(); if (s.Petals) s.Petals.Burst(stage + Vector3.up * 0.4f, 120); Events.Add("kiss"); }
            if (Crossed(K + 12.6f)) Sfx(s.Applause, 0.8f);
            if (Crossed(R + 28f)) Sfx(s.Applause, 0.55f);
            if (Crossed(C + 11.8f)) Sfx(s.KnifeCut, 0.9f);
            if (Crossed(C + 15.2f)) Sfx(s.KnifeCut, 0.9f);
            if (Crossed(C + 19.7f)) { Sfx(s.Applause, 0.6f); Sparkles(plate.transform.position + Vector3.up * 0.12f, 8); }
            if (Crossed(D + 6.5f)) Sfx(s.Clink, 1f);
            if (Crossed(F + 3.5f)) { Sfx(s.Applause, 1f); Sfx(s.Cheer, 0.7f); }
            if (Crossed(F + 5f) && s.Petals) s.Petals.Burst(stage + Vector3.up * 0.5f, 160);
            if (Crossed(F + 8.5f) && s.Petals) s.Petals.Burst(new Vector3(-1f, 0f, 1.2f), 160);
            if (Crossed(F + 12.5f)) { Sfx(s.Cheer, 0.6f); if (s.Petals) s.Petals.Burst(stage, 200); }
        }
        private int stepA = -1, stepB = -1;

        private void StartHearts()
        {
            Vector3 at = (A.FaceFront + B.FaceFront) * 0.5f + Vector3.up * 0.25f;
            for (int i = 0; i < 9; i++) SpawnFloat(WeddingProps.Star(), at + new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(-0.05f, 0.25f), -0.1f), new Color(1f, 0.6f, 0.75f), 2.4f, i * 0.18f, 0.5f);
        }

        private void Sparkles(Vector3 at, int n)
        {
            for (int i = 0; i < n; i++) SpawnFloat(WeddingProps.Star(), at + UnityEngine.Random.insideUnitSphere * 0.08f, new Color(1f, 0.95f, 0.75f), 1.1f, i * 0.04f, 0.18f);
        }

        private void SpawnFloat(Sprite sp, Vector3 at, Color c, float life, float delay, float size)
        {
            var go = new GameObject("Sparkle");
            spawned.Add(go);
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp; sr.color = new Color(c.r, c.g, c.b, 0f); sr.sortingOrder = 46;
            var f = go.AddComponent<FloatAway>();
            f.Init(life, delay, size, c);
        }

        /// <summary>Drifting sparkle / heart: fades in, rises, sways, fades out, always faces the camera.</summary>
        private sealed class FloatAway : MonoBehaviour
        {
            private float life, delay, size, born; private Color c; private SpriteRenderer sr; private Vector3 p0; private float sway;
            public void Init(float l, float d, float s, Color col) { life = l; delay = d; size = s; c = col; born = Time.time; sr = GetComponent<SpriteRenderer>(); p0 = transform.position; sway = UnityEngine.Random.Range(0f, 6f); }
            private void Update()
            {
                float k = (Time.time - born - delay) / life;
                if (k < 0f) return;
                if (k >= 1f) { Destroy(gameObject); return; }
                transform.position = p0 + new Vector3(Mathf.Sin(k * 5f + sway) * 0.06f, k * 0.5f, 0f);
                transform.localScale = Vector3.one * size * Mathf.Lerp(0.6f, 1.2f, Mathf.Sin(k * Mathf.PI));
                if (Camera.main) transform.rotation = Camera.main.transform.rotation;
                sr.color = new Color(c.r, c.g, c.b, Mathf.Sin(k * Mathf.PI));
            }
        }
    }
}
