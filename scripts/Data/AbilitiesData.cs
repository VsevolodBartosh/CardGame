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

		// ───────── Элементали ─────────
		Shot("ability_shot_fire_elemental", damage: 5, distance: 1, selfDamage: 3),
		new AbilityData
		{
			Id = "ability_water_elemental",
			Name = "Течение",
			Description = "В фазу трофеев теряет 1 здоровье. Каждый раз, когда вы разыгрываете карту с водой в цене, "
				+ "восстановите этой карте 3 здоровья.",
			OnTrophyPhase = ctx => ctx.Battle.DealDamage(ctx.Source, 1),
			OnOwnerPlayedCard = ctx =>
			{
				if (ctx.Target.Data.Cost.Any(c => c.Types.Contains(ManaType.Water)))
					ctx.Battle.Heal(ctx.Source, 3);
			},
		},
		new AbilityData
		{
			Id = "ability_earth_elemental",
			Name = "Плодородие",
			Description = "1 труд и поворот: получите 1 траву.",
			ActivationLaborCost = 1,
			ActivationTapsSelf = true,
			OnActivated = ctx => ctx.Player.AddMana(ManaType.Grass, 1),
		},
		new AbilityData
		{
			Id = "ability_air_elemental",
			Name = "Вихрь",
			Description = "Когда эта карта нападает на существо, поверните ещё одно вражеское существо на той же линии.",
			OnAttackDeclared = ctx => ctx.Battle.TapAnotherEnemyOnLane(ctx.Source, ctx.Target),
		},

		// ───────── Легион, Тень, Животное ─────────
		new AbilityData
		{
			Id = "ability_legion_copy",
			Name = "Нас много",
			Description = "1 тьма: создайте на той же линии полную изначальную копию этой карты.",
			ActivationCost = [new ManaCost(1, ManaType.Darkness)],
			OnActivated = ctx => ctx.Battle.CreateCopy(ctx.Source),
		},
		new AbilityData
		{
			Id = "ability_legion_as_enchantment",
			Name = "Одержимость",
			Description = "Можно разыграть как чары за 2 тьмы: бросьте карту на существо.",
		},
		new AbilityData
		{
			Id = "ability_legion_enchantment",
			Name = "Одержимость",
			Description = "Прикреплённое существо получает +1/+1/+1 до конца матча. Если вы его не контролируете, "
				+ "получите над ним контроль до конца хода.",
			OnPlayed = ctx =>
			{
				ctx.Battle.Buff(ctx.Target, attack: 1, defense: 1, health: 1);
				if (ctx.Target.Player != ctx.Player)
					ctx.Battle.TakeControlUntilEndOfTurn(ctx.Target, ctx.Player);
			},
		},
		new AbilityData
		{
			Id = "ability_shadow_undying",
			Name = "Вечная ночь",
			Description = "Эта карта не может умереть. Эту карту нельзя заблокировать.",
			CantDie = true,
			Unblockable = true,
		},
		new AbilityData
		{
			Id = "ability_shadow_return",
			Name = "Возвращение",
			Description = "Верните эту карту в руку (работает и из сброса).",
			UsableFromOwnDiscard = true,
			OnActivated = ctx => ctx.Battle.ReturnToHand(ctx.Source),
		},
		new AbilityData
		{
			Id = "ability_strange_animal",
			Name = "Подношение",
			Description = "Пожертвуйте эту карту, затем выберите: получите 1 золото или оплатите 1 стоимости Человека в вашей руке.",
			OnActivated = ctx => ctx.Battle.ResolveStrangeAnimal(ctx.Source, ctx.Player),
		},

		// ───────── Заклинания и преимущества (набор 3) ─────────
		new AbilityData
		{
			Id = "ability_nastiness",
			Name = "Гадство",
			Description = "Выберите один эффект: противник сбрасывает 2 карты; противник жертвует карту выбранного вами типа, "
				+ "если контролирует больше 1 карты этого типа; верните в руку оппонента карту, сыгранную в прошлую фазу.",
			OnPlayed = ctx => ctx.Battle.ResolveNastiness(ctx.Player),
		},
		new AbilityData
		{
			Id = "ability_multi_gaze",
			Name = "Множественный взор",
			Description = "2 тьмы и поворот: выберите тип карты. Посмотрите столько случайных карт из руки оппонента, сколько карт "
				+ "у вас в руке; если у вас больше, досмотрите остальное из его колоды. Увиденные карты из руки выбранного типа сбросьте.",
			ActivationCost = [new ManaCost(2, ManaType.Darkness)],
			ActivationTapsSelf = true,
			OnActivated = ctx => ctx.Battle.ResolveMultiGaze(ctx.Player),
		},
		new AbilityData
		{
			Id = "ability_graveyard_collect",
			Name = "Кладбище",
			Description = "Погибшие существа попадают на эту карту, а не в сброс.",
			CollectsDeadCreatures = true,
		},
		new AbilityData
		{
			Id = "ability_graveyard_harvest",
			Name = "Жатва",
			Description = "1 любая мана и поворот: получите свет или тьму в количестве, равном числу существ на кладбище.",
			ActivationCost = [new ManaCost(1, ManaTypeExtensions.AllTypes)],
			ActivationTapsSelf = true,
			OnActivated = ctx => ctx.Battle.ResolveGraveyardHarvest(ctx.Source, ctx.Player),
		},

		// ───────── Набор 4: общие ─────────
		new AbilityData
		{
			Id = "ability_hunger",
			Name = Glossary.Hunger,
			Description = "Чтобы закончить фазу трофеев: 1 здоровье этой карты, 1 еда или изгнать 1 существо из любого сброса. "
				+ "Не срабатывает, если в этом ходу умерло существо.",
			GlossaryTerms = [Glossary.Hunger],
			Hunger = true,
		},

		// ───────── Теневая тварь ─────────
		new AbilityData
		{
			Id = "ability_shadow_beast_feed",
			Name = "Голод тьмы",
			Description = "Каждый раз, когда вы разыгрываете карту, заплатив тьмой, эта карта получает +1 к атаке до конца хода.",
			OnOwnerPlayedCard = ctx =>
			{
				if (ctx.Target.LastPaidMana.Contains(ManaType.Darkness))
					ctx.Battle.AddTempAttack(ctx.Source, 1);
			},
		},
		new AbilityData
		{
			Id = "ability_shadow_beast_grow",
			Name = "Рост",
			Description = "1 тьма: эта карта получает +1/+1/+2 до конца матча.",
			ActivationCost = [new ManaCost(1, ManaType.Darkness)],
			OnActivated = ctx => ctx.Battle.Buff(ctx.Source, attack: 1, defense: 1, health: 2),
		},

		// ───────── Скавены ─────────
		new AbilityData
		{
			Id = "ability_plague_monk",
			Name = "Мор",
			Description = "Когда эта карта нападает на существо, все существа обоих игроков получают отравление.",
			GlossaryTerms = [Glossary.Poison],
			OnAttackDeclared = ctx => ctx.Battle.PoisonAllCreatures(),
		},
		new AbilityData
		{
			Id = "ability_skaven_assassin_raid",
			Name = "Набег",
			Description = "Поворот, если эта карта на первой или второй линии оппонента в фазу диверсии: "
				+ "противник сбрасывает карту, вы получаете 1 золото.",
			ActivationTapsSelf = true,
			ActivationCondition = ctx => ctx.Battle.IsOnEnemyLineInDiversion(ctx.Source),
			OnActivated = ctx =>
			{
				ctx.Battle.DiscardRandomFromHand(ctx.Battle.OpponentOf(ctx.Player));
				ctx.Player.AddMana(ManaType.Gold, 1);
			},
		},
		new AbilityData
		{
			Id = "ability_seer_rally",
			Name = "Знамение",
			Description = "Поворот: скавены и крысы под вашим контролем получают +1 к атаке до конца хода.",
			ActivationTapsSelf = true,
			OnActivated = ctx => ctx.Battle.RallySkavens(ctx.Player),
		},
		new AbilityData
		{
			Id = "ability_seer_rats",
			Name = "Крысиный выводок",
			Description = "1 тьма: создайте 2 крыс (1/1/1/0, Зверь) на линии этой карты.",
			ActivationCost = [new ManaCost(1, ManaType.Darkness)],
			OnActivated = ctx => ctx.Battle.CreateTokens("rat", 2, ctx.Player, ctx.Source),
		},
		Shot("ability_shot_seer", damage: 6, distance: 1, cost: [new ManaCost(2, ManaType.Darkness)],
			extraCostPerLine: [new ManaCost(1, ManaType.Darkness)]),
		new AbilityData
		{
			Id = "ability_seer_poison",
			Name = "Варп-яд",
			Description = "2 тьмы: отравите выбранное существо.",
			GlossaryTerms = [Glossary.Poison],
			ActivationCost = [new ManaCost(2, ManaType.Darkness)],
			ActivationTarget = ActivationTarget.AnyCreature,
			OnActivated = ctx => ctx.Battle.Poison(ctx.Target),
		},

		// ───────── Люди веры ─────────
		new AbilityData
		{
			Id = "ability_preacher",
			Name = "Проповедь",
			Description = "При розыгрыше получите 1 веру за каждого человека под вашим контролем. "
				+ "Каждый раз, когда вы разыгрываете человека, получите 1 веру.",
			OnPlayed = ctx => ctx.Player.AddMana(ManaType.Faith, ctx.Battle.HumansControlledBy(ctx.Player)),
			OnOwnerPlayedCard = ctx =>
			{
				if (ctx.Target.Data.Has(CardTag.Human))
					ctx.Player.AddMana(ManaType.Faith, 1);
			},
		},
		new AbilityData
		{
			Id = "ability_enlightened",
			Name = "Прозрение",
			Description = "Когда эта карта потрудится 3 раза: поворот — получите 1 веру.",
			ActivationTapsSelf = true,
			ActivationCondition = ctx => ctx.Source.LaborCount >= 3,
			OnActivated = ctx => ctx.Player.AddMana(ManaType.Faith, 1),
		},
		new AbilityData
		{
			Id = "ability_diligent",
			Name = "Усердие",
			Description = "Когда эта карта потрудится 3 раза, она начнёт давать 2 труда вместо 1.",
			DoubleLaborAfter = 3,
		},
		new AbilityData
		{
			Id = "ability_miracle",
			Name = "Чудо",
			Description = "1 вера: исцелите существо на 2.",
			ActivationCost = [new ManaCost(1, ManaType.Faith)],
			ActivationTarget = ActivationTarget.AnyCreature,
			OnActivated = ctx => ctx.Battle.Heal(ctx.Target, 2),
		},
		new AbilityData
		{
			Id = "ability_fanatics",
			Name = "Рвение",
			Description = "Если у вас 3 веры в накопителе: +2 к атаке и защите. Если 5 веры: ещё +2 к блоку.",
			DynamicBonus = card =>
			{
				int faith = card.Player?.Mana.GetValueOrDefault(ManaType.Faith) ?? 0;
				int attackAndDefense = faith >= 3 ? 2 : 0;
				return new StatBonus(attackAndDefense, attackAndDefense, faith >= 5 ? 2 : 0);
			},
		},
		new AbilityData
		{
			Id = "ability_ascetic",
			Name = "Аскеза",
			Description = "В фазу трофеев нанесите этой карте 1 урон. Если она от этого погибла, возьмите преимущество из своей колоды.",
			OnTrophyPhase = ctx =>
			{
				ctx.Battle.DealDamage(ctx.Source, 1);
				if (ctx.Source.IsDead)
					ctx.Battle.TakeFromDeck(ctx.Player, CardType.Advantage);
			},
		},
		new AbilityData
		{
			Id = "ability_elementalist_burn",
			Name = "Жертвенный огонь",
			Description = "Поворот: нанесите 2 урона своему существу, затем получите любую элементальную ману.",
			ActivationTapsSelf = true,
			ActivationTarget = ActivationTarget.FriendlyCreature,
			OnActivated = ctx =>
			{
				ctx.Battle.DealDamage(ctx.Target, 2, ctx.Source);
				ctx.Battle.GainChosenElementalMana(ctx.Player);
			},
		},
		new AbilityData
		{
			Id = "ability_elementalist_summon",
			Name = "Призыв стихии",
			Description = "3 одинаковые элементальные маны: создайте базового элементаля этого типа "
				+ "(огонь — Огненный, вода — Водный, свет — Воздушный, трава — Земляной).",
			ActivationCondition = ctx => ctx.Battle.CanSummonElemental(ctx.Player),
			OnActivated = ctx => ctx.Battle.SummonElemental(ctx.Source, ctx.Player),
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
	public static AbilityData Shot(string id, int damage, int distance, ManaCost[] cost = null, int? ammo = null,
		int selfDamage = 0, ManaCost[] extraCostPerLine = null)
	{
		var parts = new List<string> { $"урон {damage}", $"дистанция {distance}" };
		if (cost is { Length: > 0 })
			parts.Add("цена " + string.Join(" + ", cost.Select(c => c.ToString())));
		if (ammo != null)
			parts.Add($"боезапас {ammo}");
		if (extraCostPerLine is { Length: > 0 })
			parts.Add($"дальше — +{string.Join(" + ", extraCostPerLine.Select(c => c.ToString()))} за линию");

		string description = "Нанесите урон вражескому существу в пределах дистанции, не получив урона в ответ.";
		if (selfDamage > 0)
			description += $" После выстрела нанесите {selfDamage} урона этой карте.";

		return new AbilityData
		{
			Id = id,
			Name = $"{Glossary.Shot} ({string.Join(", ", parts)})",
			Description = description,
			GlossaryTerms = [Glossary.Shot],
			Shot = new ShotInfo
			{
				Damage = damage,
				Distance = distance,
				Ammo = ammo,
				SelfDamage = selfDamage,
				ExtraCostPerLine = extraCostPerLine ?? [],
			},
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
