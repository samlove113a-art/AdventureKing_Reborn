using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Minimap of the current side-view map, built from the map's real layout (TownLayout: ground, decks, ladders /
    /// ropes, portals) plus NPC positions found in the scene; the player marker follows the hero.
    /// </summary>
    public sealed class HudMinimap : MonoBehaviour
    {
        [SerializeField] private RectTransform area;
        [SerializeField] private Sprite dot;
        private RectTransform player;
        private float minX, maxX, minY = -2f, maxY = 14f;

        private IEnumerator Start()
        {
            float until = Time.time + 5f;
            while ((!AdventureWorldSession.Current || AdventureWorldSession.Current.Player == null) && Time.time < until) yield return null;
            AdventureWorldSession world = AdventureWorldSession.Current;
            if (!world || !area) yield break;
            bool hub = world.MapId == TownLayout.Hub.MapId;
            minX = hub ? TownLayout.Hub.MinX : TownLayout.Meadow.MinX;
            maxX = hub ? TownLayout.Hub.MaxX : TownLayout.Meadow.MaxX;
            TownLayout.Deck[] decks = hub ? TownLayout.Hub.Decks : TownLayout.Meadow.Decks;
            TownLayout.Climb[] climbs = hub ? TownLayout.Hub.Climbs : TownLayout.Meadow.Climbs;
            TownLayout.PortalSpot[] portals = hub ? TownLayout.Hub.Portals : TownLayout.Meadow.Portals;
            Bar(minX, maxX, 0f, 3f, new Color(0.55f, 0.82f, 0.45f, 0.95f));
            foreach (TownLayout.Deck d in decks) Bar(d.XMin, d.XMax, d.Top, 2f, new Color(0.86f, 0.66f, 0.4f, 0.95f));
            foreach (TownLayout.Climb c in climbs) VBar(c.X, c.Bottom, c.VisualTop, c.Rope ? new Color(0.85f, 0.75f, 0.5f) : new Color(0.75f, 0.55f, 0.3f));
            foreach (TownLayout.PortalSpot p in portals) Marker(p.X, 1.4f, 14f, new Color(0.5f, 0.95f, 1f));
            foreach (NpcActor n in FindObjectsByType<NpcActor>(FindObjectsInactive.Exclude)) Marker(n.transform.position.x, n.transform.position.y + 0.8f, 10f, new Color(1f, 0.84f, 0.4f));
            player = Marker(world.Player.transform.position.x, world.Player.transform.position.y, 13f, new Color(1f, 0.35f, 0.35f));
        }

        private Vector2 Map(float x, float y)
        {
            Rect r = area.rect;
            return new Vector2(Mathf.InverseLerp(minX, maxX, x) * r.width, Mathf.InverseLerp(minY, maxY, y) * r.height);
        }

        private Image Rect(string name, Vector2 pos, Vector2 size, Color c, Sprite s = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(area, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = c;
            img.sprite = s;
            img.raycastTarget = false;
            return img;
        }

        private void Bar(float x0, float x1, float y, float h, Color c)
        {
            Vector2 a = Map(x0, y), b = Map(x1, y);
            Rect("Ground", (a + b) * 0.5f, new Vector2(Mathf.Max(2f, b.x - a.x), h), c);
        }

        private void VBar(float x, float y0, float y1, Color c)
        {
            Vector2 a = Map(x, y0), b = Map(x, y1);
            Rect("Climb", (a + b) * 0.5f, new Vector2(2f, Mathf.Max(2f, b.y - a.y)), c);
        }

        private RectTransform Marker(float x, float y, float size, Color c) => Rect("Marker", Map(x, y), new Vector2(size, size), c, dot).rectTransform;

        private void LateUpdate()
        {
            AdventureWorldSession world = AdventureWorldSession.Current;
            if (player && world && world.Player) player.anchoredPosition = Map(world.Player.transform.position.x, world.Player.transform.position.y);
        }
    }
}

