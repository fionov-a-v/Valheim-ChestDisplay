using System;
using System.Collections.Generic;
using ChestDisplay.Logic;
using UnityEngine;

namespace ChestDisplay
{
    /// <summary>
    /// Табличка на сундуке. Находит сундук у себя за спиной и показывает иконку его первого предмета (верхняя левая
    /// занятая ячейка). Иконка меняется сразу, как меняется содержимое: на событие инвентаря сундука (оно приходит и при
    /// изменении у этого игрока, и когда игра подгружает чужие изменения из сети — раз в секунду), плюс редкая проверка
    /// на всякий случай. Наведение и «Использовать» передаются сундуку — сквозь табличку сундук открывается как обычно.
    /// Сундук разрушили или разобрали — табличка снимается вместе с ним (ресурсы выпадают); табличка, за которой сундука
    /// нет вовсе, тоже отваливается.
    /// </summary>
    public class ChestSign : MonoBehaviour, Hoverable, Interactable
    {
        public const string IconName = "chestdisplay_icon";

        /// <summary>Таблички в мире (без призрака при строительстве).</summary>
        public static readonly List<ChestSign> All = new List<ChestSign>();

        private const float SearchInterval = 1f;
        private const float PollInterval = 2f;

        /// <summary>Сколько поисков подряд (раз в SearchInterval) за табличкой нет сундука, прежде чем она отвалится.</summary>
        private const int OrphanSearches = 8;

        private ZNetView m_nview;
        private WearNTear m_wear;
        private MeshFilter m_iconFilter;
        private MeshRenderer m_iconRenderer;
        private bool m_ghost;

        private Container m_chest;
        private Inventory m_inventory;
        private WearNTear m_chestWear;
        private ItemDrop.ItemData m_item;
        private Sprite m_shown;
        private bool m_applied;
        private bool m_dirty;
        private float m_nextSearch;
        private float m_nextPoll;
        private int m_orphanSearches;

        /// <summary>Сундук, на котором висит табличка (null, пока не найден).</summary>
        public Container Chest => m_chest;

        private void Awake()
        {
            // Призрак при строительстве создаётся с отключённой сетевой инициализацией.
            m_ghost = ZNetView.m_forceDisableInit;
            m_nview = GetComponent<ZNetView>();
            m_wear = GetComponent<WearNTear>();
            Transform icon = transform.Find(IconName);
            if (icon != null)
            {
                m_iconFilter = icon.GetComponent<MeshFilter>();
                m_iconRenderer = icon.GetComponent<MeshRenderer>();
            }
            Show(null);
            if (!m_ghost)
            {
                All.Add(this);
            }
        }

        private void OnDestroy()
        {
            All.Remove(this);
            Unlink();
        }

        private void Update()
        {
            if (m_ghost || m_nview == null || !m_nview.IsValid())
            {
                return;
            }
            if (m_chest == null)
            {
                if (m_inventory != null)
                {
                    Unlink(); // сундук пропал (выгружен или уничтожен)
                    Show(null);
                }
                if (Time.time < m_nextSearch)
                {
                    return;
                }
                m_nextSearch = Time.time + SearchInterval;
                Container chest = ChestMount.FindBehind(this);
                if (chest == null)
                {
                    CheckOrphan();
                    return;
                }
                m_orphanSearches = 0;
                Link(chest);
            }
            if (m_dirty || Time.time >= m_nextPoll)
            {
                m_dirty = false;
                m_nextPoll = Time.time + PollInterval;
                Show(FirstItem(m_inventory));
            }
        }

        /// <summary>
        /// За табличкой нет сундука: его разобрали или сломали, пока табличка не была с ним связана, или она висит на том,
        /// что сундуком не считается (бочка, бродильня — поставлена старой версией мода). Если местность вокруг загружена
        /// целиком, а сундука всё нет несколько проверок подряд — табличка отваливается, ресурсы выпадают. Решает владелец.
        /// </summary>
        private void CheckOrphan()
        {
            if (!m_nview.IsOwner() || ZNetScene.instance == null || !ZNetScene.instance.IsAreaReady(transform.position))
            {
                m_orphanSearches = 0;
                return;
            }
            if (++m_orphanSearches >= OrphanSearches && m_wear != null)
            {
                m_orphanSearches = 0;
                m_wear.Remove();
            }
        }

        /// <summary>Призрак при строительстве: показать, что окажется на табличке у этого сундука.</summary>
        public void Preview(Container chest)
        {
            Show(chest != null ? FirstItem(chest.GetInventory()) : null);
        }

        private void Link(Container chest)
        {
            m_chest = chest;
            m_inventory = chest.GetInventory();
            if (m_inventory != null)
            {
                m_inventory.m_onChanged = (Action)Delegate.Combine(m_inventory.m_onChanged, new Action(OnChestChanged));
            }
            m_chestWear = chest.GetComponent<WearNTear>();
            if (m_chestWear != null)
            {
                m_chestWear.m_onDestroyed = (Action)Delegate.Combine(m_chestWear.m_onDestroyed, new Action(OnChestDestroyed));
            }
            m_dirty = true;
        }

        private void Unlink()
        {
            if (m_inventory != null)
            {
                m_inventory.m_onChanged = (Action)Delegate.Remove(m_inventory.m_onChanged, new Action(OnChestChanged));
            }
            // Сравнение как object: у уничтоженного сундука делегат всё равно надо отцепить.
            if ((object)m_chestWear != null)
            {
                m_chestWear.m_onDestroyed = (Action)Delegate.Remove(m_chestWear.m_onDestroyed, new Action(OnChestDestroyed));
            }
            m_chest = null;
            m_inventory = null;
            m_chestWear = null;
            m_item = null;
        }

        private void OnChestChanged()
        {
            m_dirty = true;
        }

        /// <summary>
        /// Сундук разрушен или разобран (вызывается у владельца сундука) — снять и табличку. Remove уходит владельцу
        /// таблички, тот её разрушает, и ресурсы выпадают, как у постройки, оставшейся без опоры.
        /// </summary>
        private void OnChestDestroyed()
        {
            if (this != null && m_wear != null && m_nview != null && m_nview.IsValid())
            {
                m_wear.Remove();
            }
        }

        private void Show(ItemDrop.ItemData item)
        {
            m_item = item;
            Sprite icon = IconOf(item);
            if (m_applied && icon == m_shown)
            {
                return;
            }
            m_applied = true;
            m_shown = icon;
            IconArt.Apply(m_iconFilter, m_iconRenderer, icon);
        }

        /// <summary>Предмет в верхней левой занятой ячейке (как его видит игрок) или null, если сундук пуст.</summary>
        public static ItemDrop.ItemData FirstItem(Inventory inventory)
        {
            if (inventory == null)
            {
                return null;
            }
            ItemDrop.ItemData best = null;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item != null && (best == null ||
                    DisplayRules.Precedes(item.m_gridPos.x, item.m_gridPos.y, best.m_gridPos.x, best.m_gridPos.y)))
                {
                    best = item;
                }
            }
            return best;
        }

        private static Sprite IconOf(ItemDrop.ItemData item)
        {
            Sprite[] icons = item?.m_shared?.m_icons;
            if (icons == null || icons.Length == 0)
            {
                return null;
            }
            int variant = item.m_variant >= 0 && item.m_variant < icons.Length ? item.m_variant : 0;
            return icons[variant];
        }

        // ================================================================== сквозь табличку — к сундуку

        public string GetHoverText()
        {
            if (m_chest == null)
            {
                return SignLocalization.Format("$chestdisplay_piece");
            }
            string text = m_chest.GetHoverText();
            if (m_item != null)
            {
                text += "\n<color=#C8C8C8>" + SignLocalization.Format("$chestdisplay_hover",
                    SignLocalization.Format(m_item.m_shared.m_name)) + "</color>";
            }
            return text;
        }

        public string GetHoverName() => m_chest != null ? m_chest.GetHoverName() : "$chestdisplay_piece";

        public float GetHoverOffset() => m_chest != null ? m_chest.GetHoverOffset() : 0f;

        public bool Interact(Humanoid user, bool hold, bool alt) => m_chest != null && m_chest.Interact(user, hold, alt);

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => m_chest != null && m_chest.UseItem(user, item);
    }
}
