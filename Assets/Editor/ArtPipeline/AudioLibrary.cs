using System.Collections.Generic;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Wires the original synthesised class SFX (Tools/audio/synth_sfx.py -> Assets/Audio/SFX) and the original
    /// in-code BGM drafts (<see cref="MusicComposer"/>) into AdventureAudioDirector. The BGM is labelled DRAFT:
    /// it is original, but not yet approved by the owner for a commercial release.
    /// </summary>
    public static class AudioLibrary
    {
        private const string Dir = "Assets/Audio/SFX";

        public static void AssignTo(AdventureAudioDirector director)
        {
            if (!director) return;
            ConfigureImporters();
            var so = new SerializedObject(director);
            var sets = so.FindProperty("classSounds");
            var classes = (HeroClass[])System.Enum.GetValues(typeof(HeroClass));
            sets.arraySize = classes.Length;
            for (int i = 0; i < classes.Length; i++)
            {
                var e = sets.GetArrayElementAtIndex(i);
                string c = classes[i].ToString();
                e.FindPropertyRelative("heroClass").enumValueIndex = (int)classes[i];
                e.FindPropertyRelative("attackWhoosh").objectReferenceValue = Clip(c + "_AttackWhoosh");
                e.FindPropertyRelative("attackImpact").objectReferenceValue = Clip(c + "_AttackImpact");
                e.FindPropertyRelative("skillCast").objectReferenceValue = Clip(c + "_SkillCast");
                e.FindPropertyRelative("skillImpact").objectReferenceValue = Clip(c + "_SkillImpact");
            }
            // Stage music: original in-code compositions (MusicComposer, DRAFT until the owner approves by ear).
            if (System.Array.Exists(MusicComposer.Tracks, t => !System.IO.File.Exists(MusicComposer.PathOf(t)))) MusicComposer.ComposeMissing();
            (AdventureMusicStage stage, string track)[] map =
            {
                (AdventureMusicStage.Splash, "BGM_Login"), (AdventureMusicStage.Authentication, "BGM_Login"), (AdventureMusicStage.CharacterSelection, "BGM_Login"),
                (AdventureMusicStage.Village, "BGM_Village_Day"), (AdventureMusicStage.VillageNight, "BGM_Village_Night"),
                (AdventureMusicStage.Exploration, "BGM_Meadow"), (AdventureMusicStage.Boss, "BGM_Boss"), (AdventureMusicStage.Wedding, "BGM_Wedding"),
                (AdventureMusicStage.MushroomKingdom, "BGM_Ch1_MushroomVillage"),
            };
            var music = so.FindProperty("soundtracks");
            music.arraySize = map.Length;
            for (int i = 0; i < map.Length; i++)
            {
                var e = music.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("stage").enumValueIndex = (int)map[i].stage;
                e.FindPropertyRelative("music").objectReferenceValue = MusicComposer.Clip(map[i].track);
                e.FindPropertyRelative("ambience").objectReferenceValue = null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Dir}/{name}.wav");
            if (!clip) Debug.LogWarning("[AUDIO] missing " + name);
            return clip;
        }

        private static void ConfigureImporters()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ai = (AudioImporter)AssetImporter.GetAtPath(path);
                var s = ai.defaultSampleSettings;
                if (s.loadType == AudioClipLoadType.DecompressOnLoad && s.compressionFormat == AudioCompressionFormat.Vorbis) continue;
                s.loadType = AudioClipLoadType.DecompressOnLoad;   // short SFX: no decode latency at the hit frame
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                ai.defaultSampleSettings = s;
                ai.forceToMono = true;
                ai.SaveAndReimport();
            }
        }
    }
}
