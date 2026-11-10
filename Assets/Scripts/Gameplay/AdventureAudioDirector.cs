using System;
using System.Collections;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    public enum AdventureMusicStage
    {
        Splash,
        Authentication,
        CharacterSelection,
        Village,
        Exploration,
        Boss,
        VillageNight, // appended: keeps serialized indices of the stages above
        Wedding
    }

    [Serializable]
    public sealed class StageMusic
    {
        public AdventureMusicStage stage;
        [Tooltip("Original or correctly licensed track, loopable and mastered.")]
        public AudioClip music;
        public AudioClip ambience;
    }

    [Serializable]
    public sealed class HeroSoundSet
    {
        public HeroClass heroClass;
        [Tooltip("Weapon swing sound at start/windup of the animation.")]
        public AudioClip attackWhoosh;
        [Tooltip("Contact sound of the basic attack (melee connect / projectile arrival).")]
        public AudioClip attackImpact;
        [Tooltip("Sound at the start of the unique class skill animation.")]
        public AudioClip skillCast;
        [Tooltip("Sound aligned with actual hit frame using animation events.")]
        public AudioClip skillImpact;
    }

    /// <summary>
    /// Three independent audio channels: BGM, ambient, and SFX.
    /// Does NOT contain completed audio assets; assign licensed clips in Inspector.
    /// Volume preferences are local only, never a substitute for character saves.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class AdventureAudioDirector : MonoBehaviour
    {
        public static AdventureAudioDirector Instance { get; private set; }

        [Header("Persistent channels")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource effectsSource;
        [Header("Original music and ambience")]
        [SerializeField] private StageMusic[] soundtracks = new StageMusic[0];
        [Header("Four unique class sound sets")]
        [SerializeField] private HeroSoundSet[] classSounds = new HeroSoundSet[0];
        [Header("Mix")]
        [SerializeField, Min(0f)] private float transitionSeconds = 0.65f;
        [SerializeField, Range(0f, 1f)] private float sfxHeadroom = 0.75f;

        private const string MusicVolumeKey = "AK_Option_MusicVolume";
        private const string AmbienceVolumeKey = "AK_Option_AmbienceVolume";
        private const string EffectsVolumeKey = "AK_Option_EffectsVolume";
        private const string MutedKey = "AK_Option_Muted";
        private const string MusicOnKey = "AK_Option_MusicOn";
        private bool muted, musicOn = true;

        public bool Muted => muted;
        /// <summary>Background music on/off on its own (SFX keep playing).</summary>
        public bool MusicOn => musicOn;
        public AdventureMusicStage CurrentStage => currentStage;
        public AudioClip CurrentMusic => musicSource ? musicSource.clip : null;
        /// <summary>The music source (cinematics duck it while they play their own score).</summary>
        public AudioSource MusicSource => musicSource;
        public bool MusicAudible => musicSource && musicSource.isPlaying && !musicSource.mute && musicSource.volume > 0.01f;
        public float MusicSourceVolume => musicSource ? musicSource.volume : 0f;

        public void SetMusicOn(bool value)
        {
            musicOn = value;
            PlayerPrefs.SetInt(MusicOnKey, value ? 1 : 0);
            PlayerPrefs.Save();
            musicSource.mute = muted || !musicOn;
        }

        /// <summary>Steps 0 → 25 → 50 → 75 → 100 % → 0 (settings button).</summary>
        public void CycleMusicVolume() => SetMusicVolume(NextStep(musicVolume));
        public void CycleEffectsVolume() => SetEffectsVolume(NextStep(effectsVolume));
        private static float NextStep(float v) { float n = Mathf.Round(v * 4f) / 4f + 0.25f; return n > 1.001f ? 0f : n; }
        private float musicVolume;
        private float ambienceVolume;
        private float effectsVolume;
        private Coroutine fadeRoutine;
        private AdventureMusicStage currentStage;
        private bool hasStage;

        public float MusicVolume => musicVolume;
        public float AmbienceVolume => ambienceVolume;
        public float EffectsVolume => effectsVolume;

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (!musicSource) musicSource = gameObject.AddComponent<AudioSource>();
            if (!ambienceSource) ambienceSource = gameObject.AddComponent<AudioSource>();
            if (!effectsSource) effectsSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            ambienceSource.playOnAwake = false;
            effectsSource.playOnAwake = false;
            musicSource.loop = true;
            ambienceSource.loop = true;
            musicSource.spatialBlend = 0f;
            ambienceSource.spatialBlend = 0f;
            effectsSource.spatialBlend = 0f;
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 0.75f));
            ambienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(AmbienceVolumeKey, 0.65f));
            effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsVolumeKey, 0.85f));
            ApplyVolumes();
            musicOn = PlayerPrefs.GetInt(MusicOnKey, 1) == 1;
            SetAllMuted(PlayerPrefs.GetInt(MutedKey, 0) == 1);
        }

        public void SetMusicVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
            PlayerPrefs.Save();
            if (fadeRoutine == null) musicSource.volume = musicVolume;
        }

        public void SetAmbienceVolume(float value)
        {
            ambienceVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(AmbienceVolumeKey, ambienceVolume);
            PlayerPrefs.Save();
            ambienceSource.volume = ambienceVolume;
        }

        public void SetEffectsVolume(float value)
        {
            effectsVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(EffectsVolumeKey, effectsVolume);
            PlayerPrefs.Save();
            effectsSource.volume = effectsVolume;
        }

        /// <summary>Mute is a saved preference (local option only; never part of character saves).</summary>
        public void SetAllMuted(bool value)
        {
            muted = value;
            musicSource.mute = value || !musicOn;
            ambienceSource.mute = value;
            effectsSource.mute = value;
            PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplyVolumes()
        {
            musicSource.volume = musicVolume;
            ambienceSource.volume = ambienceVolume;
            effectsSource.volume = effectsVolume;
        }

        public bool PlayStage(AdventureMusicStage stage)
        {
            if (hasStage && currentStage == stage) return true;
            StageMusic match = null;
            foreach (StageMusic entry in soundtracks)
            {
                if (entry != null && entry.stage == stage)
                {
                    match = entry;
                    break;
                }
            }
            if (match == null || !match.music)
            {
                Debug.LogWarning("AdventureAudioDirector: missing authored music for " + stage);
                return false;
            }

            currentStage = stage;
            hasStage = true;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeToMusic(match.music));
            if (match.ambience != ambienceSource.clip)
            {
                ambienceSource.Stop();
                ambienceSource.clip = match.ambience;
                if (match.ambience) ambienceSource.Play();
            }
            ambienceSource.volume = ambienceVolume;
            return true;
        }

        private IEnumerator FadeToMusic(AudioClip next)
        {
            if (musicSource.clip == next && musicSource.isPlaying)
            {
                musicSource.volume = musicVolume;
                fadeRoutine = null;
                yield break;
            }
            float duration = Mathf.Max(0.02f, transitionSeconds);
            float startingVolume = musicSource.volume;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                musicSource.volume = Mathf.Lerp(startingVolume, 0f, elapsed / duration);
                yield return null;
            }

            musicSource.Stop();
            musicSource.clip = next;
            musicSource.Play();
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                musicSource.volume = Mathf.Lerp(0f, musicVolume, elapsed / duration);
                yield return null;
            }
            musicSource.volume = musicVolume;
            fadeRoutine = null;
        }

        public void PlayClassAttack(HeroClass heroClass)
        {
            HeroSoundSet sounds = FindClassSounds(heroClass);
            if (sounds != null) PlayEffect(sounds.attackWhoosh);
        }

        /// <summary>Call on actual contact (animation event / projectile arrival).</summary>
        public void PlayClassAttackImpact(HeroClass heroClass)
        {
            HeroSoundSet sounds = FindClassSounds(heroClass);
            if (sounds != null) PlayEffect(sounds.attackImpact);
        }

        public void PlayClassSkillCast(HeroClass heroClass)
        {
            HeroSoundSet sounds = FindClassSounds(heroClass);
            if (sounds != null) PlayEffect(sounds.skillCast);
        }

        /// <summary>Call from a real skill impact animation event; don't fake hits.</summary>
        public void PlayClassSkillImpact(HeroClass heroClass)
        {
            HeroSoundSet sounds = FindClassSounds(heroClass);
            if (sounds != null) PlayEffect(sounds.skillImpact);
        }

        public void PlayEffect(AudioClip clip, float gain = 1f)
        {
            if (clip && effectsSource)
                effectsSource.PlayOneShot(clip, Mathf.Clamp01(gain) * sfxHeadroom);
        }

        private HeroSoundSet FindClassSounds(HeroClass heroClass)
        {
            foreach (HeroSoundSet entry in classSounds)
                if (entry != null && entry.heroClass == heroClass) return entry;
            return null;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
