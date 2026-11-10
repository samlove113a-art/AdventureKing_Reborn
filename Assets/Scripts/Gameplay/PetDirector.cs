using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// The summoned pet in the world. Shows the server's pet state (summoned / satiety), spawns the pet model next to
    /// the hero and animates it procedurally: follows with little hops (squash and stretch), breathes and blinks when
    /// idle, waits at the foot of ladders, wags its tail / flaps its wings, VIP aura orbits. On a farm map, while it is
    /// fed, it runs to the owner's coins within reach and the SERVER pays the same drop once (pet never makes Gold),
    /// then does a happy spin. Hungry: droops, sad eyes, a "Đói" bubble, collects nothing.
    /// </summary>
    public sealed class PetDirector : MonoBehaviour
    {
        [Serializable] public struct PetPrefab { public string id; public GameObject prefab; public Sprite portrait; }
        [SerializeField] private PetPrefab[] pets = new PetPrefab[0];
        [SerializeField] private Font font;

        public static PetDirector Current { get; private set; }
        public PetsState State { get; private set; }
        public event Action Changed;
        public Transform Pet { get; private set; }
        public string PetId { get; private set; } = "";
        public bool Hungry => SummonedState != null && SummonedState.hungry;
        public OwnedPetState SummonedState => State?.owned?.FirstOrDefault(o => o.summoned);
        public int Collected { get; private set; }
        public string Mood { get; private set; } = "idle"; // idle | follow | fetch | happy | hungry
        public Sprite Portrait(string id) => pets.FirstOrDefault(p => p.id == id).portrait;

        private Transform body, head, eyeL, eyeR, aura;
        private Transform[] tails = new Transform[0], wingsL = new Transform[0], wingsR = new Transform[0];
        private TextMesh bubble;
        private float x, y, groundY, hop, nextPoll, nextBlink, happyUntil, fetchCooldown;
        private int facing = 1;
        private GoldDropView target;
        private bool busy;

        private void Awake() => Current = this;
        private void OnDestroy() { if (Current == this) Current = null; }

        private AdventureWorldSession World => AdventureWorldSession.Current;

        public void Refresh(Action done = null)
        {
            var w = World;
            if (busy || !w || w.Character == null || !AdventureServerConnection.Instance) return;
            busy = true;
            AdventureServerConnection.Instance.GetPets(w.Character.characterId, (ok, s, err) => { busy = false; if (ok) Apply(s); done?.Invoke(); });
        }

        public void Apply(PetsState s)
        {
            if (s == null) return;
            State = s;
            var w = World;
            if (w && w.Character != null) { w.Character.gold = s.gold; w.Character.diamonds = s.diamonds; }
            string want = s.summoned ?? "";
            if (want != PetId) Spawn(want);
            Changed?.Invoke();
        }

        private void Spawn(string id)
        {
            if (Pet) Destroy(Pet.gameObject);
            Pet = null; PetId = id ?? "";
            if (string.IsNullOrEmpty(id)) return;
            GameObject prefab = pets.FirstOrDefault(p => p.id == id).prefab;
            var w = World;
            if (!prefab || !w || !w.Player) return;
            Pet = Instantiate(prefab).transform;
            Pet.name = "Pet_" + id;
            Pet.localScale = Vector3.one * 1.1f;
            body = Pet.Find("Body"); head = body ? body.Find("Head") : null;
            eyeL = head ? head.Find("EyeL") : null; eyeR = head ? head.Find("EyeR") : null;
            aura = Pet.Find("Aura");
            tails = Pet.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Tail") && t.parent == body).ToArray();
            wingsL = Pet.GetComponentsInChildren<Transform>().Where(t => t.name == "WingL").ToArray();
            wingsR = Pet.GetComponentsInChildren<Transform>().Where(t => t.name == "WingR").ToArray();
            Vector3 hero = w.Player.transform.position;
            x = hero.x - 1.2f; groundY = y = hero.y - 0.8f;
            var b = new GameObject("Bubble");
            b.transform.SetParent(Pet, false);
            b.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            bubble = b.AddComponent<TextMesh>();
            bubble.font = font; bubble.text = ""; bubble.fontSize = 48; bubble.characterSize = 0.035f;
            bubble.anchor = TextAnchor.MiddleCenter; bubble.color = new Color(0.55f, 0.75f, 1f);
            if (font) b.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private void Update()
        {
            var w = World;
            if (!w || w.Character == null || !w.Player) return;
            if (Time.unscaledTime >= nextPoll) { nextPoll = Time.unscaledTime + 8f; Refresh(); }
            if (!Pet) return;
            float dt = Time.deltaTime, t = Time.time;
            var motor = w.Player.GetComponent<PlayerMotor2D>();
            Vector3 hero = w.Player.transform.position;
            if (motor && motor.IsGrounded && !motor.IsClimbing) groundY = hero.y - 0.8f; // waits at the foot of ladders
            int heroFacing = motor ? motor.Facing : 1;

            // ---- choose what to do
            FarmField farm = FarmField.Current;
            bool hungry = Hungry;
            if (!hungry && farm && Time.time >= fetchCooldown && (!target || target.Pending))
            {
                float reach = State != null && State.pickupReach > 0 ? State.pickupReach - 0.6f : 7f;
                target = farm.Drops.Values.Where(d => d && d.Landed && !d.Pending && !d.Expired && Mathf.Abs(d.Ground.x - hero.x) <= reach && Mathf.Abs(d.Ground.y - groundY) < 3f)
                    .OrderBy(d => Mathf.Abs(d.Ground.x - x)).FirstOrDefault();
            }
            if (hungry) target = null;
            float goalX = target ? target.Ground.x + 0.6f : hero.x - heroFacing * 1.15f;
            float goalY = target ? target.Ground.y : groundY;
            float speed = target ? 9f : Mathf.Abs(goalX - x) > 4f ? 10f : 5.5f;
            float before = x;
            x = Mathf.MoveTowards(x, goalX, speed * dt * (hungry ? 0.6f : 1f));
            y = Mathf.Lerp(y, goalY, 1f - Mathf.Exp(-8f * dt));
            bool moving = Mathf.Abs(x - before) > 0.002f;
            if (moving) facing = x > before ? 1 : -1;
            if (target && Mathf.Abs(x - goalX) < 0.25f && !target.Pending)
            {
                GoldDropView d = target;
                fetchCooldown = Time.time + 0.4f;
                farm.PetCollect(d, Pet, (ok, err) =>
                {
                    if (ok) { Collected++; happyUntil = Time.time + 1.1f; }
                    else fetchCooldown = Time.time + 2f; // e.g. went hungry on the server: try again later
                    target = null;
                });
            }
            Mood = hungry ? "hungry" : Time.time < happyUntil ? "happy" : target ? "fetch" : moving ? "follow" : "idle";

            // ---- procedural animation
            hop = moving ? hop + dt * (target ? 14f : 10f) : Mathf.MoveTowards(hop, Mathf.Round(hop / Mathf.PI) * Mathf.PI, dt * 6f);
            float hopH = moving ? Mathf.Abs(Mathf.Sin(hop)) * 0.18f : 0f;
            float happyJump = Mood == "happy" ? Mathf.Abs(Mathf.Sin((happyUntil - Time.time) * 9f)) * 0.35f : 0f;
            Pet.position = new Vector3(x, y + hopH + happyJump, -0.25f);
            float yaw = Mood == "happy" ? (happyUntil - Time.time) * 720f : facing > 0 ? 115f : 245f;
            if (!moving && Mood == "idle") yaw = Mathf.LerpAngle(facing > 0 ? 115f : 245f, 180f, 0.5f);
            Pet.rotation = Quaternion.Slerp(Pet.rotation, Quaternion.Euler(0f, yaw + 180f, 0f), 1f - Mathf.Exp(-10f * dt));
            if (body)
            {
                float squash = moving ? 1f + Mathf.Cos(hop * 2f) * 0.08f : 1f + Mathf.Sin(t * 2.2f) * 0.025f; // breathing
                float droop = hungry ? 0.86f : 1f;
                body.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash * droop, 1f / Mathf.Sqrt(squash));
            }
            if (head) head.localRotation = Quaternion.Euler(hungry ? 22f : Mathf.Sin(t * 1.3f) * 4f, 0f, hungry ? 8f : Mathf.Sin(t * 0.9f) * 6f);
            foreach (Transform tl in tails) tl.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * (moving ? 12f : 4f) + tl.GetSiblingIndex()) * (hungry ? 5f : 22f), tl.localEulerAngles.z);
            float flap = Mathf.Sin(t * (moving ? 22f : 6f)) * (hungry ? 5f : 30f);
            foreach (Transform wg in wingsL) wg.localRotation = Quaternion.Euler(0f, 0f, flap);
            foreach (Transform wg in wingsR) wg.localRotation = Quaternion.Euler(0f, 0f, -flap);
            if (aura) aura.localRotation = Quaternion.Euler(0f, t * 120f, 0f);
            // eyes: blink, happy squint, sad droop
            if (Time.time >= nextBlink) nextBlink = Time.time + UnityEngine.Random.Range(2.2f, 4.5f);
            float blink = Time.time > nextBlink - 0.12f ? 0.15f : 1f;
            float eyeY = Mood == "happy" ? 0.35f : hungry ? 0.6f : blink;
            if (eyeL) eyeL.localScale = new Vector3(eyeL.localScale.x, Mathf.Abs(eyeL.localScale.x) * 1.25f * eyeY, eyeL.localScale.z);
            if (eyeR) eyeR.localScale = new Vector3(eyeR.localScale.x, Mathf.Abs(eyeR.localScale.x) * 1.25f * eyeY, eyeR.localScale.z);
            if (bubble)
            {
                bubble.text = hungry ? "Đói..." : Mood == "happy" ? "♥" : "";
                bubble.color = hungry ? new Color(0.6f, 0.78f, 1f) : new Color(1f, 0.45f, 0.6f);
                bubble.transform.rotation = Quaternion.identity;
            }
        }

        // ------------------------------------------------------------------ actions (server)
        private string CharId => World && World.Character != null ? World.Character.characterId : null;

        public void Buy(string petId, Action<bool, string> done) =>
            AdventureServerConnection.Instance.BuyPet(CharId, petId, Guid.NewGuid().ToString("N"), (ok, s, err) => { if (ok) Apply(s); done?.Invoke(ok, err); });
        public void Summon(string petId, Action<bool, string> done) =>
            AdventureServerConnection.Instance.SummonPet(CharId, petId, (ok, s, err) => { if (ok) Apply(s); done?.Invoke(ok, err); });
        public void Dismiss(Action<bool, string> done) =>
            AdventureServerConnection.Instance.DismissPet(CharId, (ok, s, err) => { if (ok) Apply(s); done?.Invoke(ok, err); });

        /// <summary>Feeds the given pet with the first pet food in the bag (basic before feast).</summary>
        public void Feed(string petId, InventoryPanel bag, Action<bool, string> done)
        {
            BagSlotState food = bag && bag.State != null
                ? bag.State.slots.Where(s => s.kind == "petfood").OrderBy(s => s.itemId == "petfood_basic" ? 0 : 1).FirstOrDefault() : null;
            if (food == null) { done?.Invoke(false, "Không có thức ăn thú cưng trong túi. Mua ở quầy của Dược Sư Mộc Linh."); return; }
            AdventureServerConnection.Instance.FeedPet(CharId, petId, food.index, (ok, s, err) =>
            {
                if (ok) { Apply(s); happyUntil = Time.time + 1f; if (bag) bag.Refresh(); }
                done?.Invoke(ok, err);
            });
        }
    }
}
