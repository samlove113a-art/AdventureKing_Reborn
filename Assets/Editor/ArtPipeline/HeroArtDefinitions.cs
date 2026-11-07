using AdventureKing.Gameplay;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Clip table for each hero class. Frame numbers are 30 fps take frames of the Meshy animations
    /// exported by Tools/blender/export_character.py. Contact frames come from
    /// Tools/blender/analyze_actions.py (wrist-speed peaks), see ArtSource/Specs/analysis_*.json.
    /// </summary>
    public static class HeroArtDefinitions
    {
        public sealed class Clip
        {
            public string Name, Take;
            public float First, Last;
            public bool Loop;
            public bool BakeY;
            public float Speed = 1f;
            public (float frame, string fn)[] Events = new (float, string)[0];

            public Clip(string name, string take, float first, float last, bool loop = false)
            {
                Name = name; Take = take; First = first; Last = last; Loop = loop;
            }
        }

        public sealed class Hero
        {
            public HeroClass Class;
            public string Folder, Fbx;
            public (string hand, string weapon)[] Weapons;
            public Clip[] Clips;
        }

        private static Clip[] Shared(string idleTake, float idleLast) => new[]
        {
            new Clip("Idle", idleTake, 0, idleLast, true),
            new Clip("Run", "Run_02", 0, 17, true) { Events = new[] { (0.5f, "OnFootstep"), (9f, "OnFootstep") } },
            new Clip("Jump", "Regular_Jump", 12, 19) { BakeY = true },
            new Clip("Fall", "Regular_Jump", 19, 25) { BakeY = true },
            new Clip("Climb", "Ladder_Climb_Loop", 0, 38, true) { BakeY = true },
            new Clip("Hit", "Hit_Reaction", 0, 30) { Speed = 1.5f },
            new Clip("Death", "dying_backwards", 0, 53),
        };

        private static Clip[] With(Clip[] shared, params Clip[] extra)
        {
            var all = new Clip[shared.Length + extra.Length];
            shared.CopyTo(all, 0);
            extra.CopyTo(all, shared.Length);
            return all;
        }

        public static readonly Hero[] Heroes =
        {
            new Hero
            {
                Class = HeroClass.Swordsman, Folder = "Assets/Art/Characters/Swordsman", Fbx = "Swordsman.fbx",
                Weapons = new[] { ("RightHand", "Sword_Starter") },
                Clips = With(Shared("Combat_Stance", 40),
                    new Clip("Attack", "Right_Hand_Sword_Slash", 0, 30) { Speed = 1.2f, Events = new[] { (13f, "OnAttackImpact") } },
                    new Clip("Skill", "Triple_Combo_Attack", 0, 70)
                    {
                        Speed = 1.35f,
                        Events = new[] { (2f, "OnSkillCast"), (18f, "OnSkillImpact"), (36f, "OnSkillImpact"), (54f, "OnSkillImpact") }
                    })
            },
            new Hero
            {
                Class = HeroClass.Mage, Folder = "Assets/Art/Characters/Mage", Fbx = "Mage.fbx",
                Weapons = new[] { ("RightHand", "Staff_Starter") },
                Clips = With(Shared("Idle_02", 45),
                    new Clip("Attack", "mage_soell_cast", 0, 40) { Speed = 1.0f, Events = new[] { (14f, "OnAttackImpact") } },
                    new Clip("Skill", "Charged_Spell_Cast", 0, 62)
                    {
                        Speed = 1.0f, Events = new[] { (6f, "OnSkillCast"), (42f, "OnSkillImpact") }
                    })
            },
            new Hero
            {
                Class = HeroClass.Assassin, Folder = "Assets/Art/Characters/Assassin", Fbx = "Assassin.fbx",
                Weapons = new[] { ("RightHand", "Dagger_Starter"), ("LeftHand", "Dagger_Starter_L") },
                Clips = With(Shared("Combat_Stance", 40),
                    new Clip("Attack", "Double_Combo_Attack", 4, 36) { Speed = 2.4f, Events = new[] { (15f, "OnAttackImpact"), (29f, "OnAttackImpact") } },
                    new Clip("Skill", "Double_Blade_Spin", 0, 56)
                    {
                        Speed = 1.25f,
                        Events = new[] { (4f, "OnSkillCast"), (20f, "OnSkillImpact"), (32f, "OnSkillImpact"), (40f, "OnSkillImpact") }
                    },
                    new Clip("Dodge", "Roll_Dodge", 0, 44) { Speed = 1.4f })
            },
            new Hero
            {
                Class = HeroClass.Archer, Folder = "Assets/Art/Characters/Archer", Fbx = "Archer.fbx",
                Weapons = new[] { ("LeftHand", "Bow_Starter") },
                Clips = With(Shared("Idle_02", 45),
                    new Clip("Attack", "Archery_Shot_3", 0, 16) { Speed = 0.8f, Events = new[] { (10f, "OnAttackImpact") } },
                    new Clip("Skill", "Archery_Shot", 24, 76)
                    {
                        Speed = 1.15f, Events = new[] { (26f, "OnSkillCast"), (64f, "OnSkillImpact") }
                    })
            },
        };
    }
}
