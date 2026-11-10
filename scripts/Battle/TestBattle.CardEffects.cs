using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame;

/// <summary>
/// Часть TestBattle: эффекты отдельных карт, которые слишком длинные для лямбды в AbilitiesData
/// (выбор игрока, поиск подходящих карт). Способности вызывают эти методы через ctx.Battle.
/// </summary>
public partial class TestBattle
{
	public void Heal(Card card, int amount) => card.Heal(amount);

	public void Buff(Card card, int attack, int defense, int health) => card.AddBonus(attack, defense, health);

	/// <summary>Контроль до конца хода: существо слушается нового игрока, в StartNextTurn вернётся хозяину.</summary>
	public void TakeControlUntilEndOfTurn(Card card, PlayerState player)
	{
		card.Player = player;
		card.SetBaseBorder(ColorFor(player));
	}

	/// <summary>Легион: новая карта того же типа «с нуля» на той же линии.</summary>
	public void CreateCopy(Card source)
	{
		Card copy = CreateCard(source.Data.Id, source.Player, source.Player.Hand);
		copy.PlayedPhase = _phaseCounter;
		PlaceOnLane(copy, LaneOf(source));
	}

	/// <summary>Вернуть карту в руку хозяина: с поля или из сброса (Тень вечной ночи, Гадство).</summary>
	public void ReturnToHand(Card card)
	{
		PlayerState owner = card.OwnerPlayer ?? card.Player;
		string id = card.Data.DiscardAsId ?? card.Data.Id;
		Lane lane = LaneOf(card);

		if (lane != null)
		{
			// С поля: прикреплённое снаряжение выпадает на линию, чары уходят в сброс.
			foreach (Card attachment in card.Attachments.ToList())
			{
				card.Detach(attachment);
				if (attachment.Data.Is(CardType.Equipment))
					lane.Cards.AddChild(attachment);
				else
					SendToDiscard(attachment);
			}
		}

		if (card.IsInsideTree())
		{
			card.GetParent().RemoveChild(card);
			card.QueueFree();
		}
		else
		{
			// Карта из сброса (временная копия): убираем запись из сброса.
			owner.Discard.Remove(id);
		}

		CreateCard(id, owner, owner.Hand);
	}

	/// <summary>Воздушный Элементаль: повернуть ещё одно вражеское существо на линии цели.</summary>
	public async void TapAnotherEnemyOnLane(Card attacker, Card target)
	{
		List<Card> candidates = LaneOf(target).Creatures
			.Where(c => c != target && c.Player != attacker.Player && !c.IsTapped)
			.ToList();
		if (candidates.Count == 0)
			return;

		int index = candidates.Count == 1
			? 0
			: await ChooseAsync(attacker.Player, "«Вихрь»: какое существо повернуть?", Names(candidates));
		candidates[index].SetTapped(true);
		RefreshView();
	}

	/// <summary>Необыкновенное животное: жертва, потом 1 золото или скидка Человеку в руке.</summary>
	public async void ResolveStrangeAnimal(Card source, PlayerState player)
	{
		Kill(source);

		List<Card> humans = player.Hand.GetChildren().OfType<Card>()
			.Where(c => c.Data.Has(CardTag.Human) && c.Data.Cost.Sum(m => m.Amount) > c.CostDiscount)
			.ToList();

		var options = new List<string> { "Получить 1 золото" };
		if (humans.Count > 0)
			options.Add("Оплатить 1 стоимости Человека в руке");

		int choice = options.Count == 1 ? 0 : await ChooseAsync(player, "«Подношение»: выберите эффект", options);
		if (choice == 0)
		{
			player.AddMana(ManaType.Gold, 1);
		}
		else
		{
			int index = humans.Count == 1 ? 0 : await ChooseAsync(player, "Какому Человеку уменьшить цену?", Names(humans));
			humans[index].CostDiscount++;
		}

		AfterAsyncEffect();
	}

	/// <summary>Гадство: один из трёх эффектов на выбор.</summary>
	public async void ResolveNastiness(PlayerState player)
	{
		PlayerState opponent = OpponentOf(player);
		int effect = await ChooseAsync(player, "«Гадство»: выберите эффект",
		[
			"Противник сбрасывает 2 карты",
			"Противник жертвует карту выбранного типа",
			"Вернуть в руку оппонента карту, сыгранную в прошлую фазу",
		]);

		if (effect == 0)
		{
			DiscardRandomFromHand(opponent);
			DiscardRandomFromHand(opponent);
		}
		else if (effect == 1)
		{
			CardType[] types = [CardType.Creature, CardType.Enchantment, CardType.Equipment, CardType.Advantage];
			CardType type = types[await ChooseAsync(player, "Тип карты для жертвы", types.Select(t => t.ToRussian()).ToList())];

			List<Card> controlled = CardsInPlay(opponent).Where(c => c.Data.Is(type)).ToList();
			if (controlled.Count > 1)
			{
				// Какую карту отдать, решает сам противник.
				int index = await ChooseAsync(opponent, $"Пожертвуйте карту типа «{type.ToRussian()}»", Names(controlled));
				Sacrifice(controlled[index]);
			}
			else
			{
				ShowHint($"У {opponent.Name} не больше 1 карты типа «{type.ToRussian()}» — жертвы нет.");
			}
		}
		else
		{
			List<Card> recent = CardsInPlay(opponent).Where(c => c.PlayedPhase == _phaseCounter - 1).ToList();
			if (recent.Count > 0)
			{
				int index = recent.Count == 1 ? 0 : await ChooseAsync(player, "Какую карту вернуть?", Names(recent));
				Card card = recent[index];
				Card host = AllCreatures.FirstOrDefault(c => c.Attachments.Contains(card));
				host?.Detach(card);
				ReturnToHand(card);
			}
			else
			{
				ShowHint($"{opponent.Name} не разыгрывал карт в прошлую фазу.");
			}
		}

		AfterAsyncEffect();
	}

	/// <summary>Множественный взор: подсмотреть карты оппонента и сбросить из его руки карты выбранного типа.</summary>
	public async void ResolveMultiGaze(PlayerState player)
	{
		PlayerState opponent = OpponentOf(player);
		CardType[] types = Enum.GetValues<CardType>();
		CardType type = types[await ChooseAsync(player, "«Множественный взор»: выберите тип карты", types.Select(t => t.ToRussian()).ToList())];

		int toSee = player.Hand.GetChildCount();
		List<Card> hand = opponent.Hand.GetChildren().OfType<Card>().OrderBy(_ => _rng.Next()).ToList();
		List<Card> seenInHand = hand.Take(toSee).ToList();

		// У вас больше карт, чем у оппонента в руке — остаток досматриваем в его колоде.
		int fromDeck = Math.Max(0, toSee - hand.Count);
		List<string> seenInDeck = opponent.Deck.OrderBy(_ => _rng.Next()).Take(fromDeck).ToList();

		List<string> seenIds = seenInHand.Select(c => c.Data.Id).Concat(seenInDeck).ToList();
		List<Card> discarded = seenInHand.Where(c => c.Data.Is(type)).ToList();
		discarded.ForEach(SendToDiscard);

		// Увиденное показываем только тому, кто смотрел.
		if (IsLocal(player))
			ShowGazeResult(opponent, seenInHand.Count, seenInDeck.Count, type, discarded.Count, seenIds);

		AfterAsyncEffect();
	}

	private void ShowGazeResult(PlayerState opponent, int fromHand, int fromDeck, CardType type, int discardedCount, List<string> seenIds)
	{
		_viewedDiscardOwner = null;
		_discardView.Open($"Увидено у {opponent.Name}: из руки {fromHand}, из колоды {fromDeck}. "
			+ $"Сброшено «{type.ToRussian()}»: {discardedCount}", seenIds, CardScene, opponent);
	}

	/// <summary>Кладбище: свет или тьма по числу существ на нём.</summary>
	public async void ResolveGraveyardHarvest(Card graveyard, PlayerState player)
	{
		int amount = graveyard.StoredCards.Count;
		int choice = await ChooseAsync(player, $"«Жатва»: получить {amount}", [$"{amount} света", $"{amount} тьмы"]);
		player.AddMana(choice == 0 ? ManaType.Light : ManaType.Darkness, amount);
		AfterAsyncEffect();
	}

	// ───────────── Набор 4 ─────────────

	public void AddTempAttack(Card card, int amount) => card.AddTempAttack(amount);

	public void Poison(Card card) => card.SetPoisoned(true);

	/// <summary>Чумной монах: отравить всех существ обоих игроков.</summary>
	public void PoisonAllCreatures()
	{
		foreach (Card creature in AllCreatures)
			creature.SetPoisoned(true);
	}

	/// <summary>Скавенский Ассасин: сейчас диверсия и он стоит на одной из линий оппонента.</summary>
	public bool IsOnEnemyLineInDiversion(Card card) =>
		_phase == BattlePhase.Diversion && LaneOf(card)?.Owner == OpponentOf(card.Player);

	/// <summary>Серый провидец: скавены и крысы под вашим контролем +1 к атаке до конца хода.</summary>
	public void RallySkavens(PlayerState player)
	{
		foreach (Card creature in AllCreatures.Where(c => c.Player == player && (c.Data.Has(CardTag.Skaven) || c.Data.Id == "rat")))
			creature.AddTempAttack(1);
	}

	/// <summary>Создать жетоны (крыс) на линии карты-источника.</summary>
	public void CreateTokens(string id, int count, PlayerState player, Card source)
	{
		Lane lane = LaneOf(source);
		for (int i = 0; i < count; i++)
		{
			Card token = CreateCard(id, player, player.Hand);
			token.PlayedPhase = _phaseCounter;
			PlaceOnLane(token, lane);
		}
	}

	public int HumansControlledBy(PlayerState player) => AllCreatures.Count(c => c.Player == player && c.Data.Has(CardTag.Human));

	/// <summary>Взять из колоды случайную карту нужного типа (Аскет — преимущество). Нет такой — ничего.</summary>
	public void TakeFromDeck(PlayerState player, CardType type)
	{
		List<string> matching = player.Deck.Where(id => CardsData.Get(id).Is(type)).ToList();
		if (matching.Count == 0)
			return;

		string id = matching[_rng.Next(matching.Count)];
		player.Deck.Remove(id);
		CreateCard(id, player, player.Hand);
	}

	/// <summary>Элементалист: игрок выбирает, какую элементальную ману получить.</summary>
	public async void GainChosenElementalMana(PlayerState player)
	{
		ManaType[] types = ManaTypeExtensions.ElementalTypes;
		int choice = await ChooseAsync(player, "Какую элементальную ману получить?", types.Select(t => t.ToRussian()).ToList());
		player.AddMana(types[choice], 1);
		AfterAsyncEffect();
	}

	// Какой элементаль за какую ману создаёт Элементалист.
	private static readonly (ManaType Mana, string ElementalId)[] BasicElementals =
	[
		(ManaType.Fire, "fire_elemental"),
		(ManaType.Water, "water_elemental"),
		(ManaType.Light, "air_elemental"),
		(ManaType.Grass, "earth_elemental"),
	];

	private const int ElementalSummonCost = 3;

	public bool CanSummonElemental(PlayerState player) =>
		BasicElementals.Any(e => player.Mana.GetValueOrDefault(e.Mana) >= ElementalSummonCost);

	/// <summary>Элементалист: 3 одинаковые элементальные маны → базовый элементаль на его линии.</summary>
	public async void SummonElemental(Card source, PlayerState player)
	{
		var options = BasicElementals.Where(e => player.Mana.GetValueOrDefault(e.Mana) >= ElementalSummonCost).ToList();
		var labels = options.Select(e => $"{CardsData.Get(e.ElementalId).Name} ({ElementalSummonCost} {e.Mana.ToRussian()})").ToList();
		labels.Add("Отмена");

		int choice = await ChooseAsync(player, "«Призыв стихии»: кого создать?", labels);
		if (choice < options.Count && LaneOf(source) is { } lane)
		{
			(ManaType mana, string elementalId) = options[choice];
			player.Mana[mana] -= ElementalSummonCost;
			Card elemental = CreateCard(elementalId, player, player.Hand);
			elemental.PlayedPhase = _phaseCounter;
			PlaceOnLane(elemental, lane);
		}

		AfterAsyncEffect();
	}

	/// <summary>
	/// Голод: перед концом хода каждое голодное существо игрока требует плату.
	/// Не срабатывает, если в этом ходу умерло хоть одно существо.
	/// </summary>
	private async System.Threading.Tasks.Task PayHunger()
	{
		PlayerState player = _currentPlayer;

		foreach (Card creature in AllCreatures.Where(c => c.Player == player && c.HasAny(a => a.Hunger)).ToList())
		{
			if (_deathLanesThisTurn.Count > 0 || creature.IsDead)
				continue;

			List<string> exilable = Players.SelectMany(p => p.Discard.Select(id => (Owner: p, Id: id)))
				.Where(x => CardsData.Get(x.Id).Is(CardType.Creature))
				.Select(x => $"{x.Owner.Name}: {CardsData.Get(x.Id).Name}")
				.ToList();

			var options = new List<string> { $"1 здоровье «{creature.Data.Name}»" };
			bool canPayFood = player.Mana.GetValueOrDefault(ManaType.Food) > 0;
			if (canPayFood)
				options.Add("1 еда");
			if (exilable.Count > 0)
				options.Add("Изгнать существо из сброса");

			int choice = await ChooseAsync(player, $"Голод «{creature.Data.Name}»: чем заплатить?", options);
			string picked = options[choice];

			if (picked == "1 еда")
			{
				player.Mana[ManaType.Food]--;
			}
			else if (picked == "Изгнать существо из сброса")
			{
				int index = exilable.Count == 1 ? 0 : await ChooseAsync(player, "Какое существо изгнать?", exilable);
				ExileCreatureFromDiscard(index);
			}
			else
			{
				ApplyDamage(creature, 1, null);
				CleanupDead();
			}

			RefreshView();
		}
	}

	// Изгнать index-е по счёту существо из сбросов обоих игроков (в том же порядке, что в списке выбора).
	private void ExileCreatureFromDiscard(int index)
	{
		foreach (PlayerState p in Players)
		{
			for (int i = 0; i < p.Discard.Count; i++)
			{
				if (!CardsData.Get(p.Discard[i]).Is(CardType.Creature))
					continue;
				if (index-- == 0)
				{
					p.Discard.RemoveAt(i);
					return;
				}
			}
		}
	}

	// ───────────── Вспомогательное ─────────────

	/// <summary>Все карты игрока в игре: существа на линиях, преимущества, прикреплённые и выпавшие карты.</summary>
	private IEnumerable<Card> CardsInPlay(PlayerState player)
	{
		IEnumerable<Card> onLanes = AllLanes.SelectMany(l => l.AllCards);
		IEnumerable<Card> attached = AllCreatures.SelectMany(c => c.Attachments);
		IEnumerable<Card> advantages = player.Advantages.GetChildren().OfType<Card>();
		return onLanes.Concat(attached).Concat(advantages).Where(c => c.Player == player).Distinct();
	}

	/// <summary>Пожертвовать карту: существо погибает, остальное уходит в сброс.</summary>
	private void Sacrifice(Card card)
	{
		if (card.IsCreature && LaneOf(card) != null)
		{
			Kill(card);
			return;
		}

		Card host = AllCreatures.FirstOrDefault(c => c.Attachments.Contains(card));
		host?.Detach(card);
		SendToDiscard(card);
	}

	/// <summary>Подменить карту другой (Легион ⇄ Легион-чары) на том же месте.</summary>
	private Card SwapCard(Card card, string newId)
	{
		var parent = card.GetParent();
		int index = card.GetIndex();
		Card swapped = CreateCard(newId, card.Player, parent);
		parent.MoveChild(swapped, index);
		parent.RemoveChild(card);
		card.QueueFree();
		return swapped;
	}

	/// <summary>Подписи для окна выбора: «Карта3 (Игрок 2, PlayerLane1)».</summary>
	private List<string> Names(IEnumerable<Card> cards) =>
		cards.Select((c, i) => $"{i + 1}. {c.Data.Name}" + (LaneOf(c) is { } lane ? $" ({lane.Panel.Name})" : "")).ToList();
}
