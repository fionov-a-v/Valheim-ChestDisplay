using System;
using System.Collections.Generic;
using ChestDisplay.Logic;
using UnityEngine;

namespace ChestDisplay
{
    /// <summary>
    /// Табличка на сундуке. Находит сундук у себя за спиной и показывает иконку его первого предмета (верхняя левая
    /// занятая ячейка), а если включено — сколько этого предмета в сундуке (числом внизу доски). Иконка меняется сразу, как меняется
    /// содержимое: на событие инвентаря сундука (оно приходит и при изменении у этого игрока, и когда игра подгружает
    /// чужие изменения из сети — раз в секунду), плюс редкая проверка на всякий случай. Наведение и «Использовать»
    /// передаются сундуку — сквозь табличку сундук открывается как обычно.
    /// Опустел сундук — табличка показывает последний предмет тусклым (и «0», если число включено): она помнит его
    /// в своих сетевых данных, поэтому помнят все игроки и после перезахода.
    /// Размер — по настройке SizePercent от меньшей стороны поверхности, на которой табличка висит. Табличка на крышке
    /// при открытии сундука уезжает вместе с крышкой.
    /// Сундук разрушили или разобрали — табличка снимается вместе с ним (ресурсы выпадают); табличка, за которой сундука
    /// нет вовсе, тоже отваливается.
    /// </summary>
    public class ChestSign : MonoBehaviour, Hoverable, Interactable
    {
        /// <summary>Всё видимое и коллайдер таблички: масштабируется по размеру и ездит с крышкой.</summary>
        public const string VisualName = "chestdisplay_visual";
        public const string IconName = "chestdisplay_icon";
        public const string CountName = "chestdisplay_count";

        /// <summary>Таблички в мире (без призрака при строительстве).</summary>
        public static readonly List<ChestSign> All = new List<ChestSign>();

        /// <summary>Последний показанный предмет (префаб и вариант) — в ZDO таблички, для тусклой иконки пустого сундука.</summary>
        private static readonly int s_lastItemKey = "chestdisplay_last_item".GetStableHashCode();
        private static readonly int s_lastVariantKey = "chestdisplay_last_variant".GetStableHashCode();

        private const float SearchInterval = 1f;
        private const float PollInterval = 2f;

        /// <summary>Сколько поисков подряд (раз в SearchInterval) за табличкой нет сундука, прежде чем она отвалится.</summary>
        private const int OrphanSearches = 8;

        private ZNetView m_nview;
        private WearNTear m_wear;
        private Transform m_visual;
        private Transform m_icon;
        private MeshFilter m_iconFilter;
        private MeshRenderer m_iconRenderer;
        private GameObject m_count;
        private MeshFilter m_countFilter;
        private MeshRenderer m_countRenderer;
        private bool m_ghost;

        private Container m_chest;
        private Inventory m_inventory;
        private WearNTear m_chestWear;
        private ItemDrop.ItemData m_item;
        private Sprite m_shown;
        private bool m_shownDim;
        private bool m_applied;
        private int m_shownCount = -1;
        private bool m_dirty;
        private float m_nextSearch;
        private float m_nextPoll;
        private int m_orphanSearches;

        private Face m_face = Face.PosZ;
        private bool m_lidPosed;

        /// <summary>Сундук, на котором висит табличка (null, пока не найден).</summary>
        public Container Chest => m_chest;

        /// <summary>Настройки вида поменялись (размер, количество) — пересчитать все таблички.</summary>
        public static void RefreshAll()
        {
            foreach (ChestSign sign in All)
            {
                if (sign != null && sign.m_chest != null)
                {
                    sign.RefreshLayout();
                    sign.m_dirty = true;
                }
            }
        }

        private void Awake()
        {
            // Призрак при строительстве создаётся с отключённой сетевой инициализацией.
            m_ghost = ZNetView.m_forceDisableInit;
            m_nview = GetComponent<ZNetView>();
            m_wear = GetComponent<WearNTear>();
            m_visual = transform.Find(VisualName);
            if (m_visual == null)
            {
                m_visual = transform;
            }
            m_icon = m_visual.Find(IconName);
            if (m_icon != null)
            {
                m_iconFilter = m_icon.GetComponent<MeshFilter>();
                m_iconRenderer = m_icon.GetComponent<MeshRenderer>();
            }
            Transform count = m_visual.Find(CountName);
            if (count != null)
            {
                m_count = count.gameObject;
                m_countFilter = count.GetComponent<MeshFilter>();
                m_countRenderer = count.GetComponent<MeshRenderer>();
            }
            Show(null, null);
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
                    Show(null, null);
                    ResetPose();
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
                Show(FirstItem(m_inventory), m_inventory);
            }
            UpdateLid();
        }

        /// <summary>
        /// За табличкой нет сундука: его разобрали или сломали, пока табличка не была с ним связана, или она висит на том,
        /// что сундуком не считается. Если местность вокруг загружена целиком, а сундука всё нет несколько проверок
        /// подряд — табличка отваливается, ресурсы выпадают. Решает владелец.
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

        /// <summary>Призрак при строительстве: показать, какой табличка будет на этом месте (иконка, число, размер).</summary>
        public void Preview(Container chest, float surfaceMin)
        {
            Inventory inventory = chest != null ? chest.GetInventory() : null;
            Show(FirstItem(inventory), inventory);
            ApplySize(chest != null ? ChestMount.SignSize(surfaceMin) : SignPiece.BoardSize);
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
            RefreshLayout();
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

        // ================================================================== размер и крышка

        /// <summary>На какой грани висит табличка и какого она размера (по SizePercent и этой поверхности).</summary>
        private void RefreshLayout()
        {
            ResetPose();
            if (m_chest != null && ChestMount.TryGetSurface(m_chest, transform, out Face face, out float surfaceMin))
            {
                m_face = face;
                ApplySize(ChestMount.SignSize(surfaceMin));
            }
            else
            {
                ApplySize(SignPiece.BoardSize);
            }
        }

        /// <summary>Табличка квадратная: стороны в плоскости таблички растут одинаково, толщина та же.</summary>
        private void ApplySize(float size)
        {
            if (m_visual == null || m_visual == transform)
            {
                return;
            }
            float k = size / SignPiece.BoardSize;
            var scale = new Vector3(k, k, 1f);
            if (m_visual.localScale != scale)
            {
                m_visual.localScale = scale;
            }
        }

        /// <summary>
        /// Табличка на крышке: сундук открыли — переносим её туда, где теперь крышка (та же матрица, что переводит
        /// закрытую крышку в открытую), закрыли — обратно. Не удалось понять, куда уехала крышка, — на время прячем.
        /// </summary>
        private void UpdateLid()
        {
            bool open = m_face == Face.Top && ChestMount.IsOpen(m_chest);
            if (open == m_lidPosed)
            {
                return;
            }
            if (!open)
            {
                ResetPose();
                return;
            }
            m_lidPosed = true;
            if (m_visual == null || m_visual == transform || !ChestMount.TryLidMotion(m_chest, out Matrix4x4 motion))
            {
                if (m_visual != null && m_visual != transform)
                {
                    m_visual.gameObject.SetActive(false);
                }
                return;
            }
            Matrix4x4 local = transform.worldToLocalMatrix * motion * transform.localToWorldMatrix;
            m_visual.localPosition = local.MultiplyPoint3x4(Vector3.zero);
            m_visual.localRotation = local.rotation;
        }

        private void ResetPose()
        {
            m_lidPosed = false;
            if (m_visual == null || m_visual == transform)
            {
                return;
            }
            m_visual.localPosition = Vector3.zero;
            m_visual.localRotation = Quaternion.identity;
            if (!m_visual.gameObject.activeSelf)
            {
                m_visual.gameObject.SetActive(true);
            }
        }

        // ================================================================== иконка и число

        /// <summary>
        /// Показать предмет сундука. inventory — сундук, на котором висит табличка (null — сундука нет, табличка пустая).
        /// Сундук есть, но пуст — тусклый последний предмет, который табличка запомнила, и число 0.
        /// </summary>
        private void Show(ItemDrop.ItemData item, Inventory inventory)
        {
            bool dim = false;
            int variant = item != null ? item.m_variant : 0;
            if (item != null)
            {
                Remember(item);
            }
            else if (inventory != null)
            {
                item = Remembered(out variant);
                dim = item != null;
            }
            m_item = item;
            Sprite icon = IconOf(item, variant);
            if (!m_applied || icon != m_shown || dim != m_shownDim)
            {
                m_applied = true;
                m_shown = icon;
                m_shownDim = dim;
                IconArt.Apply(m_iconFilter, m_iconRenderer, icon, dim);
            }
            ShowCount(icon != null && SignConfig.ShowCount.Value ? (dim ? 0 : Count(item, inventory)) : -1);
        }

        /// <summary>Запомнить показанный предмет в ZDO таблички (пишет только её владелец — и только если предмет сменился).</summary>
        private void Remember(ItemDrop.ItemData item)
        {
            ZDO zdo = m_nview != null && m_nview.IsValid() && m_nview.IsOwner() ? m_nview.GetZDO() : null;
            string name = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
            if (zdo == null || string.IsNullOrEmpty(name))
            {
                return;
            }
            if (zdo.GetString(s_lastItemKey) != name)
            {
                zdo.Set(s_lastItemKey, name);
            }
            if (zdo.GetInt(s_lastVariantKey) != item.m_variant)
            {
                zdo.Set(s_lastVariantKey, item.m_variant);
            }
        }

        /// <summary>Запомненный предмет — данные его префаба (иконки, название) и вариант, или null.</summary>
        private ItemDrop.ItemData Remembered(out int variant)
        {
            variant = 0;
            ZDO zdo = m_nview != null && m_nview.IsValid() ? m_nview.GetZDO() : null;
            string name = zdo != null ? zdo.GetString(s_lastItemKey) : null;
            if (string.IsNullOrEmpty(name) || ObjectDB.instance == null)
            {
                return null;
            }
            GameObject prefab = ObjectDB.instance.GetItemPrefab(name);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return null; // предмет из удалённого мода
            }
            variant = zdo.GetInt(s_lastVariantKey);
            return drop.m_itemData;
        }

        /// <summary>Сколько этого предмета в сундуке — все стопки вместе (как для рецептов, но без отбора по уровню мира).</summary>
        private static int Count(ItemDrop.ItemData item, Inventory inventory) =>
            inventory != null ? inventory.CountItems(item.m_shared.m_name, -1, false) : item.m_stack;

        /// <summary>
        /// Число внизу доски; меньше нуля — спрятать. С числом иконка чуть меньше и выше, чтобы не наезжать на него.
        /// </summary>
        private void ShowCount(int count)
        {
            if (m_count == null)
            {
                return;
            }
            bool on = count >= 0;
            if (m_count.activeSelf != on)
            {
                m_count.SetActive(on);
                if (m_icon != null)
                {
                    m_icon.localScale = Vector3.one * (on ? SignPiece.IconScaleWithCount : 1f);
                    m_icon.localPosition = new Vector3(0f, on ? SignPiece.IconShiftWithCount : 0f, SignPiece.FrontZ);
                }
            }
            if (on && count != m_shownCount)
            {
                NumberArt.Apply(m_countFilter, m_countRenderer, DisplayRules.FormatCount(count));
            }
            m_shownCount = on ? count : -1;
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

        private static Sprite IconOf(ItemDrop.ItemData item, int variant)
        {
            Sprite[] icons = item?.m_shared?.m_icons;
            if (icons == null || icons.Length == 0)
            {
                return null;
            }
            return icons[variant >= 0 && variant < icons.Length ? variant : 0];
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
