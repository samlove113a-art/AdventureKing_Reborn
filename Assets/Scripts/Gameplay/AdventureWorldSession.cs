using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Runs a world map for the character the server authorised in the hero hall:
    ///  - spawns the selected class's real 3D hero on the 2D-physics player;
    ///  - restores the server-saved position (or the map spawn the first time);
    ///  - autosaves position to the server while playing and on leaving;
    ///  - "Thoát" saves, signs out and returns to the login screen.
    /// Without an authorised session it never invents a character: it returns to the entry scene.
    /// </summary>
    public sealed class AdventureWorldSession : MonoBehaviour
    {
        [SerializeField] private string mapId = "village_wind_bell";
        [SerializeField] private string entrySceneName = "AK_Entry";
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private PlayerMotor2D player;
        [SerializeField] private CharacterVisual3D playerVisual;
        [SerializeField] private Transform visualMount;
        [Tooltip("Visual prefab per HeroClass, in enum order: Swordsman, Mage, Assassin, Archer.")]
        [SerializeField] private GameObject[] heroPrefabs = new GameObject[4];
        [SerializeField] private float heroScale = 1.55f;
        [SerializeField] private Text characterLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField, Min(3f)] private float autosaveSeconds = 15f;
        [Header("World safety + travel")]
        [Tooltip("Below this height the player has fallen out of the map and is returned to the last safe ground.")]
        [SerializeField] private float killY = -12f;
        [SerializeField] private CanvasGroup screenFade;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private AudioClip warpClip, arriveClip;
        [SerializeField] private AdventureMusicStage musicStage = AdventureMusicStage.Village;
        [Header("Portal UI")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private Text confirmText;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Text loadingTitle;
        [SerializeField] private Image loadingBar;
        [SerializeField] private Text loadingTip;
        private PortalGate2D pendingPortal;
        private static readonly string[] Tips =
        {
            "Mẹo: đứng trong vòng rune và nhấn ▲ / W để dùng cổng dịch chuyển.",
            "Mẹo: nhấn ▼ / S trên thang để leo xuống xuyên qua sàn gỗ.",
            "Mẹo: tiến trình được lưu trên máy chủ mỗi khi bạn dịch chuyển hoặc thoát.",
            "Mẹo: Thị trấn Chuông Gió là nơi an toàn để gặp gỡ bạn bè.",
        };

        public bool ConfirmOpen => confirmPanel && confirmPanel.activeSelf;
        public bool LoadingVisible => loadingPanel && loadingPanel.activeSelf;

        /// <summary>Server map id -> Unity scene. The server decides the map; the client only loads its scene.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, string> SceneForMap = new()
        {
            ["village_wind_bell"] = "AK_Village",
            ["meadow_breeze"] = "AK_Meadow",
            [TownLayout.GuildHall.MapId] = TownLayout.GuildHall.Scene,
            [TownLayout.WeddingHall.MapId] = TownLayout.WeddingHall.Scene,
        };

        public static AdventureWorldSession Current { get; private set; }

        private AdventureCharacterSnapshot character;
        private Vector2 lastSaved, lastSafeGround;
        private float nextAutosave;
        private bool saving, leaving;

        public string MapId => mapId;
        public bool Traveling { get; private set; }
        public int FallRecoveries { get; private set; }
        public PlayerMotor2D Player => player;

        private void Awake() => Current = this;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }
        public AdventureCharacterSnapshot Character => character;
        public GameObject HeroModel { get; private set; }
        public bool Saving => saving;
        public bool Leaving => leaving;
        public string Status => statusLabel ? statusLabel.text : "";

        private void Start()
        {
            character = AdventurePlayerSession.Character;
            AdventureServerConnection server = AdventureServerConnection.Instance;
            if (character == null || !server || !server.HasSession)
            {
                SetStatus("Chưa đăng nhập. Đang quay lại màn hình đăng nhập...");
                if (player) player.gameObject.SetActive(false);
                StartCoroutine(ReturnToEntryAfter(1.2f));
                return;
            }

            // The server's saved map wins: if this scene is not the character's map, load the right one.
            if (!string.IsNullOrEmpty(character.lastMapId) && character.lastMapId != mapId &&
                SceneForMap.TryGetValue(character.lastMapId, out string correctScene))
            {
                if (player) player.gameObject.SetActive(false);
                SceneManager.LoadScene(correctScene);
                return;
            }

            SpawnHero();
            Vector2 start = spawnPoint ? (Vector2)spawnPoint.position : Vector2.zero;
            if (character.hasPosition && character.lastMapId == mapId)
                start = new Vector2(character.posX, character.posY);
            if (player)
            {
                player.transform.position = start;
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body) { body.position = start; body.linearVelocity = Vector2.zero; }
            }
            lastSaved = lastSafeGround = start;
            nextAutosave = Time.time + autosaveSeconds;
            if (screenFade) StartCoroutine(Fade(1f, 0f, 0.45f));
            if (AdventurePlayerSession.ArrivedByPortal)
            {
                AdventurePlayerSession.ArrivedByPortal = false;
                if (sfx && arriveClip) sfx.PlayOneShot(arriveClip);
            }
            if (characterLabel)
                characterLabel.text = character.characterName + "  ·  Lv." + character.level + "  ·  " +
                                      HeroClassCatalog.Get(character.heroClass).displayName;
            requestedMusic = MusicNow;
            if (AdventureAudioDirector.Instance) AdventureAudioDirector.Instance.PlayStage(MusicNow);
            SetStatus("");
        }

        private void SpawnHero()
        {
            int index = (int)character.heroClass;
            GameObject prefab = heroPrefabs != null && index < heroPrefabs.Length ? heroPrefabs[index] : null;
            if (!prefab || !visualMount)
            {
                SetStatus("Thiếu model 3D của nghề này.");
                return;
            }
            HeroModel = Instantiate(prefab, visualMount);
            HeroModel.transform.localPosition = Vector3.zero;
            HeroModel.transform.localScale = Vector3.one * heroScale;
            if (playerVisual)
                playerVisual.Bind(HeroModel.transform, HeroModel.GetComponentInChildren<Animator>(true));
            // Wear the outfit the server has on record (restored after relog / scene change / server restart).
            Outfit = DressHero(HeroModel, character.heroClass);
            Outfit.Apply(character.outfit);
            // class-based pools for the HUD (HP from the class table; MP is a balance proposal until skills land)
            if (player)
            {
                Health2D hp = player.GetComponent<Health2D>();
                if (hp) hp.SetMax(HeroClassCatalog.Get(character.heroClass).startingHealth);
                PlayerMana mp = player.GetComponent<PlayerMana>();
                if (!mp) mp = player.gameObject.AddComponent<PlayerMana>();
                mp.Configure(PlayerMana.MaxFor(character.heroClass), PlayerMana.RegenFor(character.heroClass));
            }
        }

        public HeroOutfit Outfit { get; private set; }
        public GameObject[] HeroPrefabs => heroPrefabs;
        public float HeroScale => heroScale;
        public CharacterVisual3D PlayerVisual => playerVisual;

        /// <summary>Adds (or finds) the outfit component on a hero visual and binds it to the body mesh.</summary>
        public static HeroOutfit DressHero(GameObject hero, HeroClass heroClass)
        {
            HeroOutfit o = hero.GetComponent<HeroOutfit>();
            if (!o) o = hero.AddComponent<HeroOutfit>();
            foreach (SkinnedMeshRenderer s in hero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (!s.name.StartsWith("Weapon_") && !s.name.StartsWith("Outfit_")) { o.Bind(heroClass, s); break; }
            return o;
        }

        /// <summary>Called only with a server-confirmed outfit (purchase / equip response).</summary>
        public void ApplyServerOutfit(OutfitLoadout outfit, long gold, long diamonds, long revision)
        {
            if (character == null) return;
            character.outfit = outfit ?? new OutfitLoadout();
            character.gold = gold;
            character.diamonds = diamonds;
            character.serverRevision = revision;
            if (Outfit) Outfit.Apply(character.outfit);
        }

        private void FixedUpdate()
        {
            if (character == null || !player || Traveling) return;
            // Read the physics body, not the (interpolated) transform, which lags a teleport by a step or more.
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Vector2 pos = body ? body.position : (Vector2)player.transform.position;
            if (player.IsGrounded && !player.IsClimbing && pos.y > killY + 1f) lastSafeGround = pos;
            if (pos.y < killY)
            {
                // Fell out of the playable area: return to the last ground the player stood on.
                FallRecoveries++;
                Vector2 back = lastSafeGround + Vector2.up * 0.4f;
                if (body)
                {
                    body.linearVelocity = Vector2.zero;
                    body.position = back;
                }
                player.transform.position = back;
                Physics2D.SyncTransforms();
            }
        }

        /// <summary>The village plays its day theme by day and the calm night theme after dark (VN time / display mode).</summary>
        private AdventureMusicStage? requestedMusic;
        public AdventureMusicStage MusicNow => musicStage == AdventureMusicStage.Village && DayNightCycle.TargetNight > 0.5f
            ? AdventureMusicStage.VillageNight : musicStage;

        private void Update()
        {
            if (character != null && AdventureAudioDirector.Instance && requestedMusic != MusicNow)
            {
                requestedMusic = MusicNow;
                AdventureAudioDirector.Instance.PlayStage(requestedMusic.Value);
            }
#if ENABLE_INPUT_SYSTEM
            if (ConfirmOpen && UnityEngine.InputSystem.Keyboard.current is { } k)
            {
                if (k.enterKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame) ConfirmPortal();
                else if (k.escapeKey.wasPressedThisFrame) CancelPortal();
            }
#endif
            if (character == null || leaving || saving || Traveling || !player) return;
            if (Time.time >= nextAutosave)
            {
                nextAutosave = Time.time + autosaveSeconds;
                Vector2 pos = player.transform.position;
                if (player.IsGrounded && (pos - lastSaved).sqrMagnitude > 0.25f) SaveNow(null);
            }
        }

        /// <summary>
        /// Portal travel: the server validates the portal (current map + reach) and moves the character;
        /// only then does the client fade out and load the destination scene.
        /// </summary>
        /// <summary>Opens the "travel to …?" dialog; the player can confirm or stay.</summary>
        public void AskPortalConfirmation(PortalGate2D portal)
        {
            if (Traveling || leaving || ConfirmOpen || !portal) return;
            if (!confirmPanel) { UsePortal(portal); return; }
            pendingPortal = portal;
            SetInput(false);
            if (confirmText) confirmText.text = "Rời " + DisplayNameForMap(mapId) + " để đến\n<b>" + portal.DestinationName + "</b>?";
            confirmPanel.SetActive(true);
        }

        public void ConfirmPortal()
        {
            PortalGate2D p = pendingPortal;
            pendingPortal = null;
            if (confirmPanel) confirmPanel.SetActive(false);
            if (p) UsePortal(p);
            else SetInput(true);
        }

        public void CancelPortal()
        {
            pendingPortal = null;
            if (confirmPanel) confirmPanel.SetActive(false);
            SetInput(true);
        }

        private static string DisplayNameForMap(string id) => TownLayout.For(id).DisplayName;

        private void SetInput(bool on)
        {
            if (!player) return;
            PlayerInput2D input = player.GetComponent<PlayerInput2D>();
            if (input) input.enabled = on;
        }

        /// <summary>
        /// Portal travel: the server validates the portal (current map, reach, cooldown) and moves the character;
        /// only then does the client play the warp, show the loading screen and load the destination scene.
        /// A lost connection or a refusal leaves the player where they were, with the reason shown.
        /// </summary>
        public void UsePortal(PortalGate2D portal)
        {
            AdventureServerConnection server = AdventureServerConnection.Instance;
            if (Traveling || leaving || character == null || !server || !player || !portal) return;
            Traveling = true; // client-side guard against double activation; the server enforces a cooldown too
            SetInput(false);
            SetStatus("Đang mở cổng tới " + portal.DestinationName + "...");
            server.Travel(character.characterId, portal.PortalId, player.transform.position, (ok, updated, message) =>
            {
                if (!ok || updated == null || !SceneForMap.TryGetValue(updated.lastMapId, out string scene))
                {
                    Traveling = false;
                    SetInput(true);
                    SetStatus(string.IsNullOrEmpty(message) ? "Không thể dịch chuyển. Hãy thử lại." : message);
                    return;
                }
                character.lastMapId = updated.lastMapId;
                character.posX = updated.posX;
                character.posY = updated.posY;
                character.hasPosition = true;
                character.serverRevision = updated.serverRevision;
                AdventurePlayerSession.ArrivedByPortal = true;
                SetStatus("");
                if (sfx && warpClip) sfx.PlayOneShot(warpClip);
                portal.PlayActivation();
                StartCoroutine(TravelAndLoad(scene, portal.DestinationName));
            });
        }

        /// <summary>Uses a teleport ticket from the bag: the server removes the ticket and moves the hero in one step;
        /// only then the warp + loading screen run. A refusal (wrong map, level, already there) changes nothing.</summary>
        public void UseTicket(int slot, System.Action<string> failed = null)
        {
            AdventureServerConnection server = AdventureServerConnection.Instance;
            if (Traveling || leaving || character == null || !server) return;
            Traveling = true;
            SetInput(false);
            server.UseTicket(character.characterId, slot, (ok, r, message) =>
            {
                if (!ok || r == null || r.character == null || !SceneForMap.TryGetValue(r.character.lastMapId, out string scene))
                {
                    Traveling = false;
                    SetInput(true);
                    failed?.Invoke(string.IsNullOrEmpty(message) ? "Không thể dịch chuyển." : message);
                    return;
                }
                character.lastMapId = r.character.lastMapId;
                character.posX = r.character.posX;
                character.posY = r.character.posY;
                character.hasPosition = true;
                character.serverRevision = r.character.serverRevision;
                AdventurePlayerSession.ArrivedByPortal = true;
                if (sfx && warpClip) sfx.PlayOneShot(warpClip);
                StartCoroutine(TravelAndLoad(scene, MapDisplayName(r.character.lastMapId)));
            });
        }

        private static string MapDisplayName(string mapId) => TownLayout.For(mapId).DisplayName;

        private IEnumerator TravelAndLoad(string scene, string destination)
        {
            // Warp: the hero shrinks into the vortex while the screen fades.
            Transform hero = HeroModel ? HeroModel.transform : null;
            Vector3 heroScale = hero ? hero.localScale : Vector3.one;
            yield return Fade(0f, 1f, 0.6f, t => { if (hero) hero.localScale = heroScale * (1f - 0.8f * t); });
            if (loadingPanel)
            {
                if (loadingTitle) loadingTitle.text = destination;
                if (loadingTip) loadingTip.text = Tips[Random.Range(0, Tips.Length)];
                if (loadingBar) loadingBar.fillAmount = 0f;
                loadingPanel.SetActive(true);
            }
            AsyncOperation op = SceneManager.LoadSceneAsync(scene);
            op.allowSceneActivation = false;
            float shown = 0f, minEnd = Time.unscaledTime + 0.8f; // never flash the loading screen for one frame
            while (op.progress < 0.9f || Time.unscaledTime < minEnd || shown < 0.99f)
            {
                float target = Mathf.Clamp01(op.progress / 0.9f);
                shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 1.6f);
                if (loadingBar) loadingBar.fillAmount = shown;
                yield return null;
            }
            op.allowSceneActivation = true;
        }

        private IEnumerator Fade(float from, float to, float seconds, System.Action<float> each)
        {
            if (!screenFade) yield break;
            screenFade.blocksRaycasts = to > 0.5f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                screenFade.alpha = Mathf.Lerp(from, to, k);
                each?.Invoke(k);
                yield return null;
            }
            screenFade.alpha = to;
            each?.Invoke(1f);
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            if (!screenFade) yield break;
            screenFade.blocksRaycasts = to > 0.5f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                screenFade.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            screenFade.alpha = to;
        }

        /// <summary>Saves the current position to the server (grounded positions are preferred).</summary>
        public void SaveNow(System.Action<bool> done)
        {
            AdventureServerConnection server = AdventureServerConnection.Instance;
            if (character == null || !server || !player) { done?.Invoke(false); return; }
            saving = true;
            Vector2 pos = player.transform.position;
            server.SaveLocation(character.characterId, mapId, pos, (ok, updated, message) =>
            {
                saving = false;
                if (ok && updated != null)
                {
                    lastSaved = pos;
                    character.lastMapId = updated.lastMapId;
                    character.posX = updated.posX;
                    character.posY = updated.posY;
                    character.hasPosition = updated.hasPosition;
                    character.serverRevision = updated.serverRevision;
                }
                else SetStatus(string.IsNullOrEmpty(message) ? "Không lưu được vị trí." : message);
                done?.Invoke(ok);
            });
        }

        /// <summary>"Thoát": save to the server, then sign out and return to the login screen.</summary>
        public void ExitToLogin()
        {
            if (leaving || character == null) return;
            leaving = true;
            if (player)
            {
                PlayerInput2D input = player.GetComponent<PlayerInput2D>();
                if (input) input.enabled = false;
            }
            SetStatus("Đang lưu tiến trình...");
            SaveNow(ok =>
            {
                if (!ok)
                {
                    // Never leave silently with unsaved progress: stay, show the error, allow retry.
                    leaving = false;
                    if (player) { PlayerInput2D input = player.GetComponent<PlayerInput2D>(); if (input) input.enabled = true; }
                    SetStatus("Lưu thất bại — kiểm tra kết nối máy chủ rồi thử lại.");
                    return;
                }
                AdventureServerConnection.Instance.SignOut();
                AdventurePlayerSession.Clear();
                AdventureEntryFlow.ReturningFromWorld = true;
                SceneManager.LoadScene(entrySceneName);
            });
        }

        private void OnApplicationQuit()
        {
            // Best effort only: the process may exit before the request completes. Autosave covers the rest.
            if (character != null && !leaving && !saving) SaveNow(null);
        }

        private IEnumerator ReturnToEntryAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            SceneManager.LoadScene(entrySceneName);
        }

        private void SetStatus(string message)
        {
            if (statusLabel) statusLabel.text = message;
        }
    }
}
