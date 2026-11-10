using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Sảnh Kết Hôn ceremony REHEARSAL (diễn tập): talking to the Chủ Hôn plays the cinematic of the rite —
    /// entrance down the aisle, the vows on the flower stage, the rings on their cushion, the cake, the toast at
    /// the banquet, a petal shower — with the NPCs bowing / talking / waving and petal bursts at each moment.
    /// The real two-player wedding (proposal, partner, rings as items, ring-exchange / cake-cutting / toast / kiss
    /// animations for both heroes) needs the marriage system on the server, which is not built yet; the captions say
    /// so. Nothing here changes game state.
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
                new CinematicDirector.Shot(stage + new Vector3(-2.2f, 2.2f, -6.5f), stage + up * 1.8f, 30f, 2.4f, 2.6f, "Chủ Hôn: “Hôm nay chúng ta cùng chứng giám lời thề của hai người.”"),
                new CinematicDirector.Shot(stage + new Vector3(0.7f, 2.3f, -4.8f), stage + new Vector3(0f, 1.15f, 0.2f), 30f, 1.6f, 2.4f, "Trao nhẫn"),
                new CinematicDirector.Shot(cake + new Vector3(-1.8f, 2.0f, -5f), cake + up * 1.2f, 30f, 2.2f, 2.0f, "Cắt bánh cưới"),
                new CinematicDirector.Shot(banquet + new Vector3(-2f, 2.4f, -6f), banquet + up * 1.1f, 32f, 1.8f, 2.0f, "Nâng ly chúc mừng"),
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
            if (officiant) officiant.FaceFront(); // faces the guests, not the player who talked to him
            if (musician) musician.FaceFront();
            dir.ShotStarted += OnShot;
            dir.Play(Shots(), () => { dir.ShotStarted -= OnShot; if (officiant) officiant.Play("Idle"); });
        }

        private void OnShot(int i)
        {
            switch (i)
            {
                case 1: if (musician) StartCoroutine(musician.Gesture("Wave", 1.6f)); break;
                case 2: if (officiant) StartCoroutine(officiant.Gesture("Talk", 2.4f)); break;
                case 3: if (officiant) StartCoroutine(officiant.Gesture("Bow", 2f)); if (petals) petals.Burst(stage + Vector3.up * 0.5f, 80); break;
                case 4: if (petals) petals.Burst(cake, 60); break;
                case 5: if (musician) StartCoroutine(musician.Gesture("Bow", 1.8f)); if (petals) petals.Burst(banquet, 60); break;
                case 6: if (petals) petals.Burst(new Vector3(4f, 0f, 2f), 140); break;
            }
        }
    }
}
