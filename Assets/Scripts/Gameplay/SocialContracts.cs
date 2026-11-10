using System;

namespace AdventureKing.Gameplay
{
    // Field names = server DTOs (Server/AdventureKing.Server/Domain/SocialServices.cs, GuildMarriageServices.cs).

    [Serializable] public sealed class PeerState
    {
        public string id, name, anim = "idle", emote = "", guild = "", guildEmblem = "", partner = "", title = "";
        public int heroClass, level, facing = 1, emoteSeq;
        public float x, y;
        public bool friend;
        public OutfitLoadout outfit = new OutfitLoadout();
    }
    [Serializable] public sealed class ChatMessageState { public long id, ms; public string channel, senderId, sender, toId, toName, text; }
    [Serializable] public sealed class BadgesState { public int friendRequests, guildInvites, guildApplications, proposals; public int Total => friendRequests + guildInvites + guildApplications + proposals; }
    [Serializable] public sealed class CeremonyStepState { public string id; public long startMs; public int ms; }
    [Serializable] public sealed class CeremonyState
    {
        public string id, state, invitedBy, aId, aName, bId, bName;
        public int aClass, bClass;
        public long inviteExpiresMs, startMs, endMs, serverMs;
        public CeremonyStepState[] steps = new CeremonyStepState[0];
        public bool Valid => !string.IsNullOrEmpty(id);
    }
    [Serializable] public sealed class SyncState
    {
        public long serverMs, chatCursor;
        public string mapId;
        public bool corrected;
        public float x, y;
        public PeerState[] peers = new PeerState[0];
        public ChatMessageState[] chat = new ChatMessageState[0];
        public BadgesState badges = new BadgesState();
        public CeremonyState ceremony = new CeremonyState();
    }

    [Serializable] public sealed class FriendState { public string id, name, mapId; public int heroClass, level; public bool online; }
    [Serializable] public sealed class FriendsState { public FriendState[] friends = new FriendState[0], incoming = new FriendState[0], outgoing = new FriendState[0], blocked = new FriendState[0]; }

    [Serializable] public sealed class GuildMemberState { public string id, name, role; public int heroClass, level; public long devotion; public bool online; }
    [Serializable] public sealed class GuildQuestState { public string id, text; public int have, need; public bool done; public long rewardExp, rewardDevotion; }
    [Serializable] public sealed class GuildSummaryState { public string id, name, emblem, leader; public int level, members, maxMembers; }
    [Serializable] public sealed class GuildState
    {
        public string id, name, emblem, notice, myRole;
        public int level, maxMembers;
        public long exp, expNext, fundGold, myDevotion;
        public GuildMemberState[] members = new GuildMemberState[0], applications = new GuildMemberState[0];
        public GuildQuestState[] quests = new GuildQuestState[0];
        public bool Valid => !string.IsNullOrEmpty(id);
    }
    [Serializable] public sealed class GuildViewState
    {
        public GuildState guild = new GuildState();
        public GuildSummaryState[] browse = new GuildSummaryState[0], invites = new GuildSummaryState[0], applied = new GuildSummaryState[0];
        public string[] emblems = new string[0];
        public long createFeeGold;
        public bool pricesApproved, economyEnabled;
        public long[] contributionGold = new long[0];
        public string status;
    }

    [Serializable] public sealed class ProposalState { public string id, name; public int heroClass, level; }
    [Serializable] public sealed class MarriageState
    {
        public string status = "none", partnerId, partnerName;
        public int partnerClass;
        public bool partnerOnline, pricesApproved;
        public long engagedMs, wedMs, ringPriceGold;
        public ProposalState[] incoming = new ProposalState[0], outgoing = new ProposalState[0];
        public CeremonyState ceremony = new CeremonyState();
    }
}
