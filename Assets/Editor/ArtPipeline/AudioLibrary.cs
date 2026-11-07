using System.Collections.Generic;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Wires the original synthesised class SFX (Tools/audio/synth_sfx.py -> Assets/Audio/SFX) into
    /// AdventureAudioDirector. Stage music is intentionally NOT assigned: no original or licensed
    /// soundtrack exists yet, and silent/placeholder tracks must not be passed off as music.
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
