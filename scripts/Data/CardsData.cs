using System.Collections.Generic;
using System.Linq;

namespace CardGame;

/// <summary>
/// Справочник всех карт в игре.
/// Чтобы добавить карту, допиши новый блок в список All.
/// </summary>
public static class CardsData
{
	// Временная картинка для всех карт, пока нет настоящих рисунков.
	private const string YellowSquare = "res://art/cards/yellow_square.png";

	public static readonly IReadOnlyList<CardData> All =
	[
		new CardData
		{
			Id = "prizuvnic",
			Name = "Призывник",
			ImagePath = YellowSquare,
			Health = 3,
			Attack = 1,
			Defense = 1,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Human, CardTag.Warrior, CardTag.Soldier],
		},
		new CardData
		{
			Id = "golem",
			Name = "Голем",
			ImagePath = YellowSquare,
			Health = 12,
			Attack = 5,
			Defense = 7,
			Block = 1,
			Types = [CardType.Creature],
			Tags = [CardTag.Golem],
			Cost = [new ManaCost(1, ManaType.Grass)],
		},
		new CardData
		{
			Id = "lightning_strike",
			Name = "Удар молнии",
			ImagePath = YellowSquare,
			Types = [CardType.Spell],
			Cost = [new ManaCost(1, ManaType.Fire)],
			TargetsCreature = true,
			AbilityIds = ["ability_lightning"],
		},
		new CardData
		{
			Id = "club",
			Name = "Дубина",
			ImagePath = YellowSquare,
			Types = [CardType.Equipment],
			LaborCost = 2,
			AbilityIds = ["ability_club"],
		},
		new CardData
		{
			Id = "strangle",
			Name = "Удушение",
			ImagePath = YellowSquare,
			Types = [CardType.Enchantment],
			Cost = [new ManaCost(1, ManaType.Darkness)],
			AbilityIds = ["ability_strangle"],
		},
		new CardData
		{
			Id = "manuscripts",
			Name = "Манускрипты",
			ImagePath = YellowSquare,
			Types = [CardType.Advantage],
			Cost = [new ManaCost(1, ManaType.Gold)],
			AbilityIds = ["ability_manuscripts"],
		},
		// ───────── Существа (набор 2) ─────────
		// Характеристики в порядке, как на карте: атака, защита, здоровье, блок.
		new CardData
		{
			Id = "zombie",
			Name = "Зомби",
			ImagePath = YellowSquare,
			Attack = 4,
			Defense = 4,
			Health = 10,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Squad, CardTag.Undead],
			Cost = [new ManaCost(3, ManaType.Darkness)],
			ExileDiscountMax = 2,
			AbilityIds = ["ability_zombie_exile"],
		},
		new CardData
		{
			Id = "peasant",
			Name = "Крестьянка",
			ImagePath = YellowSquare,
			Attack = 2,
			Defense = 1,
			Health = 4,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Human],
			Cost = [new ManaCost(2, ManaType.Darkness)],
			AlternativeCosts = [new CostOption { Mana = [new ManaCost(2, ManaType.Food)] }],
			AbilityIds = ["ability_shot_peasant", "ability_peasant_meat"],
		},
		new CardData
		{
			Id = "archer",
			Name = "Лучник",
			ImagePath = YellowSquare,
			Attack = 2,
			Defense = 1,
			Health = 6,
			Block = 1,
			Types = [CardType.Creature],
			Tags = [CardTag.Human, CardTag.Warrior, CardTag.Soldier],
			Cost = [new ManaCost(2, ManaType.Gold)],
			AbilityIds = ["ability_shot_archer"],
		},
		new CardData
		{
			Id = "raccoon",
			Name = "Енот грабитель",
			ImagePath = YellowSquare,
			Attack = 3,
			Defense = 0,
			Health = 3,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Beast],
			Cost = [new ManaCost(1, ManaType.Gold)],
			AlternativeCosts = [new CostOption { Mana = [new ManaCost(2, ManaType.Food)] }],
			FreeAfterDeathThisTurn = true,
			AbilityIds = ["ability_first_strike", "ability_flash", "ability_raccoon_free"],
		},
		new CardData
		{
			Id = "bandit",
			Name = "Бандит",
			ImagePath = YellowSquare,
			Attack = 4,
			Defense = 2,
			Health = 4,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Human, CardTag.Warrior],
			Cost = [new ManaCost(1, ManaType.Gold, ManaType.Food)],
			AbilityIds = [AbilitiesData.RetreatId],
		},
		new CardData
		{
			Id = "assassin",
			Name = "Ассасин",
			ImagePath = YellowSquare,
			Attack = 5,
			Defense = 3,
			Health = 4,
			Block = 2,
			Types = [CardType.Creature],
			Tags = [CardTag.Human, CardTag.Warrior, CardTag.Soldier],
			Cost = [new ManaCost(2, ManaType.Gold)],
			AlternativeCosts = [new CostOption { Mana = [new ManaCost(1, ManaType.Faith)] }],
			AbilityIds = [AbilitiesData.RetreatId, "ability_first_strike"],
		},
		new CardData
		{
			Id = "fari",
			Name = "Фари",
			ImagePath = YellowSquare,
			Attack = 2,
			Defense = 1,
			Health = 2,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Fairy],
			Cost = [new ManaCost(1, ManaType.Gold, ManaType.Grass)],
			AlternativeCosts = [new CostOption { Discard = 1 }],
			AbilityIds = ["ability_fari_discard", "ability_fari_stun"],
		},
		new CardData
		{
			Id = "hiva",
			Name = "Хива",
			ImagePath = YellowSquare,
			Attack = 0,
			Defense = 1,
			Health = 3,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Fairy],
			Cost = [new ManaCost(1, ManaType.Light)],
			AbilityIds = ["ability_hiva"],
		},
		new CardData
		{
			Id = "lamplighter",
			Name = "Фонарщик",
			ImagePath = YellowSquare,
			Attack = 1,
			Defense = 1,
			Health = 5,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Fairy],
			Cost = [new ManaCost(1, ManaType.Light)],
			AbilityIds = ["ability_lamplighter"],
		},

		// ───────── Заклинания и чары (набор 2) ─────────
		new CardData
		{
			Id = "drink_any_cost",
			Name = "Выпить любой ценой",
			ImagePath = YellowSquare,
			Types = [CardType.Spell],
			XCost = true,
			AbilityIds = ["ability_drink"],
		},
		new CardData
		{
			Id = "pyro_trick",
			Name = "Трюк пиромана",
			ImagePath = YellowSquare,
			Types = [CardType.Spell],
			TargetsCreature = true,
			DiscardCost = 1,
			ReturnsToDeck = true,
			AbilityIds = ["ability_pyro"],
		},
		new CardData
		{
			Id = "serene_vision",
			Name = "Безмятежное видение",
			ImagePath = YellowSquare,
			Types = [CardType.Enchantment],
			AbilityIds = ["ability_serene_vision"],
		},
		new CardData
		{
			Id = "addiction",
			Name = "Зависимость",
			ImagePath = YellowSquare,
			Types = [CardType.Enchantment],
			Cost = [new ManaCost(1, ManaType.Grass)],
			AlternativeCosts = [new CostOption { Labor = 2 }],
			AbilityIds = ["ability_addiction"],
		},

		// ───────── Набор 3: элементали ─────────
		new CardData
		{
			Id = "fire_elemental",
			Name = "Огненный Элементаль",
			ImagePath = YellowSquare,
			Attack = 5,
			Defense = 5,
			Health = 10,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Elemental],
			Cost = [new ManaCost(3, ManaType.Fire)],
			AbilityIds = ["ability_shot_fire_elemental"],
		},
		new CardData
		{
			Id = "water_elemental",
			Name = "Водный Элементаль",
			ImagePath = YellowSquare,
			Attack = 4,
			Defense = 4,
			Health = 16,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Elemental],
			Cost = [new ManaCost(3, ManaType.Water)],
			AbilityIds = ["ability_water_elemental"],
		},
		new CardData
		{
			Id = "earth_elemental",
			Name = "Земляной Элементаль",
			ImagePath = YellowSquare,
			Attack = 3,
			Defense = 6,
			Health = 16,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Elemental],
			Cost = [new ManaCost(3, ManaType.Grass)],
			AbilityIds = ["ability_earth_elemental"],
		},
		new CardData
		{
			Id = "air_elemental",
			Name = "Воздушный Элементаль",
			ImagePath = YellowSquare,
			Attack = 3,
			Defense = 2,
			Health = 8,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Elemental],
			Cost = [new ManaCost(3, ManaType.Light)],
			AbilityIds = ["ability_air_elemental", "ability_flight"],
		},

		// ───────── Набор 3: остальные ─────────
		new CardData
		{
			Id = "legion",
			Name = "Легион",
			ImagePath = YellowSquare,
			Attack = 2,
			Defense = 2,
			Health = 2,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Spirit, CardTag.Demon, CardTag.Unclean],
			Cost = [new ManaCost(1, ManaType.Darkness)],
			EnchantmentFormId = "legion_enchantment",
			AbilityIds = ["ability_legion_copy", "ability_legion_as_enchantment"],
		},
		new CardData
		{
			// Легион, разыгранный как чары. В колоды не попадает (InDraft = false), в сброс уходит как «legion».
			Id = "legion_enchantment",
			Name = "Легион (чары)",
			ImagePath = YellowSquare,
			Types = [CardType.Enchantment],
			Tags = [CardTag.Spirit, CardTag.Demon, CardTag.Unclean],
			Cost = [new ManaCost(2, ManaType.Darkness)],
			DiscardAsId = "legion",
			InDraft = false,
			AbilityIds = ["ability_legion_enchantment"],
		},
		new CardData
		{
			Id = "eternal_night_shadow",
			Name = "Тень вечной ночи",
			ImagePath = YellowSquare,
			Attack = 0,
			Defense = 0,
			Health = 1,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Spirit],
			Cost = [new ManaCost(1, ManaType.Darkness)],
			AbilityIds = ["ability_shadow_undying", "ability_shadow_return"],
		},
		new CardData
		{
			Id = "strange_animal",
			Name = "Необыкновенное животное",
			ImagePath = YellowSquare,
			Attack = 0,
			Defense = 0,
			Health = 1,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Beast, CardTag.Artifact],
			AbilityIds = ["ability_strange_animal"],
		},
		new CardData
		{
			Id = "nastiness",
			Name = "Гадство",
			ImagePath = YellowSquare,
			Types = [CardType.Spell],
			Cost = [new ManaCost(2, ManaType.Darkness)],
			AlternativeCosts = [new CostOption { Mana = [new ManaCost(3, ManaTypeExtensions.AllTypes)] }],
			AbilityIds = ["ability_nastiness"],
		},
		new CardData
		{
			Id = "multi_gaze",
			Name = "Множественный взор",
			ImagePath = YellowSquare,
			Types = [CardType.Advantage],
			Cost = [new ManaCost(2, ManaType.Darkness)],
			AbilityIds = ["ability_multi_gaze"],
		},
		new CardData
		{
			Id = "graveyard",
			Name = "Кладбище",
			ImagePath = YellowSquare,
			Types = [CardType.Advantage],
			LaborCost = 2,
			AbilityIds = ["ability_graveyard_collect", "ability_graveyard_harvest"],
		},

		// ───────── Набор 4: тьма и скавены ─────────
		new CardData
		{
			Id = "shadow_beast",
			Name = "Теневая тварь",
			ImagePath = YellowSquare,
			Attack = 4,
			Defense = 4,
			Health = 6,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Beast, CardTag.Demon, CardTag.Unclean],
			Cost = [new ManaCost(2, ManaType.Darkness)],
			AbilityIds = ["ability_shadow_beast_feed", "ability_shadow_beast_grow"],
		},
		new CardData
		{
			Id = "skaven_slave",
			Name = "Скавенский раб",
			ImagePath = YellowSquare,
			Attack = 4,
			Defense = 3,
			Health = 4,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Skaven],
			Cost = [new ManaCost(1, ManaType.Food)],
			AbilityIds = [AbilitiesData.RetreatId, "ability_hunger"],
		},
		new CardData
		{
			Id = "plague_monk",
			Name = "Чумной монах",
			ImagePath = YellowSquare,
			Attack = 4,
			Defense = 3,
			Health = 6,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Skaven],
			Cost = [new ManaCost(2, ManaType.Food)],
			AbilityIds = [AbilitiesData.RetreatId, "ability_plague_monk", "ability_hunger"],
		},
		new CardData
		{
			Id = "skaven_assassin",
			Name = "Скавенский Ассасин",
			ImagePath = YellowSquare,
			Attack = 6,
			Defense = 3,
			Health = 6,
			Block = 2,
			Types = [CardType.Creature],
			Tags = [CardTag.Skaven],
			Cost = [new ManaCost(2, ManaType.Food)],
			AbilityIds = [AbilitiesData.RetreatId, "ability_first_strike", "ability_skaven_assassin_raid", "ability_hunger"],
		},
		new CardData
		{
			Id = "grey_seer",
			Name = "Серый провидец",
			ImagePath = YellowSquare,
			Attack = 2,
			Defense = 2,
			Health = 8,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Skaven],
			Cost = [new ManaCost(4, ManaType.Food, ManaType.Darkness)],
			AbilityIds = ["ability_seer_rally", "ability_seer_rats", "ability_shot_seer", "ability_seer_poison"],
		},
		new CardData
		{
			// Жетон: создаёт Серый провидец. В драфт не попадает, при смерти исчезает.
			Id = "rat",
			Name = "Крыса",
			ImagePath = YellowSquare,
			Attack = 1,
			Defense = 1,
			Health = 1,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Beast],
			InDraft = false,
			IsToken = true,
		},

		// ───────── Набор 4: люди веры ─────────
		new CardData
		{
			Id = "preacher",
			Name = "Проповедник",
			ImagePath = YellowSquare,
			Attack = 1,
			Defense = 1,
			Health = 4,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Human, CardTag.Priest],
			Cost = [new ManaCost(1, ManaType.Faith, ManaType.Gold)],
			AbilityIds = ["ability_preacher"],
		},
		new CardData
		{
			Id = "enlightened",
			Name = "Просветлённый",
			ImagePath = YellowSquare,
			Attack = 1,
			Defense = 1,
			Health = 4,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Human],
			Cost = [new ManaCost(1, ManaType.Faith, ManaType.Food)],
			AbilityIds = ["ability_enlightened"],
		},
		new CardData
		{
			Id = "diligent",
			Name = "Усердный",
			ImagePath = YellowSquare,
			Attack = 1,
			Defense = 1,
			Health = 4,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Human],
			Cost = [new ManaCost(1, ManaType.Faith, ManaType.Food)],
			AbilityIds = ["ability_diligent"],
		},
		new CardData
		{
			Id = "miracle_worker",
			Name = "Чудотворка",
			ImagePath = YellowSquare,
			Attack = 1,
			Defense = 1,
			Health = 4,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Human, CardTag.Priest],
			Cost = [new ManaCost(2, ManaType.Faith)],
			AbilityIds = ["ability_miracle"],
		},
		new CardData
		{
			Id = "fanatics",
			Name = "Фанатики",
			ImagePath = YellowSquare,
			Attack = 4,
			Defense = 4,
			Health = 8,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Human, CardTag.Warrior],
			Cost = [new ManaCost(2, ManaType.Faith)],
			AbilityIds = ["ability_fanatics"],
		},
		new CardData
		{
			Id = "ascetic",
			Name = "Аскет",
			ImagePath = YellowSquare,
			Attack = 0,
			Defense = 0,
			Health = 4,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Human],
			AbilityIds = ["ability_ascetic"],
		},
		new CardData
		{
			Id = "elementalist",
			Name = "Элементалист",
			ImagePath = YellowSquare,
			Attack = 2,
			Defense = 2,
			Health = 4,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Human, CardTag.Priest],
			Cost = [new ManaCost(2, ManaTypeExtensions.AllTypes)],
			AbilityIds = ["ability_elementalist_burn", "ability_elementalist_summon"],
		},

		// ───────── Герои ─────────
		// Характеристики в порядке, как на карте: атака, защита, здоровье, блок.
		new CardData
		{
			Id = "hero_ramor",
			Name = "Рэмор",
			ImagePath = YellowSquare,
			Attack = 6,
			Defense = 4,
			Health = 10,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Demon],
			IsHero = true,
			Cost = [new ManaCost(6, ManaType.Darkness)],
			AbilityIds = ["ability_ramor_dark", "ability_ramor_discard", "ability_ramor_move", "ability_flight"],
		},
		new CardData
		{
			Id = "hero_iznanka",
			Name = "Изнанка",
			ImagePath = YellowSquare,
			Attack = 8,
			Defense = 4,
			Health = 8,
			Block = 0,
			Types = [CardType.Creature],
			Tags = [CardTag.Elemental],
			IsHero = true,
			Cost = [new ManaCost(6, ManaTypeExtensions.ElementalTypes)],
			AbilityIds = ["ability_iznanka_second_line", "ability_iznanka_no_defense", "ability_multi_target", "ability_speed"],
		},
		new CardData
		{
			Id = "hero_shikimaru",
			Name = "Шикимару",
			ImagePath = YellowSquare,
			Attack = 3,
			Defense = 3,
			Health = 8,
			Block = 2,
			Types = [CardType.Creature],
			Tags = [CardTag.Human, CardTag.Warrior],
			IsHero = true,
			Cost = [new ManaCost(4, ManaTypeExtensions.AllTypes)],
			AbilityIds = ["ability_shikimaru_draw", "ability_shikimaru_immobilize", "ability_laziness_1_any"],
		},
		new CardData
		{
			Id = "hero_baron",
			Name = "Барон Суббота",
			ImagePath = YellowSquare,
			Attack = 0,
			Defense = 7,
			Health = 16,
			Block = 1,
			Types = [CardType.Creature],
			Tags = [CardTag.Loa],
			IsHero = true,
			Cost = [new ManaCost(6, ManaType.Darkness)],
			AbilityIds = ["ability_baron_draw", "ability_baron_destroy", "ability_baron_control"],
		},
		// Карты оплаты: по одной на каждый основной тип маны (payment_fire и т.д.).
		// У них задан PaymentManaType, поэтому BuildDecks не берёт их в общий пул.
		.. ManaTypeExtensions.MainTypes.Select(PaymentCard),
	];

	// Словарь для быстрого поиска по Id. Строится один раз из списка All.
	private static readonly Dictionary<string, CardData> ById = All.ToDictionary(c => c.Id);

	public static CardData Get(string id) => ById[id];

	/// <summary>Карта оплаты для типа маны или null, если её нет (например, для веры).</summary>
	public static CardData FindPayment(ManaType type) => All.FirstOrDefault(c => c.PaymentManaType == type);

	private static CardData PaymentCard(ManaType type) => new()
	{
		Id = $"payment_{type.ToString().ToLowerInvariant()}",
		Name = $"Оплата: {type.ToRussian()}",
		ImagePath = YellowSquare,
		Types = [CardType.Payment],
		PaymentManaType = type,
		AbilityIds = [AbilitiesData.GainManaId(type)],
	};
}
