using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Adds the face-plane coordinates (UV channel 3) to the four hero body meshes while Unity imports their FBX.
    /// Nothing in the FBX file changes; re-importing always recomputes the same data from the bind pose.
    /// </summary>
    public sealed class HeroFaceImport : AssetPostprocessor
    {
        public override uint GetVersion() => 3; // bump to force re-import when the face mapping changes

        private void OnPostprocessModel(GameObject g)
        {
            HeroArtDefinitions.Hero hero = HeroArtDefinitions.Heroes.FirstOrDefault(h => $"{h.Folder}/{h.Fbx}" == assetPath);
            if (hero == null) return;
            SkinnedMeshRenderer body = g.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => !s.name.StartsWith("Weapon_"));
            if (!body || !body.sharedMesh) return;
            try
            {
                Vector4[] uv = HeroFaceRig.FaceCoordinates(g);
                body.sharedMesh.SetUVs(3, new List<Vector4>(uv));
                int face = uv.Count(u => u.z > 0.5f);
                Debug.Log($"[FACE] {hero.Class}: face coordinates on {face}/{uv.Length} vertices (UV3)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FACE] {hero.Class}: face coordinates skipped ({e.Message})");
            }
        }
    }
}
