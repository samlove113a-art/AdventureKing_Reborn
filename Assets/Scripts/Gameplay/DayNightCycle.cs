using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdventureKing.Gameplay
{
    public enum DayNightMode { Auto, Day, Night }

    /// <summary>
    /// Simple, smooth day/night for the world maps, driven by Vietnam time (UTC+7, from the device clock; purely
    /// cosmetic, nothing gameplay-relevant depends on it). Night ramps in at dusk (17:30-18:45) and out at dawn
    /// (05:00-06:15). Drives: sun (to moonlight), SH ambient, fog + sky colour, painted cutouts (global _AKNight in
    /// AK_Cutout), the star/moon sky layer and the lanterns (WorldLighting.Night). The player can force Ngày/Đêm in
    /// Settings (a local display preference, not game data).
    /// </summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Renderer nightSky;
        [SerializeField] private float fadeSeconds = 4f;

        private const string PrefKey = "ak_daynight_mode";
        private static readonly int NightId = Shader.PropertyToID("_AKNight");
        private static readonly int NightTintId = Shader.PropertyToID("_AKNightTint");
        private static readonly int TintId = Shader.PropertyToID("_TintColor");
        private static readonly Color MoonLight = new Color(0.58f, 0.68f, 1f);
        private static readonly Color NightSkyColor = new Color(0.07f, 0.1f, 0.22f);
        private static readonly Color PaintingNight = new Color(0.34f, 0.4f, 0.66f);

        private Color daySunColor, dayFog, dayBg;
        private float daySunIntensity;
        private SphericalHarmonicsL2 dayProbe, nightProbe;
        private MaterialPropertyBlock block;

        public static DayNightCycle Current { get; private set; }
        /// <summary>0 = full day, 1 = full night (smoothed).</summary>
        public float Night { get; private set; }

        public static DayNightMode Mode
        {
            get { try { return (DayNightMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2); } catch { return DayNightMode.Auto; } }
            set { try { PlayerPrefs.SetInt(PrefKey, (int)value); PlayerPrefs.Save(); } catch { } }
        }

        /// <summary>Test / QA override of the clock (hours, Vietnam time). Null = real clock.</summary>
        public static float? HourOverride { get; set; }

        public static float VietnamHour => HourOverride ?? (float)DateTime.UtcNow.AddHours(7).TimeOfDay.TotalHours;

        /// <summary>Night amount for an hour of the day (Vietnam time).</summary>
        public static float NightAt(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            if (hour >= 18.75f || hour < 5f) return 1f;
            if (hour >= 17.5f) return Mathf.SmoothStep(0f, 1f, (hour - 17.5f) / 1.25f);
            if (hour < 6.25f) return Mathf.SmoothStep(1f, 0f, (hour - 5f) / 1.25f);
            return 0f;
        }

        /// <summary>Test / QA override of the player's choice (null = the saved preference).</summary>
        public static DayNightMode? ModeOverride { get; set; }

        public static float TargetNight
        {
            get
            {
                DayNightMode m = ModeOverride ?? Mode;
                return m == DayNightMode.Day ? 0f : m == DayNightMode.Night ? 1f : NightAt(VietnamHour);
            }
        }

        /// <summary>Jump straight to the current target (map load, tests).</summary>
        public void SnapToTarget()
        {
            Night = TargetNight;
            Apply(Night);
        }

        public static string ModeLabel => Mode switch
        {
            DayNightMode.Day => "Thời gian: Luôn ngày",
            DayNightMode.Night => "Thời gian: Luôn đêm",
            _ => "Thời gian: Theo giờ VN",
        };

        public void Bind(Light sunLight, Camera cam, Renderer sky)
        {
            sun = sunLight; worldCamera = cam; nightSky = sky;
        }

        private void Awake()
        {
            Current = this;
            block = new MaterialPropertyBlock();
            if (sun) { daySunColor = sun.color; daySunIntensity = sun.intensity; }
            dayFog = RenderSettings.fogColor;
            dayBg = worldCamera ? worldCamera.backgroundColor : dayFog;
            dayProbe = RenderSettings.ambientProbe;
            nightProbe = dayProbe * 0.3f;
            nightProbe.AddAmbientLight(new Color(0.06f, 0.08f, 0.18f));
            Night = TargetNight; // no visible fade when a map loads
            Apply(Night);
        }

        private void Update()
        {
            float target = TargetNight;
            if (!Mathf.Approximately(Night, target))
            {
                Night = Mathf.MoveTowards(Night, target, Time.unscaledDeltaTime / Mathf.Max(0.1f, fadeSeconds));
                Apply(Night);
            }
            else if (nightSky && Night > 0.01f) Apply(Night); // twinkle
        }

        private void Apply(float n)
        {
            if (sun)
            {
                sun.color = Color.Lerp(daySunColor, MoonLight, n);
                sun.intensity = Mathf.Lerp(daySunIntensity, 0.42f, n);
            }
            Color sky = Color.Lerp(dayFog, NightSkyColor, n);
            RenderSettings.fogColor = sky;
            if (worldCamera) worldCamera.backgroundColor = Color.Lerp(dayBg, NightSkyColor, n);
            RenderSettings.ambientProbe = dayProbe * (1f - n) + nightProbe * n;
            Shader.SetGlobalFloat(NightId, n);
            Shader.SetGlobalColor(NightTintId, PaintingNight);
            WorldLighting.Night = n;
            if (nightSky)
            {
                nightSky.enabled = n > 0.01f;
                float twinkle = 0.92f + 0.08f * Mathf.Sin(Time.unscaledTime * 1.7f);
                nightSky.GetPropertyBlock(block);
                block.SetColor(TintId, Color.white * (n * twinkle));
                nightSky.SetPropertyBlock(block);
            }
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            // leave other scenes (login, hall) untouched
            Shader.SetGlobalFloat(NightId, 0f);
            WorldLighting.Night = 0f;
        }

        /// <summary>Settings button: Theo giờ VN -> Luôn ngày -> Luôn đêm -> ...</summary>
        public static void CycleMode() => Mode = (DayNightMode)(((int)Mode + 1) % 3);
    }
}
