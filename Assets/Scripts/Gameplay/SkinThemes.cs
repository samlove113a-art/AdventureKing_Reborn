using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Skill look of every full-body skin: the colour all of the hero's skill effects (and the skill-bar icons) take while the skin is worn, plus the name shown in the Shop.
    /// Presentation only: damage, cooldown and MP stay decided by the server. The legendary (mythic) skin of each class also adds a signature burst on every impact.
    /// </summary>
    public static class SkinThemes
    {
        public readonly struct Theme { public readonly Color Tint; public readonly string Name; public Theme(float r, float g, float b, string n) { Tint = new Color(r, g, b, 1f); Name = n; } }

        private static readonly Dictionary<string, Theme> ByKey = new Dictionary<string, Theme>
        {
            ["crimson"] = new Theme(1f, 0.35f, 0.4f, "ánh đỏ thẫm"), ["verdant"] = new Theme(0.55f, 1f, 0.5f, "ánh xanh lục"), ["azure"] = new Theme(0.45f, 0.8f, 1f, "ánh thanh long"),
            ["jade"] = new Theme(0.4f, 1f, 0.75f, "ánh ngọc bích"), ["ronin"] = new Theme(0.75f, 0.7f, 1f, "ánh tím chàm"), ["aurum"] = new Theme(1f, 0.85f, 0.4f, "ánh vàng kim"),
            ["frost"] = new Theme(0.7f, 0.95f, 1f, "ánh băng giá"), ["ember"] = new Theme(1f, 0.55f, 0.2f, "ánh lửa cháy"), ["shadow"] = new Theme(0.7f, 0.45f, 1f, "ánh bóng tối"),
            ["sun"] = new Theme(1f, 0.8f, 0.3f, "ánh thái dương"), ["violet"] = new Theme(0.8f, 0.5f, 1f, "ánh tím mộng"), ["ocean"] = new Theme(0.3f, 0.7f, 1f, "ánh hải triều"),
            ["sakura"] = new Theme(1f, 0.65f, 0.85f, "cánh anh đào"), ["night"] = new Theme(0.5f, 0.55f, 1f, "ánh dạ nguyệt"), ["abyss"] = new Theme(0.6f, 0.3f, 0.9f, "ánh hư không"),
            ["tide"] = new Theme(0.3f, 0.9f, 0.9f, "ánh thủy triều"), ["viper"] = new Theme(0.6f, 1f, 0.3f, "ánh độc xà"), ["void"] = new Theme(0.6f, 0.35f, 0.8f, "ánh hư vô"),
            ["blood"] = new Theme(1f, 0.2f, 0.25f, "ánh huyết nguyệt"), ["ghost"] = new Theme(0.6f, 0.95f, 0.9f, "ánh u hồn"), ["star"] = new Theme(0.7f, 0.8f, 1f, "ánh tinh tú"),
            ["forest"] = new Theme(0.5f, 0.9f, 0.4f, "ánh rừng xanh"), ["autumn"] = new Theme(1f, 0.65f, 0.25f, "lá thu bay"), ["moon"] = new Theme(0.8f, 0.9f, 1f, "ánh nguyệt quang"),
            ["fae"] = new Theme(0.85f, 1f, 0.8f, "bụi tiên lấp lánh"),
        };

        private static readonly Dictionary<HeroClass, Theme> Legend = new Dictionary<HeroClass, Theme>
        {
            [HeroClass.Swordsman] = new Theme(1f, 0.7f, 0.2f, "Long Hỏa: rồng vàng bùng nổ mỗi nhát chém"),
            [HeroClass.Mage] = new Theme(0.4f, 1f, 1f, "Thời Không: sóng pha lê thời gian lan tỏa"),
            [HeroClass.Assassin] = new Theme(1f, 0.15f, 0.25f, "Huyết Nguyệt: vụ nổ trăng máu mỗi đòn"),
            [HeroClass.Archer] = new Theme(1f, 0.6f, 0.15f, "Hỏa Phượng: cánh lửa phượng hoàng khi trúng đích"),
        };

        /// <summary>Skin key of a set id ("set2d_mage_sakura" → "sakura"); null when it is not a skin.</summary>
        public static string KeyOf(string setId) => !string.IsNullOrEmpty(setId) && setId.StartsWith("set2d_") ? setId.Substring(setId.LastIndexOf('_') + 1) : null;

        public static bool TryGet(HeroClass cls, string key, out Theme theme)
        {
            theme = default;
            if (string.IsNullOrEmpty(key)) return false;
            return key == "legend" ? Legend.TryGetValue(cls, out theme) : ByKey.TryGetValue(key, out theme);
        }

        public static bool IsLegend(string key) => key == "legend";

        /// <summary>Colours an effect with the skin's theme (alpha kept).</summary>
        public static Color Apply(Color baseColor, Color tint, float amount = 0.7f) =>
            new Color(Mathf.Lerp(baseColor.r, tint.r, amount), Mathf.Lerp(baseColor.g, tint.g, amount), Mathf.Lerp(baseColor.b, tint.b, amount), baseColor.a);
    }
}
