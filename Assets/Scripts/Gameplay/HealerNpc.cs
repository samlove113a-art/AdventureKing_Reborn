using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Dược Sư Mộc Linh at her herb counter in the hub. Walk up: "E · Nói chuyện" appears; talking turns her to the hero,
    /// she smiles (face rig) and greets with a flourish of her staff, the quest event "talk:healer" is sent and her shop
    /// opens. Closing the shop turns her back to the counter.
    /// </summary>
    public sealed class HealerNpc : MonoBehaviour
    {
        [SerializeField] private Transform visual;  // turning pivot
        [SerializeField] private Transform model;   // the chibi model (dressed at runtime)
        [SerializeField] private Animator animator;
        [SerializeField] private HeroFace face;
        [SerializeField] private GameObject prompt;
        [SerializeField] private HealerShopUI shop;
        [SerializeField, Min(0.5f)] private float range = 2.6f;

        public bool PlayerNear { get; private set; }
        public int Greetings { get; private set; }
        private float targetYaw = 180f;
        private static readonly int SkillTrigger = Animator.StringToHash("Skill");

        public void Bind(Transform pivot, Transform m, Animator a, HeroFace f, GameObject p, HealerShopUI s) { visual = pivot; model = m; animator = a; face = f; prompt = p; shop = s; }
        public HeroOutfit Outfit { get; private set; }

        private void Start()
        {
            if (prompt) prompt.SetActive(false);
            if (shop) shop.Closed += () => targetYaw = 180f;
            // a cute herbalist look on the shared chibi rig: lotus crown + dragonfly wings (in-engine cosmetics)
            if (model)
            {
                Outfit = AdventureWorldSession.DressHero(model.gameObject, HeroClass.Mage);
                if (Outfit) Outfit.Apply(new OutfitLoadout().With("head", "head_lotus_crown").With("back", "back_dragonfly_wings"));
            }
        }

        private void Update()
        {
            var world = AdventureWorldSession.Current;
            if (!world || !world.Player) return;
            Vector2 d = world.Player.transform.position - transform.position;
            bool near = Mathf.Abs(d.x) < range && Mathf.Abs(d.y) < 2.2f && !(shop && shop.IsOpen) && !(world.Traveling || world.Leaving);
            if (near != PlayerNear)
            {
                PlayerNear = near;
                if (prompt) prompt.SetActive(near);
                // 180 = facing the camera; turn three-quarters toward the hero while they are near
                targetYaw = !near ? 180f : d.x > 0f ? 135f : 225f;
            }
            var input = world.Player.GetComponent<PlayerInput2D>();
            if (near && input && input.enabled && input.ConsumeInteract()) Talk();
            if (visual) visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(0f, targetYaw, 0f), 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        public void Talk()
        {
            var world = AdventureWorldSession.Current;
            if (!world || world.Character == null || (shop && shop.IsOpen)) return;
            Greetings++;
            if (prompt) prompt.SetActive(false);
            if (face) face.Pulse(HeroFace.Expression.Happy, 3f);
            if (animator) animator.SetTrigger(SkillTrigger); // greeting flourish of her staff
            QuestBook.Event("talk:healer");
            if (shop) shop.Open($"Chào <b>{world.Character.characterName}</b>! Thuốc mới sắc sáng nay đây — cần gì cứ chọn nhé.");
        }
    }
}
