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
            // Loop ranges = the full take cycles found by MotionQualityAudit.LoopSearch (QA/AnimAudit/loop_search.md);
            // the old 0-17 Run / 0-38 Climb cuts popped 47-54° at every loop seam.
            new Clip("Run", "Run_02", 0, 21, true) { Events = new[] { (0.5f, "OnFootstep"), (11f, "OnFootstep") } },
            new Clip("Jump", "Regular_Jump", 12, 19) { BakeY = true },
            new Clip("Fall", "Regular_Jump", 19, 25) { BakeY = true },
            // The take lifts the hips ~0.2 m per cycle; HeroArtPipeline derives an in-place copy (drift removed) because
            // the motor already lifts the hero - otherwise the body sinks ~30 cm at every loop seam.
            new Clip("Climb", "Ladder_Climb_Loop", 1, 48, true) { BakeY = true },
            new Clip("Hit", "Hit_Reaction", 0, 30) { Speed = 1.5f },
            new Clip("Death", "dying_backwards", 0, 53),
            // Wedding ceremony gestures (Meshy library batch, 2026-11-10, 15 credits per class): ring on one knee,
            // toast, cheer, bow, celebration.
            new Clip("Kneel", "Kneel_on_One_Knee_and_Stand", 0, 60) { Speed = 0.7f }, // held longer: the ring moment
            // hand rises from frame ~54, at the mouth 96-114, down by ~144 (measured): the idle lead-in is cut
            new Clip("Toast", "Stand_and_Drink", 50, 150),
            new Clip("Cheer", "Cheer_with_Both_Hands_Up", 0, 44),
            new Clip("Celebrate", "Cheer_with_Both_Hands", 0, 70),
            new Clip("Bow", "Formal_Bow", 0, 184) { Speed = 1.5f },
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
                Clips = With(Shared("Combat_Stance", 50),
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
                Clips = With(Shared("Idle_02", 56),
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
                Clips = With(Shared("Combat_Stance", 50),
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
                Clips = With(Shared("Idle_02", 56),
                    new Clip("Attack", "Archery_Shot_3", 0, 16) { Speed = 0.8f, Events = new[] { (10f, "OnAttackImpact") } },
                    new Clip("Skill", "Archery_Shot", 24, 76)
                    {
                        Speed = 1.15f, Events = new[] { (26f, "OnSkillCast"), (64f, "OnSkillImpact") }
                    })
            },
        };
    }
}
