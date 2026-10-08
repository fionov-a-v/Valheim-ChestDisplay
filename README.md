# Табличка для сундука (Chest Display) — мод для Valheim

Автор: Fionov Alexander · исходники: https://github.com/fionov-a-v/Valheim-ChestDisplay · [English below](#english)

Небольшая деревянная табличка, которая крепится к общему сундуку и показывает иконку того, что в нём лежит первым
(предмет в верхней левой занятой ячейке), а если включить — и сколько его там. Поменяли содержимое — табличка
обновилась. Сделано для Valheim 1.0.17 на BepInEx 5 и Jötunn 2.30. Интерфейс и описание — на русском и английском (по языку игры).

## Установка

Нужны [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) и
[Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/). Через r2modman/Thunderstore Mod Manager —
установить ChestDisplay, зависимости поставятся сами. Вручную — положить `ChestDisplay.dll` в `BepInEx/plugins`.

Мод нужен **всем** игрокам и серверу (у кого его нет — Jötunn не пустит на сервер). Настройки (рецепт, станок)
приходят с сервера, менять их может только администратор.

## Постройка

Молот → «Мебель» → **Табличка для сундука**, рядом с верстаком.

| Стоимость | |
|---|---|
| 1 × Глаз грейдворфа | 2 × Древесина |
| 2 × Уголь | 2 × Смола |

## Как пользоваться

1. Возьмите табличку в молот и наведитесь на **общий** сундук. Призрак таблички появляется только на подходящем
   сундуке: табличка сама встанет по центру той боковой грани, которая обращена к вам (ниже крышки), и прижмётся к ней.
   Наведитесь на крышку сверху — табличка ляжет на крышку, повёрнутая так, чтобы читаться с вашего места. Пока вы
   примеряетесь, на призраке уже видна иконка того, что лежит в сундуке.
2. Поставьте. На табличке — иконка предмета из верхней левой занятой ячейки сундука. Опустел сундук — табличка
   показывает последний предмет тусклым, выцветшим в цвет дерева (и «0», если число включено): видно, что здесь
   хранится, но сейчас этого нет. Табличка помнит предмет и после перезахода, у всех игроков; на только что
   поставленной табличке у пустого сундука — пусто.
3. Иконка обновляется сама: сразу, когда вы перекладываете вещи, и в пределах секунды, когда содержимое поменял другой
   игрок.
4. **«Использовать» на табличке открывает сундук** — табличка не мешает доставать вещи. При наведении видна обычная
   подсказка сундука и строка с названием предмета на табличке. Табличка на крышке при открытии сундука уезжает вместе
   с крышкой.

Размер таблички задаёт настройка `SizePercent`: 0 % — стандартные 34 см, 100 % — во всю меньшую сторону поверхности,
на которой она висит (боковины ниже крышки или верха крышки), по умолчанию — 50 %, посередине; табличка всегда
квадратная. Если включить
`ShowItemCount`, внизу таблички появится число — сколько этого предмета в сундуке (все стопки вместе): светлые цифры
с тёмной обводкой, как в интерфейсе игры; иконка тогда чуть меньше и выше. Большие числа сокращаются, чтобы всегда
помещаться: до 999 — как есть, дальше 1.2k, 12k, а больше 99 999 — 99k+.

Куда можно повесить:

- только на **общие** сундуки: «Сундук», «Армированный сундук», «Сундук из черного металла» и сундуки других модов;
- **не** на «Личный сундук», «Бочку», «Шкаф», станки со встроенным хранилищем (например, бродильню и плавильню
  из BetterStations), тележки и корабли (табличка не поехала бы вместе с ними) и не на землю, стены и т.п.;
- на каждую боковую грань и на крышку — по одной табличке (на сундук — до пяти).

Если навестись не на подходящий сундук, призрака таблички нет; на грани, где табличка уже есть, он красный.
При попытке поставить — сообщение с причиной. Разбирается табличка молотом, как любая постройка (ресурсы возвращаются).
Разобрали или сломали сундук — табличка снимается вместе с ним, её ресурсы выпадают рядом. Табличка, за которой
сундука нет вовсе, через несколько секунд отваливается сама — тоже с выпадением ресурсов.

Иконка нарисована тем же шейдером, что и постройки: ночью она темнеет вместе с доской, у огня — освещена, а не светится
сама.

С модом [«Распределяющий сундук»](https://github.com/fionov-a-v/Valheim-SortingChest) таблички удобно сочетаются: он
раскладывает вещи по первому предмету сундука, а табличка как раз показывает этот предмет.

## Настройки

Файл `BepInEx/config/chestdisplay.cfg` (или F1 — Configuration Manager). Правка файла во время игры применяется сразу.

| Раздел | Настройка | По умолчанию | Что делает |
|---|---|---|---|
| 1 - Recipe | `Enabled` | true | Можно ли строить |
| | `Cost` | `GreydwarfEye:1,Wood:2,Coal:2,Resin:2` | Стоимость (Предмет:Кол-во через запятую) |
| | `CraftingStation` | `piece_workbench` | Станок, рядом с которым строится (пусто — без станка) |
| 2 - Sign | `SizePercent` | 50 | Размер, %: 0 — стандартный (34 см), 100 — во всю меньшую сторону поверхности |
| 3 - Client | `ShowItemCount` | false | Показывать на табличке количество предмета в сундуке |

Разделы 1–2 синхронизируются с сервера, раздел 3 — у каждого игрока свой. В панели постройки у молота 6 ячеек
на ресурсы и станок вместе: станок помещается, только если ресурсов 5 или меньше. Если в `Cost` больше 5 предметов, табличка строится без станка
(в логе будет предупреждение) — иначе панель игры сломалась бы.

## Сборка из исходников

Нужен .NET SDK 8. Путь к игре задан в `ChestDisplay.csproj` (`ValheimDir`, по умолчанию `~/ValheimWin` — ссылка
на папку игры на Windows-машине, открытую по SMB). Другой путь: `dotnet build -c Release -p:ValheimDir=/путь/к/Valheim`.

```
dotnet build -c Release
```

После сборки DLL сама копируется в `BepInEx/plugins/ChestDisplay.dll` (собрать без копирования:
`-p:SkipDeploy=true`). Если в этот момент игра запущена, Windows держит файл занятым — будет только предупреждение,
скопируйте после выхода из игры. Архив для Thunderstore — `./package.sh` (появится в `dist/`).

Тесты правил таблички (первый предмет, выбор грани, размер, число, вписывание иконки, рецепт):

```
dotnet run -c Release --project tests/LogicTests
```

С папкой (`-- out`) тесты ещё выгружают атлас цифр и готовые числа в PNG — по ним `tests/sign_preview.py` собирает
превью таблички спереди (иконка, число, табличка во всю боковину), чтобы проверить вид без запуска игры:
`python3 tests/sign_preview.py out <папка с текстурами игры или -> preview.png`.

Иконка (`package/icon.png`, 256×256) и баннер для страницы мода (`media/banner.png`, 1300×372) рисуются кодом:
`python3 tests/icon.py package/icon.png` и та же команда с `media/banner.png --banner`.

## Лицензия

[PolyForm Noncommercial License 1.0.0](LICENSE.md). Кратко (полный текст — в `LICENSE.md`):

- мод можно бесплатно использовать, изменять и распространять, в том числе переделки;
- продавать мод или использовать его в коммерческих целях нельзя;
- при любой передаче мода или его переделки нужно приложить текст лицензии (или ссылку на него) и сохранить строку:

```
Required Notice: Copyright Fionov Alexander (https://github.com/fionov-a-v/Valheim-ChestDisplay)
```

---

<a id="english"></a>
# Chest Display — mod for Valheim

A small wooden sign that you fix to a shared chest; it shows the icon of the first item inside (the top-left occupied
slot), optionally with how many of it there are, and updates when the contents change. Made for Valheim 1.0.17 with
BepInEx 5 and Jötunn 2.30. English and Russian in-game text.

**Install:** needs BepInExPack Valheim and Jötunn. Every player and the server must have the mod; settings are synced
from the server and only admins can change them.

**Build:** Hammer → Furniture → **Chest Sign**, next to a Workbench. Cost: 1 Greydwarf Eye, 2 Wood, 2 Coal, 2 Resin.

**Use:** aim at a **shared** chest with the sign selected: it snaps to the center of the chest side facing you (below the
lid) and sits flush against it; aim at the lid from above and it lies on the lid, turned to read from where you stand.
The ghost already shows what the sign will display. Once placed, the sign
shows the icon of the item in the top-left occupied slot. When the chest is emptied, the sign keeps showing the last item,
dim and faded into the wood (and "0" if the count is on), so you still see what belongs there; it remembers the item
across relogs and for all players. It updates immediately when you
move items, and within a second when another player changes the chest. **Using the sign opens the chest**, and hovering
it shows the chest's usual tooltip plus the name of the item on the sign. A sign on the lid moves with the lid when the
chest is opened. `SizePercent` sets the size: 0% — the standard 34 cm, 100% — the full smaller side of the surface it
hangs on (the chest side below the lid, or the lid top), 50% by default; the sign stays square. `ShowItemCount` adds a number at the
bottom of the sign — how many of that item the chest holds (all stacks together), light digits with a dark outline like
the game's UI; large numbers are shortened to fit (999, 1.2k, 12k, 99k+).

Signs go only on shared chests (Chest, Reinforced Chest, Black Metal Chest and modded chests) — not on the Personal
Chest, Barrel, Wardrobe, stations with built-in storage (e.g. the BetterStations fermenter and smelter), carts or ships,
and one per chest side and one on the lid. The ghost only appears on a suitable chest (on an occupied side it is red); trying to place it
elsewhere gives a message saying why. Remove a sign with the hammer like any piece; if the chest is removed or destroyed,
its signs come off with it and drop their materials; a sign with no chest behind it at all falls off by itself after a
few seconds. The icon uses the same shader as building pieces, so it is lit like the board, not glowing at night.

Pairs well with [Sorting Chest](https://github.com/fionov-a-v/Valheim-SortingChest), which sorts items by each chest's
first item — exactly what the sign shows.

**Config** (`BepInEx/config/chestdisplay.cfg`, or F1 Configuration Manager): `Enabled`, `Cost` (Item:Amount separated by
commas), `CraftingStation` (empty — no station needed), `SizePercent` (0–100, default 50) — synced from the server; `ShowItemCount`
(default off) — per client. The build panel has 6 slots
for ingredients and the station together, so with more than 5 ingredients the station is dropped (with a warning in the
log).

**Source:** https://github.com/fionov-a-v/Valheim-ChestDisplay

**License:** [PolyForm Noncommercial License 1.0.0](LICENSE.md) — free to use, modify and share, no commercial use;
keep the line `Required Notice: Copyright Fionov Alexander (https://github.com/fionov-a-v/Valheim-ChestDisplay)` when redistributing.

## Changelog

- **1.2.0** — an emptied chest's sign keeps showing the last item, dimmed (faded into the wood), with "0" if the count
  is on; the sign remembers it across relogs and for all players.
- **1.1.0** — Valheim 1.0.17. Signs can also lie on top of the lid (and move with it when the chest opens). New settings:
  `SizePercent` (sign size up to the whole chest side or lid) and `ShowItemCount` (item count on the sign, off by default).
  Side signs are centered on the part of the side below the lid.
- **1.0.0** — first release.
