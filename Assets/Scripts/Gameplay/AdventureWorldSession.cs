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

        private AdventureCharacterSnapshot character;
        private Vector2 lastSaved;
        private float nextAutosave;
        private bool saving, leaving;

        public string MapId => mapId;
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
            lastSaved = start;
            nextAutosave = Time.time + autosaveSeconds;
            if (characterLabel)
                characterLabel.text = character.characterName + "  ·  Lv." + character.level + "  ·  " +
                                      HeroClassCatalog.Get(character.heroClass).displayName;
            if (AdventureAudioDirector.Instance) AdventureAudioDirector.Instance.PlayStage(AdventureMusicStage.Village);
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
        }

        private void Update()
        {
            if (character == null || leaving || saving || !player) return;
            if (Time.time >= nextAutosave)
            {
                nextAutosave = Time.time + autosaveSeconds;
                Vector2 pos = player.transform.position;
                if (player.IsGrounded && (pos - lastSaved).sqrMagnitude > 0.25f) SaveNow(null);
            }
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
