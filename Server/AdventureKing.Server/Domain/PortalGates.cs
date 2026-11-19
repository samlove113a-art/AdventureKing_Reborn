using System.Text.Json;

namespace AdventureKing.Server.Domain;

/// <summary>Quest gates on the forward portals (config/portal_gates.json, DRAFT): a gated portal stays shut until the character has CLAIMED
/// the named quest. Ungated portals (back ways, hub halls) are always open. The refusal text names the quest so the player is never stuck
/// guessing. Server-owned: the client only shows the message of the "portal_locked" error.</summary>
public sealed class PortalGates
{
    private readonly Dictionary<string, string> gates;
    private readonly QuestService quests;

    private PortalGates(Dictionary<string, string> gates, QuestService quests) { this.gates = gates; this.quests = quests; }

    public IReadOnlyDictionary<string, string> Gates => gates;

    public static PortalGates Load(string path, QuestService quests)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        var map = new Dictionary<string, string>();
        foreach (JsonProperty g in doc.RootElement.GetProperty("gates").EnumerateObject())
            map[g.Name] = g.Value.GetProperty("quest").GetString() ?? "";
        foreach ((string portal, string quest) in map)
        {
            if (!CharacterService.Portals.ContainsKey(portal)) throw new InvalidDataException("portal_gates.json: unknown portal " + portal);
            if (quest != "" && !quests.Config.quests.Any(q => q.id == quest)) throw new InvalidDataException("portal_gates.json: unknown quest " + quest);
        }
        return new PortalGates(map, quests);
    }

    /// <summary>null = the portal is open for this character, otherwise the text to show.</summary>
    public string? Check(string accountId, string characterId, string portalId)
    {
        if (!gates.TryGetValue(portalId, out string? questId) || questId == "") return null;
        if (quests.IsClaimed(accountId, characterId, questId)) return null;
        string title = quests.Config.quests.FirstOrDefault(q => q.id == questId)?.title ?? questId;
        return $"Cổng chưa mở: hãy hoàn thành nhiệm vụ «{title}» ở bản đồ này rồi quay lại.";
    }
}
