using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Sảnh Kết Hôn rites.
    /// <para><b>Real wedding (two players):</b> an engaged couple talking to the Chủ Hôn gets the ceremony buttons
    /// (Social → Hôn Nhân). Once both confirm, the SERVER runs the timeline (intro, vows, rings, cake, toast, kiss,
    /// finale) and every client in the hall — the couple and any guests — plays the same staging from the server clock:
    /// both heroes placed side by side (the local hero moved, remote heroes overridden), bow at the vows, the ring offered
    /// on one knee, cutting the cake together with a knife (procedural arm strokes, a slice served), both raising
    /// goblets, the kiss (both lean in, hearts), the celebration; eased cinematic camera; weapons put away. When the
    /// server reports the end, the marriage and the wedding gifts are already saved on the server.</para>
    /// <para><b>Rehearsal (one player):</b> anyone else talking to the Chủ Hôn sees the solo walkthrough.</para>
    /// </summary>
    public sealed class WeddingCeremony : MonoBehaviour
    {
        [SerializeField] private NpcTalkSpot officiantTalk;
        [SerializeField] private NpcActor officiant, musician;
        [SerializeField] private PetalFall petals;
        [SerializeField] private Vector3 stage = new Vector3(2f, 0f, 3.2f), cake = new Vector3(19f, 0f, 2.8f), banquet = new Vector3(14f, 0f, 3f), entrance = new Vector3(-14f, 0f, 1f);
        [SerializeField] private Material glassMaterial, wineMaterial, goldMaterial, steelMaterial, cakeMaterial;

        public int Played { get; private set; }
        public int RealPlayed { get; private set; }
        public string RealStep { get; private set; } = "";
        public bool Playing => CinematicDirector.Current && CinematicDirector.Current.Playing;
        public bool RealPlaying { get; private set; }
        /// <summary>Hero gestures played (tests): rehearsal "Bow", "Kneel", "Celebrate", "Toast", "Cheer"; real wedding "A:Kneel" …</summary>
        public List<string> HeroGestures { get; } = new List<string>();
        public int Hearts { get; private set; }
        public bool SliceServed => slice;

        private void Start() { if (officiantTalk) officiantTalk.Talked += OnOfficiant; }
        private void OnDestroy() { if (officiantTalk) officiantTalk.Talked -= OnOfficiant; }

        /// <summary>Engaged → the ceremony buttons; otherwise the solo rehearsal.</summary>
        private void OnOfficiant()
        {
            var world = AdventureWorldSession.Current;
            var net = AdventureServerConnection.Instance;
            if (!world || world.Character == null || !net) { Begin(); return; }
            net.GetMarriage(world.Character.characterId, (ok, m, err) =>
            {
                if (ok && m != null && m.status == "engaged" && SocialPanel.Instance)
                {
                    SocialPanel.Instance.OpenTab("marriage");
                    return;
                }
                Begin();
            });
        }

        private void Update()
        {
            CeremonyState c = WorldSync.Current && WorldSync.Current.Last != null ? WorldSync.Current.Last.ceremony : null;
            if (c == null || !c.Valid || c.state != "running" || c.id == playedId) return;
            if (WorldSync.Current.ServerNowMs > c.endMs - 2000) return; // too late to join
            if (Participant(c.aId, out _, out _) && Participant(c.bId, out _, out _))
            {
                playedId = c.id;
                StartCoroutine(Real(c));
            }
        }

        // ================================================================== real wedding (server timeline)
        private string playedId = "";

        /// <summary>The local hero or a remote avatar for this character id.</summary>
        private bool Participant(string id, out CharacterVisual3D visual, out PoseOverlay pose)
        {
            visual = null; pose = null;
            var world = AdventureWorldSession.Current;
            if (world && world.Character != null && world.Character.characterId == id)
            {
                visual = world.PlayerVisual;
                if (world.HeroModel)
                {
                    pose = world.HeroModel.GetComponent<PoseOverlay>();
                    if (!pose) { pose = world.HeroModel.AddComponent<PoseOverlay>(); pose.Bind(world.HeroModel.transform); }
                }
                return visual;
            }
            RemoteAvatar a = WorldSync.Current ? WorldSync.Current.Avatar(id) : null;
            if (!a) return false;
            visual = a.Visual; pose = a.Pose;
            return visual;
        }

        /// <summary>Places a participant (feet on the walking line at x), facing ±1.</summary>
        private void Place(string id, float x, int facing)
        {
            var world = AdventureWorldSession.Current;
            if (world && world.Character != null && world.Character.characterId == id)
            {
                var rb = world.Player.GetComponent<Rigidbody2D>();
                if (rb) { rb.position = new Vector2(x, rb.position.y); rb.linearVelocity = Vector2.zero; }
                world.Player.transform.position = new Vector3(x, world.Player.transform.position.y, world.Player.transform.position.z);
                world.Player.FaceTowards(facing);
                return;
            }
            RemoteAvatar a = WorldSync.Current ? WorldSync.Current.Avatar(id) : null;
            if (a) a.Override(new Vector3(x, 0f, 0f), facing);
        }

        private void Emote(string who, CharacterVisual3D v, string state, HeroFace.Expression e = HeroFace.Expression.Happy)
        {
            if (v && v.PlayEmote(state, e, 3f)) HeroGestures.Add(who + ":" + state);
        }

        private List<CinematicDirector.Shot> RealShots(CeremonyState c, int fromStep)
        {
            float s = stage.x;
            var all = new List<CinematicDirector.Shot>();
            foreach (CeremonyStepState st in c.steps)
            {
                float secs = st.ms / 1000f, move = Mathf.Min(1.4f, secs * 0.35f);
                CinematicDirector.Shot shot = st.id switch
                {
                    "intro" => new CinematicDirector.Shot(new Vector3(s, 4.6f, -14f), new Vector3(s, 2.4f, 2.5f), 40f, move, secs - move, $"Lễ thành hôn của {c.aName} và {c.bName}"),
                    "vows" => new CinematicDirector.Shot(new Vector3(s, 2.3f, -7.8f), new Vector3(s, 1.5f, 1.8f), 32f, move, secs - move, "Chủ Hôn: “Hai con có nguyện cùng nhau đi hết mọi hành trình?”"),
                    "rings" => new CinematicDirector.Shot(new Vector3(s - 0.2f, 1.5f, -5.6f), new Vector3(s, 0.95f, 1.2f), 30f, move, secs - move, "Trao nhẫn cưới"),
                    "cake" => new CinematicDirector.Shot(new Vector3(cake.x - 0.4f, 1.9f, -6.0f), new Vector3(cake.x - 0.4f, 1.15f, 1.4f), 32f, move, secs - move, "Cùng nhau cắt bánh cưới"),
                    "toast" => new CinematicDirector.Shot(new Vector3(banquet.x, 1.9f, -6.2f), new Vector3(banquet.x, 1.2f, 1.2f), 30f, move, secs - move, "Nâng ly chúc mừng"),
                    "kiss" => new CinematicDirector.Shot(new Vector3(s, 1.7f, -5.0f), new Vector3(s, 1.15f, 0.8f), 26f, move, secs - move, "♥"),
                    _ => new CinematicDirector.Shot(new Vector3(s + 1f, 3.6f, -12f), new Vector3(s + 1f, 2.6f, 2f), 42f, move, secs - move, $"Chúc mừng {c.aName} ♥ {c.bName}!"),
                };
                all.Add(shot);
            }
            return all.Skip(fromStep).ToList();
        }

        /// <summary>The pose that cuts the cake in the real ceremony (tests).</summary>
        public PoseOverlay CutPose { get; private set; }

        private IEnumerator Real(CeremonyState c)
        {
            RealPlaying = true;
            RealPlayed++;
            var dir = CinematicDirector.Current;
            long Now() => WorldSync.Current ? WorldSync.Current.ServerNowMs : c.startMs;
            // wait for the server lead-in
            while (Now() < c.startMs) yield return null;
            int first = 0;
            while (first < c.steps.Length - 1 && Now() >= c.steps[first].startMs + c.steps[first].ms) first++;
            Participant(c.aId, out CharacterVisual3D va, out PoseOverlay pa);
            Participant(c.bId, out CharacterVisual3D vb, out PoseOverlay pb);
            foreach (var v in new[] { va, vb }) if (v) v.SetWeaponsVisible(false);
            if (officiant) officiant.FaceFront();
            if (dir) dir.Play(RealShots(c, first));
            for (int i = first; i < c.steps.Length; i++)
            {
                CeremonyStepState st = c.steps[i];
                while (Now() < st.startMs) yield return null;
                RealStep = st.id;
                if (dir) dir.Cue(i - first); // the camera follows the server's step clock, never lags behind it
                float s = stage.x;
                switch (st.id)
                {
                    case "intro":
                        Place(c.aId, s - 0.85f, 1); Place(c.bId, s + 0.85f, -1);
                        if (musician) StartCoroutine(musician.Gesture("Wave", 1.6f));
                        break;
                    case "vows":
                        Place(c.aId, s - 0.85f, 1); Place(c.bId, s + 0.85f, -1);
                        if (officiant) StartCoroutine(officiant.Gesture("Talk", 3f));
                        Emote("A", va, "Bow"); Emote("B", vb, "Bow");
                        break;
                    case "rings":
                        Place(c.aId, s - 0.75f, 1); Place(c.bId, s + 0.75f, -1);
                        if (va) va.HoldInRightHand(Ring());
                        Emote("A", va, "Kneel", HeroFace.Expression.Happy);
                        Emote("B", vb, "Cheer", HeroFace.Expression.Surprised);
                        if (petals) petals.Burst(new Vector3(s, 0f, 1f), 80);
                        break;
                    case "cake":
                        if (va) va.HoldInRightHand(null);
                        Place(c.aId, cake.x - 1.05f, 1); Place(c.bId, cake.x + 0.55f, -1); // flanking the cake, the knife reaches it
                        if (va) va.HoldInRightHand(Knife());
                        if (pa) pa.Cut(3.4f);
                        CutPose = pa;
                        if (va && va.Face) va.Face.Pulse(HeroFace.Expression.Happy, 3f);
                        Emote("B", vb, "Sway"); // the partner sways happily beside the cake while it is cut
                        StartCoroutine(After(3.3f, () => { ServeSlice(); if (petals) petals.Burst(cake, 60); }));
                        break;
                    case "toast":
                        if (va) va.HoldInRightHand(null);
                        // the drink take lifts the LEFT hand: each faces so that hand is toward the camera
                        Place(c.aId, banquet.x + 0.75f, -1); Place(c.bId, banquet.x - 0.75f, 1);
                        if (va) va.HoldInHand(WineGlass(), left: true);
                        if (vb) vb.HoldInHand(WineGlass(), left: true);
                        Emote("A", va, "Toast"); Emote("B", vb, "Toast");
                        break;
                    case "kiss":
                        foreach (var v in new[] { va, vb }) if (v) v.HoldInRightHand(null);
                        Place(c.aId, s - 0.36f, 1); Place(c.bId, s + 0.36f, -1);
                        if (pa) pa.Lean(18f);
                        if (pb) pb.Lean(18f);
                        // eyes softly closed + blush during the kiss (the Happy face closes the eyes as ∪ arcs)
                        foreach (var v in new[] { va, vb }) if (v && v.Face) v.Face.Pulse(HeroFace.Expression.Happy, 2.8f);
                        StartCoroutine(HeartBurst(new Vector3(s, 2.0f, -0.2f)));
                        StartCoroutine(After(2.6f, () =>
                        {
                            // after the kiss: step back a little, a heart over the head, the partner sways
                            if (pa) pa.Lean(0f);
                            if (pb) pb.Lean(0f);
                            Place(c.aId, s - 0.6f, 1); Place(c.bId, s + 0.6f, -1);
                            Emote("A", va, "Sway"); Emote("B", vb, "Heart"); // the clip turns the body: from the right-hand side the heart faces the camera
                        }));
                        break;
                    default: // finale
                        if (pa) pa.Lean(0f);
                        if (pb) pb.Lean(0f);
                        Place(c.aId, s - 0.85f, 1); Place(c.bId, s + 0.85f, -1);
                        Emote("A", va, "Celebrate"); Emote("B", vb, "Celebrate");
                        if (petals) petals.Burst(new Vector3(s, 0f, 2f), 160);
                        if (musician) StartCoroutine(musician.Gesture("Bow", 1.8f));
                        break;
                }
            }
            while (Now() < c.endMs) yield return null;
            // hand everything back
            foreach (var (v, p) in new[] { (va, pa), (vb, pb) })
            {
                if (v) { v.HoldInRightHand(null); v.SetWeaponsVisible(true); }
                if (p) p.Lean(0f);
            }
            foreach (string id in new[] { c.aId, c.bId })
            {
                RemoteAvatar a = WorldSync.Current ? WorldSync.Current.Avatar(id) : null;
                if (a) a.Override(null);
            }
            while (dir && dir.Playing) yield return null;
            RealStep = "done";
            RealPlaying = false;
            if (SocialPanel.Instance) SocialPanel.Instance.Refresh();
        }

        // ---------------------------------------------------------------- props (generated, no Meshy credit)
        private GameObject slice;

        private void ServeSlice()
        {
            if (slice) Destroy(slice);
            slice = new GameObject("CakeSlice");
            slice.transform.position = new Vector3(cake.x - 0.45f, 1.02f, 1.6f);
            slice.AddComponent<MeshFilter>().sharedMesh = Wedge();
            var r = slice.AddComponent<MeshRenderer>();
            r.sharedMaterial = cakeMaterial;
            StartCoroutine(Pop(slice.transform));
        }

        private static IEnumerator Pop(Transform t)
        {
            for (float k = 0f; k < 1f && t; k += Time.deltaTime / 0.35f) { t.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, k); yield return null; }
        }

        private static Mesh wedge;
        private static Mesh Wedge()
        {
            if (wedge) return wedge;
            // a slice of a round cake: 40° wedge, radius 0.22 m, height 0.16 m, cream with a pink layer (vertex colors unused)
            var v = new List<Vector3>(); var t = new List<int>();
            const int seg = 6; const float r = 0.22f, h = 0.16f, a0 = -20f * Mathf.Deg2Rad, a1 = 20f * Mathf.Deg2Rad;
            v.Add(Vector3.zero); v.Add(new Vector3(0f, h, 0f));
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg);
                v.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                v.Add(new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r));
            }
            for (int i = 0; i < seg; i++)
            {
                int b = 2 + i * 2;
                t.AddRange(new[] { 1, b + 3, b + 1 });          // top
                t.AddRange(new[] { 0, b, b + 2 });              // bottom
                t.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 }); // outer
            }
            int last = 2 + seg * 2;
            t.AddRange(new[] { 0, 1, 3, 0, 3, 2 });             // cut faces
            t.AddRange(new[] { 0, last, last + 1, 0, last + 1, 1 });
            wedge = new Mesh { name = "cake_slice" };
            wedge.SetVertices(v); wedge.SetTriangles(t, 0); wedge.RecalculateNormals(); wedge.RecalculateBounds();
            return wedge;
        }

        private GameObject Ring()
        {
            var go = new GameObject("WeddingRing");
            go.AddComponent<MeshFilter>().sharedMesh = Torus();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = goldMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var inner = new GameObject("Gem");
            inner.transform.SetParent(go.transform, false);
            inner.transform.localPosition = new Vector3(0f, 0.105f, 0f);
            inner.transform.localScale = Vector3.one * 0.05f;
            inner.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            inner.AddComponent<MeshRenderer>().sharedMaterial = glassMaterial;
            go.transform.localScale = Vector3.one * 1.6f;
            return go;
        }

        private static Mesh torus;
        private static Mesh Torus()
        {
            if (torus) return torus;
            const int seg = 28, side = 10; const float R = 0.08f, r = 0.016f;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
                for (int j = 0; j <= side; j++)
                {
                    float a = i * Mathf.PI * 2f / seg, b = j * Mathf.PI * 2f / side;
                    var c = new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0f);
                    var d = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a) * Mathf.Cos(b), Mathf.Sin(b));
                    v.Add(c + d * r); n.Add(d);
                }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    int a0 = i * (side + 1) + j, a1 = a0 + side + 1;
                    t.AddRange(new[] { a0, a1, a0 + 1, a0 + 1, a1, a1 + 1 });
                }
            torus = new Mesh { name = "ring" };
            torus.SetVertices(v); torus.SetNormals(n); torus.SetTriangles(t, 0); torus.RecalculateBounds();
            return torus;
        }

        private GameObject Knife()
        {
            var root = new GameObject("CakeKnife");
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(blade.GetComponent<Collider>());
            blade.transform.SetParent(root.transform, false);
            blade.transform.localPosition = new Vector3(0f, 0.19f, 0f);
            blade.transform.localScale = new Vector3(0.012f, 0.26f, 0.05f);
            blade.GetComponent<MeshRenderer>().sharedMaterial = steelMaterial;
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(handle.GetComponent<Collider>());
            handle.transform.SetParent(root.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            handle.transform.localScale = new Vector3(0.03f, 0.11f, 0.035f);
            handle.GetComponent<MeshRenderer>().sharedMaterial = goldMaterial;
            root.transform.localScale = Vector3.one * 1.5f;
            return root;
        }

        private static Sprite heart;
        private static Sprite Heart()
        {
            if (heart) return heart;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2.4f - 1.2f, w = (y + 0.5f) / n * 2.4f - 1.1f;
                    float f = Mathf.Pow(u * u + w * w - 1f, 3f) - u * u * w * w * w; // classic heart curve
                    px[y * n + x] = f <= 0f ? new Color(1f, 0.42f, 0.58f, 1f) : Color.clear;
                }
            tex.SetPixels(px); tex.Apply();
            return heart = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 128f);
        }

        private IEnumerator HeartBurst(Vector3 at)
        {
            for (int i = 0; i < 9; i++)
            {
                Hearts++;
                var go = new GameObject("Heart");
                go.transform.position = at + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.1f, 0.2f), 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Heart();
                sr.sortingOrder = 46;
                StartCoroutine(Float(go, sr));
                yield return new WaitForSeconds(0.22f);
            }
        }

        private static IEnumerator Float(GameObject go, SpriteRenderer sr)
        {
            Vector3 p0 = go.transform.position;
            float sway = Random.Range(0f, 6f);
            for (float k = 0f; k < 1f && go; k += Time.deltaTime / 2f)
            {
                go.transform.position = p0 + new Vector3(Mathf.Sin(k * 6f + sway) * 0.15f, k * 1.2f, 0f);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.1f, Mathf.Sin(k * Mathf.PI));
                sr.color = new Color(1f, 1f, 1f, 1f - k * k);
                yield return null;
            }
            if (go) Destroy(go);
        }

        // ================================================================== solo rehearsal (unchanged flow)
        public List<CinematicDirector.Shot> Shots()
        {
            return new List<CinematicDirector.Shot>
            {
                new CinematicDirector.Shot(new Vector3(2f, 5.2f, -15f), new Vector3(2f, 2.6f, 3f), 40f, 1.4f, 2.2f, "Sảnh Kết Hôn — diễn tập nghi thức cưới"),
                new CinematicDirector.Shot(entrance + new Vector3(-1.5f, 1.8f, -5.5f), entrance + new Vector3(4f, 1.4f, 2f), 34f, 2.0f, 1.6f, "Lối vào: cô dâu chú rể bước qua thảm hoa"),
                new CinematicDirector.Shot(new Vector3(stage.x - 1.2f, 2.3f, -7.5f), new Vector3(stage.x - 0.4f, 1.5f, 1.6f), 32f, 2.4f, 2.6f, "Chủ Hôn: “Hôm nay chúng ta cùng chứng giám lời thề của hai người.”"),
                // the hero stands on the walking line (z ≈ 0) in front of each set piece (z ≈ 3): camera well in front, aimed between
                new CinematicDirector.Shot(new Vector3(stage.x - 0.6f, 1.6f, -5.6f), new Vector3(stage.x - 0.6f, 1.05f, 1.4f), 30f, 1.6f, 2.6f, "Trao nhẫn"),
                new CinematicDirector.Shot(new Vector3(cake.x - 1.0f, 2.0f, -6.6f), new Vector3(cake.x - 0.8f, 1.1f, 1.3f), 32f, 2.2f, 2.2f, "Cắt bánh cưới"),
                new CinematicDirector.Shot(new Vector3(banquet.x + 0.6f, 2.0f, -6.6f), new Vector3(banquet.x + 0.6f, 1.05f, 1.3f), 32f, 1.8f, 2.6f, "Nâng ly chúc mừng"),
                new CinematicDirector.Shot(new Vector3(4f, 3.6f, -12f), new Vector3(4f, 2.8f, 2f), 42f, 2.0f, 2.8f,
                    "Diễn tập xong! Kết bạn, cầu hôn rồi cùng người ấy trở lại đây để làm lễ thật."),
            };
        }

        /// <summary>Starts the solo rehearsal (Officiant conversation when not engaged, or tests).</summary>
        public void Begin()
        {
            var dir = CinematicDirector.Current;
            if (!dir || dir.Playing) return;
            Played++;
            PlaceHero(stage.x - 1.4f, 1);
            if (officiant) officiant.FaceFront();
            if (musician) musician.FaceFront();
            dir.ShotStarted += OnShot;
            shots = Shots();
            dir.Play(shots, () =>
            {
                dir.ShotStarted -= OnShot;
                if (officiant) officiant.Play("Idle");
                foreach (var h in Heroes()) { h.HoldInRightHand(null); h.SetWeaponsVisible(true); }
            });
        }

        private CharacterVisual3D PlaceHero(float x, int facing)
        {
            var world = AdventureWorldSession.Current;
            if (!world || !world.Player) return null;
            var rb = world.Player.GetComponent<Rigidbody2D>();
            if (rb) { rb.position = new Vector2(x, rb.position.y); rb.linearVelocity = Vector2.zero; }
            world.Player.transform.position = new Vector3(x, world.Player.transform.position.y, world.Player.transform.position.z);
            world.Player.FaceTowards(facing);
            return world.Player.GetComponentInChildren<CharacterVisual3D>();
        }

        private void Gesture(CharacterVisual3D hero, string state, HeroFace.Expression e = HeroFace.Expression.Happy)
        {
            if (!hero) return;
            if (state == "Toast") hero.HoldInHand(WineGlass(), left: true); // the Meshy drink take lifts the LEFT hand (measured)
            else hero.HoldInRightHand(null);
            if (hero.PlayEmote(state, e)) HeroGestures.Add(state);
        }

        /// <summary>Gilded goblet (generated lathe mesh). It sits ABOVE the fist (offset along the hand socket's +Y,
        /// the direction a held weapon points) so the bowl and the wine are visible instead of hidden in the hand.</summary>
        private GameObject WineGlass()
        {
            var holder = new GameObject("WineGlassHolder");
            var root = new GameObject("WineGlass");
            root.transform.SetParent(holder.transform, false);
            root.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            Vector2[] p = { new(0f, -0.05f), new(0.04f, -0.05f), new(0.04f, -0.044f), new(0.008f, -0.038f), new(0.008f, 0.03f),
                            new(0.034f, 0.05f), new(0.048f, 0.085f), new(0.046f, 0.12f) };
            root.AddComponent<MeshFilter>().sharedMesh = Lathe("glass2", p);
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterial = glassMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var wine = new GameObject("Wine");
            wine.transform.SetParent(root.transform, false);
            wine.AddComponent<MeshFilter>().sharedMesh = Lathe("wine2", new[] { new Vector2(0f, 0.046f), new Vector2(0.03f, 0.056f), new Vector2(0.042f, 0.09f), new Vector2(0f, 0.09f) });
            var wr = wine.AddComponent<MeshRenderer>();
            wr.sharedMaterial = wineMaterial;
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            holder.transform.localScale = Vector3.one * 2.6f;
            return holder;
        }

        private static readonly Dictionary<string, Mesh> lathes = new Dictionary<string, Mesh>();
        private static Mesh Lathe(string key, Vector2[] profile, int seg = 20)
        {
            if (lathes.TryGetValue(key, out Mesh cached) && cached) return cached;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < profile.Length; i++)
                for (int s = 0; s <= seg; s++)
                {
                    float a = s * Mathf.PI * 2f / seg;
                    v.Add(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x));
                }
            for (int i = 0; i < profile.Length - 1; i++)
                for (int s = 0; s < seg; s++)
                {
                    int a0 = i * (seg + 1) + s, b0 = a0 + seg + 1;
                    t.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1, a0, a0 + 1, b0, a0 + 1, b0 + 1, b0 }); // both sides
                }
            var m = new Mesh { name = "lathe_" + key };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return lathes[key] = m;
        }

        private IEnumerator After(float seconds, System.Action a) { yield return new WaitForSeconds(seconds); a(); }

        private void OnShot(int i)
        {
            float move = i < shots.Count ? shots[i].move : 0f;
            StartCoroutine(After(move * 0.5f, () => OnShotSettled(i)));
            if (i == 0) foreach (var h in Heroes()) h.SetWeaponsVisible(false);
        }

        private IEnumerable<CharacterVisual3D> Heroes()
        {
            var world = AdventureWorldSession.Current;
            if (world && world.Player) foreach (var v in world.Player.GetComponentsInChildren<CharacterVisual3D>()) yield return v;
        }

        private List<CinematicDirector.Shot> shots = new List<CinematicDirector.Shot>();

        private void OnShotSettled(int i)
        {
            switch (i)
            {
                case 1: if (musician) StartCoroutine(musician.Gesture("Wave", 1.6f)); break;
                case 2:
                    if (officiant) StartCoroutine(officiant.Gesture("Talk", 2.4f));
                    Gesture(PlaceHero(stage.x - 1.4f, 1), "Bow", HeroFace.Expression.Happy);
                    break;
                case 3:
                    if (officiant) StartCoroutine(officiant.Gesture("Bow", 2f));
                    Gesture(PlaceHero(stage.x - 1.4f, 1), "Kneel", HeroFace.Expression.Surprised);
                    if (petals) petals.Burst(stage + Vector3.up * 0.5f, 80);
                    break;
                case 4:
                    Gesture(PlaceHero(cake.x - 1.4f, 1), "Celebrate");
                    if (petals) petals.Burst(cake, 60);
                    break;
                case 5:
                    if (musician) StartCoroutine(musician.Gesture("Bow", 1.8f));
                    Gesture(PlaceHero(banquet.x + 1.4f, -1), "Toast");
                    if (petals) petals.Burst(banquet, 60);
                    break;
                case 6:
                    Gesture(PlaceHero(stage.x - 1.4f, 1), "Cheer");
                    if (petals) petals.Burst(new Vector3(4f, 0f, 2f), 140);
                    break;
            }
        }
    }
}
