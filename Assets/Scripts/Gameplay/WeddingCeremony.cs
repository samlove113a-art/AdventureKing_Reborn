using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Sảnh Kết Hôn ceremony REHEARSAL (diễn tập): talking to the Chủ Hôn plays the cinematic of the rite —
    /// entrance down the aisle, the vows on the flower stage, the rings on their cushion, the cake, the toast at
    /// the banquet, a petal shower — with the NPCs bowing / talking / waving and petal bursts at each moment.
    /// The player's own hero takes part: bows at the vows, kneels with the ring, celebrates at the cake, raises a toast,
    /// cheers at the end (Meshy library gestures). The real two-player wedding (proposal, partner, rings as items, the
    /// couple's kiss and cutting the cake together) needs the marriage system on the server, which is not built yet;
    /// the captions say so. Nothing here changes game state.
    /// </summary>
    public sealed class WeddingCeremony : MonoBehaviour
    {
        [SerializeField] private NpcTalkSpot officiantTalk;
        [SerializeField] private NpcActor officiant, musician;
        [SerializeField] private PetalFall petals;
        [SerializeField] private Vector3 stage = new Vector3(2f, 0f, 3.2f), cake = new Vector3(19f, 0f, 2.8f), banquet = new Vector3(14f, 0f, 3f), entrance = new Vector3(-14f, 0f, 1f);

        public int Played { get; private set; }
        public bool Playing => CinematicDirector.Current && CinematicDirector.Current.Playing;

        private void Start()
        {
            if (officiantTalk) officiantTalk.Talked += Begin;
        }

        private void OnDestroy()
        {
            if (officiantTalk) officiantTalk.Talked -= Begin;
        }

        public List<CinematicDirector.Shot> Shots()
        {
            Vector3 up = Vector3.up;
            return new List<CinematicDirector.Shot>
            {
                new CinematicDirector.Shot(new Vector3(2f, 5.2f, -15f), new Vector3(2f, 2.6f, 3f), 40f, 1.4f, 2.2f, "Sảnh Kết Hôn — diễn tập nghi thức cưới"),
                new CinematicDirector.Shot(entrance + new Vector3(-1.5f, 1.8f, -5.5f), entrance + new Vector3(4f, 1.4f, 2f), 34f, 2.0f, 1.6f, "Lối vào: cô dâu chú rể bước qua thảm hoa"),
                new CinematicDirector.Shot(new Vector3(stage.x - 1.2f, 2.3f, -7.5f), new Vector3(stage.x - 0.4f, 1.5f, 1.6f), 32f, 2.4f, 2.6f, "Chủ Hôn: “Hôm nay chúng ta cùng chứng giám lời thề của hai người.”"),
                // The hero stands on the walking line (z ≈ 0) in front of each set piece (z ≈ 3): these shots keep the
                // camera well in front of the hero (z ≤ -5.6) and aim between the two, so both are in frame.
                new CinematicDirector.Shot(new Vector3(stage.x - 0.6f, 1.6f, -5.6f), new Vector3(stage.x - 0.6f, 1.05f, 1.4f), 30f, 1.6f, 2.6f, "Trao nhẫn"),
                new CinematicDirector.Shot(new Vector3(cake.x - 1.0f, 2.0f, -6.6f), new Vector3(cake.x - 0.8f, 1.1f, 1.3f), 32f, 2.2f, 2.2f, "Cắt bánh cưới"),
                new CinematicDirector.Shot(new Vector3(banquet.x + 0.6f, 2.0f, -6.6f), new Vector3(banquet.x + 0.6f, 1.05f, 1.3f), 32f, 1.8f, 2.6f, "Nâng ly chúc mừng"),
                new CinematicDirector.Shot(new Vector3(4f, 3.6f, -12f), new Vector3(4f, 2.8f, 2f), 42f, 2.0f, 2.8f,
                    "Chúc mừng! (Lễ cưới thật cho hai người chơi sẽ mở khi hệ thống kết hôn hoàn thành.)"),
            };
        }

        /// <summary>Starts the rehearsal (from the Officiant's conversation, or tests).</summary>
        public void Begin()
        {
            var dir = CinematicDirector.Current;
            if (!dir || dir.Playing) return;
            Played++;
            PlaceHero(stage.x - 1.4f, 1); // the groom/bride waits at the stage (during the wide opening shot)
            if (officiant) officiant.FaceFront(); // faces the guests, not the player who talked to him
            if (musician) musician.FaceFront();
            dir.ShotStarted += OnShot;
            shots = Shots();
            dir.Play(shots, () =>
            {
                dir.ShotStarted -= OnShot;
                if (officiant) officiant.Play("Idle");
                foreach (var h in Heroes()) { h.HoldInRightHand(null); h.SetWeaponsVisible(true); } // weapons back, glass gone
            });
        }

        /// <summary>Hero gestures played during the rehearsal (for tests): "Bow", "Kneel", "Celebrate", "Toast", "Cheer".</summary>
        public List<string> HeroGestures { get; } = new List<string>();

        /// <summary>Puts the player's hero at x on the walking line (only on camera cuts, while the camera looks elsewhere).</summary>
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

        [SerializeField] private Material glassMaterial, wineMaterial;

        /// <summary>Small crystal wine glass (generated lathe mesh) for the toast; origin = where the fingers hold the stem.</summary>
        private GameObject WineGlass()
        {
            var root = new GameObject("WineGlass");
            // profile (radius, height) from the foot up: foot, stem, bowl
            Vector2[] p = { new(0f, -0.05f), new(0.035f, -0.05f), new(0.035f, -0.045f), new(0.006f, -0.04f), new(0.006f, 0.03f),
                            new(0.03f, 0.05f), new(0.042f, 0.08f), new(0.04f, 0.11f) };
            root.AddComponent<MeshFilter>().sharedMesh = Lathe("glass", p);
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterial = glassMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var wine = new GameObject("Wine");
            wine.transform.SetParent(root.transform, false);
            wine.AddComponent<MeshFilter>().sharedMesh = Lathe("wine", new[] { new Vector2(0f, 0.042f), new Vector2(0.026f, 0.052f), new Vector2(0.036f, 0.075f), new Vector2(0f, 0.075f) });
            var wr = wine.AddComponent<MeshRenderer>();
            wr.sharedMaterial = wineMaterial;
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.transform.localScale = Vector3.one * 2.3f; // chibi hands: an oversized glass reads at gameplay distance
            return root;
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
            // Gestures start once the camera has arrived (move time), so the moment is on screen.
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
                case 0: break;
                case 1: if (musician) StartCoroutine(musician.Gesture("Wave", 1.6f)); break;
                case 2:
                    if (officiant) StartCoroutine(officiant.Gesture("Talk", 2.4f));
                    Gesture(PlaceHero(stage.x - 1.4f, 1), "Bow", HeroFace.Expression.Happy);       // vows
                    break;
                case 3:
                    if (officiant) StartCoroutine(officiant.Gesture("Bow", 2f));
                    Gesture(PlaceHero(stage.x - 1.4f, 1), "Kneel", HeroFace.Expression.Surprised); // ring offered on one knee
                    if (petals) petals.Burst(stage + Vector3.up * 0.5f, 80);
                    break;
                case 4:
                    Gesture(PlaceHero(cake.x - 1.4f, 1), "Celebrate");                             // at the cake
                    if (petals) petals.Burst(cake, 60);
                    break;
                case 5:
                    if (musician) StartCoroutine(musician.Gesture("Bow", 1.8f));
                    // raise and drink with the LEFT hand: stand right of the table facing left so that hand is toward the camera
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
