using System.Collections.Generic;
using System.Linq;

namespace CardGame;

/// <summary>
/// Справочник всех способностей в игре.
/// Чтобы добавить способность, допиши новый блок в список All.
/// </summary>
public static class AbilitiesData
{
	/// <summary>Сколько маны даёт одна карта оплаты.</summary>
	private const int PaymentManaAmount = 2;

	// Id, которые бой проверяет напрямую. Константа вместо строки в двух местах:
	// опечатку в имени константы найдёт компилятор.
	public const string RetreatId = "ability_retreat";

	public static readonly IReadOnlyList<AbilityData> All =
	[
		new AbilityData
		{
			Id = RetreatId,
			Name = "Отступление",
			Description = "Когда на существо нападают, в начале схватки оно вместо боя отходит на 1 линию назад. "
				+ "Не срабатывает, если существо защищало другое.",
			// Логика в TestBattle.ResolveFights: срабатывает в бою, а не при розыгрыше.
		},
		new AbilityData
		{
			Id = "ability_lightning",
			Name = "Удар молнии",
			Description = "Нанесите 2 единицы урона любому существу и поверните его.",
			OnPlayed = ctx =>
			{
				ctx.Battle.DealDamage(ctx.Target, 2);
				ctx.Target.SetTapped(true);
			},
		},
		new AbilityData
		{
			Id = "ability_club",
			Name = "Дубина",
			Description = "Атака снаряжённого существа +1.",
			AttackBonus = 1,
		},
		new AbilityData
		{
			Id = "ability_strangle",
			Name = "Удушение",
			Description = "В фазу трофеев уменьшите здоровье прикреплённого существа на 1.",
			OnTrophyPhase = ctx => ctx.Battle.DealDamage(ctx.Target, 1),
		},
		new AbilityData
		{
			Id = "ability_manuscripts",
			Name = "Манускрипты",
			Description = "2 труда: поверните эту карту и получите 1 веру.",
			ActivationLaborCost = 2,
			ActivationTapsSelf = true,
			OnActivated = ctx => ctx.Player.AddMana(ManaType.Faith, 1),
		},
		// ───────── Свойства из одного слова (общие для разных карт) ─────────
		new AbilityData
		{
			Id = "ability_flight",
			Name = Glossary.Flight,
			Description = "Может нападать на 1 линию дальше.",
			GlossaryTerms = [Glossary.Flight],
			Flight = true,
		},
		new AbilityData
		{
			Id = "ability_speed",
			Name = Glossary.Speed,
			Description = "Может нападать на 1 линию дальше.",
			GlossaryTerms = [Glossary.Speed],
			Speed = true,
		},
		new AbilityData
		{
			Id = "ability_laziness_1_any",
			Name = $"{Glossary.Laziness} (1 любая)",
			Description = "Чтобы напасть, заплатите 1 ману любого типа.",
			GlossaryTerms = [Glossary.Laziness],
			LazinessCost = [new ManaCost(1, ManaTypeExtensions.AllTypes)],
		},

		// ───────── Рэмор ─────────
		new AbilityData
		{
			Id = "ability_ramor_dark",
			Name = "Тёмный дар",
			Description = "Поворот: получите 2 маны тьмы.",
			ActivationTapsSelf = true,
			OnActivated = ctx => ctx.Player.AddMana(ManaType.Darkness, 2),
		},
		new AbilityData
		{
			Id = "ability_ramor_discard",
			Name = "Искушение",
			Description = "1 любая мана: противник сбрасывает случайную карту из руки.",
			ActivationCost = [new ManaCost(1, ManaTypeExtensions.AllTypes)],
			UsableFromDiscard = true,
			OnActivated = ctx => ctx.Battle.DiscardRandomFromHand(ctx.Battle.OpponentOf(ctx.Player)),
		},
		new AbilityData
		{
			Id = "ability_ramor_move",
			Name = "Тень",
			Description = "2 тьмы: переместите Рэмора на любую линию.",
			ActivationCost = [new ManaCost(2, ManaType.Darkness)],
			ActivationTarget = ActivationTarget.AnyLane,
			OnActivated = ctx => ctx.Battle.MoveCreature(ctx.Source, ctx.TargetLane),
		},

		// ───────── Изнанка ─────────
		new AbilityData
		{
			Id = "ability_iznanka_second_line",
			Name = "Разлом",
			Description = "Поворот: до конца хода вы можете разыгрывать существ на второй линии.",
			ActivationTapsSelf = true,
			OnActivated = ctx => ctx.Player.CanPlayOnSecondLine = true,
		},
		new AbilityData
		{
			Id = "ability_iznanka_no_defense",
			Name = "Неуязвимость в атаке",
			Description = "Когда это существо нападает, оно не получает урона от защиты цели.",
			IgnoresDefenseWhenAttacking = true,
		},
		new AbilityData
		{
			Id = "ability_multi_target",
			Name = "Много целей",
			Description = "Может выбрать несколько существ целями нападения. Урон делится между ними поровну, с округлением вниз.",
			MultiTarget = true,
		},

		// ───────── Шикимару ─────────
		new AbilityData
		{
			Id = "ability_shikimaru_draw",
			Name = "Разведка",
			Description = "Поворот: возьмите карту из колоды.",
			ActivationTapsSelf = true,
			OnActivated = ctx => ctx.Battle.DrawCardFor(ctx.Player),
		},
		new AbilityData
		{
			Id = "ability_shikimaru_immobilize",
			Name = "Теневая техника",
			Description = "1 любая мана: обездвижьте существо на соседней линии.",
			GlossaryTerms = [Glossary.Immobilize],
			ActivationCost = [new ManaCost(1, ManaTypeExtensions.AllTypes)],
			ActivationTarget = ActivationTarget.AdjacentCreature,
			OnActivated = ctx => ctx.Battle.Immobilize(ctx.Target, ctx.Player),
		},

		// ───────── Барон Суббота ─────────
		new AbilityData
		{
			Id = "ability_baron_draw",
			Name = "Могильщик",
			Description = "Вместо того чтобы взять карту из колоды, можете взять случайную карту из подконтрольного сброса "
				+ "(кнопка в окне сброса).",
			DrawFromDiscard = true,
		},
		new AbilityData
		{
			Id = "ability_baron_destroy",
			Name = "Последний суд",
			Description = "2 тьмы: уничтожьте любое существо, кроме героя, если у вас в сбросе больше существ, чем у противника.",
			ActivationCost = [new ManaCost(2, ManaType.Darkness)],
			ActivationTarget = ActivationTarget.AnyNonHeroCreature,
			ActivationCondition = ctx =>
				ctx.Battle.CreaturesInDiscard(ctx.Player) > ctx.Battle.CreaturesInDiscard(ctx.Battle.OpponentOf(ctx.Player)),
			UsableFromDiscard = true,
			OnActivated = ctx => ctx.Battle.Kill(ctx.Target),
		},
		new AbilityData
		{
			Id = "ability_baron_control",
			Name = "Хозяин кладбища",
			Description = "Поворот: до конца хода вы контролируете сброс противника и можете активировать свойства "
				+ "существ из любого сброса, если это возможно.",
			ActivationTapsSelf = true,
			OnActivated = ctx => ctx.Player.ControlsAllDiscards = true,
		},

		// ───────── Общие ключевые слова ─────────
		new AbilityData
		{
			Id = "ability_first_strike",
			Name = Glossary.FirstStrike,
			Description = "Убил в схватке — не получил урона.",
			GlossaryTerms = [Glossary.FirstStrike],
			FirstStrike = true,
		},
		new AbilityData
		{
			Id = "ability_flash",
			Name = Glossary.Flash,
			Description = "Можно разыграть в любую фазу.",
			GlossaryTerms = [Glossary.Flash],
			Flash = true,
		},

		// ───────── Выстрелы (через конструктор Shot) ─────────
		Shot("ability_shot_peasant", damage: 3, distance: 1, ammo: 3),
		Shot("ability_shot_archer", damage: 3, distance: 2, ammo: 10),

		// ───────── Существа ─────────
		new AbilityData
		{
			Id = "ability_zombie_exile",
			Name = "Восставший",
			Description = "При розыгрыше можете изгнать до 2 существ из своего сброса: каждое уменьшает цену на 1 тьму.",
			// Логика в TestBattle (CardData.ExileDiscountMax): срабатывает до оплаты.
		},
		new AbilityData
		{
			Id = "ability_peasant_meat",
			Name = "Разделка",
			Description = "Когда это существо убивает другое существо, вы получаете 1 еду.",
			OnKill = ctx => ctx.Player.AddMana(ManaType.Food, 1),
		},
		new AbilityData
		{
			Id = "ability_raccoon_free",
			Name = "Мародёр",
			Description = "Если в этом ходу умерло любое существо, эта карта бесплатна "
				+ "и её можно разыграть на линию, где умерло существо.",
			// Логика в TestBattle (CardData.FreeAfterDeathThisTurn).
		},
		new AbilityData
		{
			Id = "ability_fari_discard",
			Name = "Подменыш",
			Description = "Вместо оплаты маны можете сбросить карту из руки (на ваш выбор).",
			// Это альтернативная цена: CardData.AlternativeCosts.
		},
		new AbilityData
		{
			Id = "ability_fari_stun",
			Name = "Пыльца",
			Description = "Когда это существо нападает на другое существо, после схватки оглушите того, с кем оно билось.",
			GlossaryTerms = [Glossary.Stun],
			StunsWhenAttacking = true,
		},
		new AbilityData
		{
			Id = "ability_hiva",
			Name = "Обида",
			Description = "Каждый раз, когда эта карта получает урон, противник сбрасывает 2 случайные карты.",
			OnDamaged = ctx =>
			{
				PlayerState opponent = ctx.Battle.OpponentOf(ctx.Source.Player);
				ctx.Battle.DiscardRandomFromHand(opponent);
				ctx.Battle.DiscardRandomFromHand(opponent);
			},
		},
		new AbilityData
		{
			Id = "ability_lamplighter",
			Name = "Фонарь",
			Description = "Каждый раз, когда это существо попадает на линию, где ещё не было (и при розыгрыше), получите 1 свет.",
			OnEnterNewLane = ctx => ctx.Source.Player.AddMana(ManaType.Light, 1),
		},

		// ───────── Заклинания ─────────
		new AbilityData
		{
			Id = "ability_drink",
			Name = "Выпить любой ценой",
			Description = "Цена X любой маны (X ≥ 1). Любую единицу можно оплатить сбросом карты из руки "
				+ "или жертвой своего существа. Возьмите X карт.",
			OnPlayed = ctx =>
			{
				for (int i = 0; i < ctx.X; i++)
					ctx.Battle.DrawCardFor(ctx.Player);
			},
		},
		new AbilityData
		{
			Id = "ability_pyro",
			Name = "Трюк пиромана",
			Description = "Чтобы разыграть, сбросьте другую карту из руки. Нанесите 2 урона существу и возьмите карту. "
				+ "Вместо сброса эта карта замешивается обратно в колоду.",
			OnPlayed = ctx =>
			{
				ctx.Battle.DealDamage(ctx.Target, 2, ctx.Source);
				ctx.Battle.DrawCardFor(ctx.Player);
			},
		},

		// ───────── Чары ─────────
		new AbilityData
		{
			Id = "ability_serene_vision",
			Name = "Безмятежное видение",
			Description = "При розыгрыше получите 1 свет. Оглушите прикреплённое существо. Когда оно развернётся, "
				+ "его владелец получает на выбор 2 тьмы или 2 света, а эти чары уходят в сброс.",
			GlossaryTerms = [Glossary.Stun],
			OnPlayed = ctx =>
			{
				ctx.Player.AddMana(ManaType.Light, 1);
				ctx.Battle.Stun(ctx.Target, 1);
			},
			OnHostUntapped = async ctx =>
			{
				PlayerState owner = ctx.Target.Player;
				int choice = await ctx.Battle.ChooseAsync(owner, "«Безмятежное видение»: что получить?", ["2 тьмы", "2 света"]);
				owner.AddMana(choice == 0 ? ManaType.Darkness : ManaType.Light, 2);
				ctx.Battle.DiscardAttachment(ctx.Source);
				ctx.Battle.AfterAsyncEffect();
			},
		},
		new AbilityData
		{
			Id = "ability_addiction",
			Name = "Зависимость",
			Description = "Владелец прикреплённого существа выбирает тип маны. В фазу трофеев: здоровье существа −1 "
				+ "или он платит 1 ману выбранного типа.",
			OnPlayed = async ctx =>
			{
				PlayerState owner = ctx.Target.Player;
				ManaType[] types = ManaTypeExtensions.AllTypes;
				int choice = await ctx.Battle.ChooseAsync(owner, "«Зависимость»: выберите тип маны", types.Select(t => t.ToRussian()).ToList());
				ctx.Source.ChosenMana = types[choice];
				ctx.Battle.AfterAsyncEffect();
			},
			OnTrophyPhase = async ctx =>
			{
				PlayerState owner = ctx.Target.Player;
				ManaType type = ctx.Source.ChosenMana ?? ManaType.Grass;
				bool canPay = owner.Mana.GetValueOrDefault(type) > 0;

				int choice = canPay
					? await ctx.Battle.ChooseAsync(owner, $"«Зависимость» на «{ctx.Target.Data.Name}»",
						[$"Заплатить 1 {type.ToRussian()}", "Здоровье −1"])
					: 1;

				if (choice == 0)
					owner.Mana[type]--;
				else
					ctx.Battle.DealDamage(ctx.Target, 1, ctx.Source);

				ctx.Battle.AfterAsyncEffect();
			},
		},

		// По одной способности «дать ману» на каждый основной тип: ability_mana_fire и т.д.
		.. ManaTypeExtensions.MainTypes.Select(GainMana),
	];

	// Словарь для быстрого поиска по Id. Строится один раз из списка All.
	private static readonly Dictionary<string, AbilityData> ById = All.ToDictionary(a => a.Id);

	public static AbilityData Get(string id) => ById[id];

	public static string GainManaId(ManaType type) => $"ability_mana_{type.ToString().ToLowerInvariant()}";

	/// <summary>
	/// Конструктор выстрела: одна строка вместо целого блока.
	/// Пример: Shot("ability_shot_archer", damage: 3, distance: 2, ammo: 10).
	/// cost не указан — выстрел бесплатный; ammo не указан — боезапас бесконечный.
	/// </summary>
	public static AbilityData Shot(string id, int damage, int distance, ManaCost[] cost = null, int? ammo = null)
	{
		var parts = new List<string> { $"урон {damage}", $"дистанция {distance}" };
		if (cost is { Length: > 0 })
			parts.Add("цена " + string.Join(" + ", cost.Select(c => c.ToString())));
		if (ammo != null)
			parts.Add($"боезапас {ammo}");

		return new AbilityData
		{
			Id = id,
			Name = $"{Glossary.Shot} ({string.Join(", ", parts)})",
			Description = "Нанесите урон вражескому существу в пределах дистанции, не получив урона в ответ.",
			GlossaryTerms = [Glossary.Shot],
			Shot = new ShotInfo { Damage = damage, Distance = distance, Ammo = ammo },
			ActivationCost = cost ?? [],
			ActivationTapsSelf = true,
			ActivationTarget = ActivationTarget.ShotTarget,
			ActivationCondition = ctx => ctx.Battle.CanShoot(ctx.Source, id),
			OnActivated = ctx => ctx.Battle.Shoot(ctx.Source, ctx.Target, id),
		};
	}

	private static AbilityData GainMana(ManaType type) => new()
	{
		Id = GainManaId(type),
		Name = $"Оплата: {type.ToRussian()}",
		Description = $"При розыгрыше добавляет в накопитель {PaymentManaAmount} маны «{type.ToRussian()}».",
		OnPlayed = ctx => ctx.Player.AddMana(type, PaymentManaAmount),
	};
}
