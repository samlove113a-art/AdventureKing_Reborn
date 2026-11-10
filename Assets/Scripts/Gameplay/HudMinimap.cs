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
        [SerializeField] private Sprite portalIcon, npcIcon, playerIcon, sunIcon, moonIcon;
        [SerializeField] private Text mapTitle, clock;
        [SerializeField] private Image phaseIcon;
        private RectTransform player;
        private Image playerImg;
        private int shownMinute = -1;
        private float minX, maxX, minY = -2f, maxY = 14f;

        private IEnumerator Start()
        {
            float until = Time.time + 5f;
            while ((!AdventureWorldSession.Current || AdventureWorldSession.Current.Player == null) && Time.time < until) yield return null;
            AdventureWorldSession world = AdventureWorldSession.Current;
            if (!world || !area) yield break;
            TownLayout.MapInfo map = TownLayout.For(world.MapId);
            minX = map.MinX;
            maxX = map.MaxX;
            TownLayout.Deck[] decks = map.Decks;
            TownLayout.Climb[] climbs = map.Climbs;
            TownLayout.PortalSpot[] portals = map.Portals;
            Bar(minX, maxX, 0f, 3f, new Color(0.55f, 0.82f, 0.45f, 0.95f));
            foreach (TownLayout.Deck d in decks) Bar(d.XMin, d.XMax, d.Top, 2f, new Color(0.86f, 0.66f, 0.4f, 0.95f));
            foreach (TownLayout.Climb c in climbs) VBar(c.X, c.Bottom, c.VisualTop, c.Rope ? new Color(0.85f, 0.75f, 0.5f) : new Color(0.75f, 0.55f, 0.3f));
            if (mapTitle) mapTitle.text = map.DisplayName;
            foreach (TownLayout.PortalSpot p in portals) Marker(p.X, 1.6f, 24f, portalIcon, new Color(0.5f, 0.95f, 1f));
            foreach (NpcActor n in FindObjectsByType<NpcActor>(FindObjectsInactive.Exclude)) Marker(n.transform.position.x, n.transform.position.y + 1.2f, 20f, npcIcon, new Color(1f, 0.84f, 0.4f));
            player = Marker(world.Player.transform.position.x, world.Player.transform.position.y + 0.9f, 22f, playerIcon, new Color(1f, 0.35f, 0.35f));
            playerImg = player.GetComponent<Image>();
            player.SetAsLastSibling();
        }

        /// <summary>Vietnam time (HH:mm) with a sun or moon, matching the world's day/night.</summary>
        private void UpdateClock()
        {
            float h = DayNightCycle.VietnamHour;
            int minute = Mathf.FloorToInt(h * 60f) % 1440;
            float night = DayNightCycle.Current ? DayNightCycle.Current.Night : DayNightCycle.TargetNight;
            if (phaseIcon) { Sprite s = night > 0.5f ? moonIcon : sunIcon; if (s && phaseIcon.sprite != s) phaseIcon.sprite = s; }
            if (minute == shownMinute || !clock) return;
            shownMinute = minute;
            clock.text = $"{minute / 60:00}:{minute % 60:00}";
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

        /// <summary>Symbol marker (falls back to a tinted dot when the icon is missing).</summary>
        private RectTransform Marker(float x, float y, float size, Sprite icon, Color fallback)
        {
            Image img = icon ? Rect("Marker", Map(x, y), new Vector2(size, size), Color.white, icon) : Rect("Marker", Map(x, y), new Vector2(size * 0.55f, size * 0.55f), fallback, dot);
            img.preserveAspect = true;
            return img.rectTransform;
        }

        private void LateUpdate()
        {
            UpdateClock();
            AdventureWorldSession world = AdventureWorldSession.Current;
            if (player && world && world.Player)
            {
                player.anchoredPosition = Map(world.Player.transform.position.x, world.Player.transform.position.y + 0.9f);
                if (playerImg) playerImg.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f)); // gentle pulse
            }
        }
    }
}

