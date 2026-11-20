using System.Linq;
using AdventureKing.QA;
using AdventureKing.QA.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.Tests
{
    public sealed class ProjectSetupTests
    {
        [Test]
        public void Editor_Version_Matches_Handoff() =>
            StringAssert.StartsWith("6000.6.7f1", Application.unityVersion);

        [Test]
        public void Layers_Are_Configured()
        {
            Assert.AreEqual(QaWorldFactory.GroundLayer, LayerMask.NameToLayer("Ground"));
            Assert.AreEqual(QaWorldFactory.PlayerLayer, LayerMask.NameToLayer("Player"));
            Assert.AreEqual(QaWorldFactory.EnemyLayer, LayerMask.NameToLayer("Enemy"));
        }

        [Test]
        public void Input_System_Is_The_Active_Backend()
        {
#if ENABLE_INPUT_SYSTEM
            Assert.Pass();
#else
            Assert.Fail("ENABLE_INPUT_SYSTEM not defined; PlayerInput2D would use the legacy branch");
#endif
        }

        [Test]
        public void Internal_QA_Scene_Exists_And_Is_Not_In_Build_Settings()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(QaSceneBuilder.ScenePath), "QA scene not generated");
            Assert.IsFalse(EditorBuildSettings.scenes.Any(s => s.path.Contains("QA_Internal")),
                "Internal QA scenes must never ship in a build");
        }
    }
}
