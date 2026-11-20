using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Đội Trưởng Liên Minh: after his line, opens the guild tab of the social window (create / join / manage).</summary>
    public sealed class GuildCaptain : MonoBehaviour
    {
        [SerializeField] private NpcTalkSpot talk;
        public void Bind(NpcTalkSpot t) => talk = t;
        private void Start() { if (talk) talk.Talked += Open; }
        private void OnDestroy() { if (talk) talk.Talked -= Open; }
        private void Open() { if (SocialPanel.Instance) SocialPanel.Instance.OpenTab("guild"); }
    }
}
