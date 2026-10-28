using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace CardGame;

/// <summary>
/// Часть TestBattle: действия, которые вызывают способности из AbilitiesData,
/// активация способностей (с выбором цели, из сброса) и условия победы.
/// </summary>
public partial class TestBattle
{
	/// <summary>Идёт выбор цели для способности. null — не идёт.</summary>
	private Targeting _targeting;

	private bool _gameEnded;
	private Control _gameOver;
	private Label _gameOverText;
	private Label _gameOverReason;

	private void ReadyAbilitiesUi()
	{
		_gameOver = GetNode<Control>("GameOver");
		_gameOverText = GetNode<Label>("GameOver/Center/Column/Text");
		_gameOverReason = GetNode<Label>("GameOver/Center/Column/Reason");
		GetNode<Button>("GameOver/Center/Column/RestartButton").Pressed += () => GetTree().ReloadCurrentScene();
		ReadyDraftUi();
	}

	// ───────────── Действия для способностей ─────────────
	// Способности из AbilitiesData получают TestBattle через AbilityContext и вызывают эти методы.

	public PlayerState OpponentOf(PlayerState player) => player == _bottomPlayer ? _topPlayer : _bottomPlayer;

	/// <summary>
	/// Нанести урон существу. source — кто нанёс (нужно для «когда убивает»), может быть null.
	/// Погибшие уходят в сброс после того, как эффект доиграет.
	/// </summary>
	public void DealDamage(Card target, int amount, Card source = null) => ApplyDamage(target, amount, source);

	/// <summary>Уничтожить существо: здоровье в ноль, уберётся вместе с остальными погибшими.</summary>
	public void Kill(Card target)
	{
		target.LastDamageSource = null;
		target.TakeDamage(target.CurrentHealth);
	}

	/// <summary>Оглушить: повернуть и пропустить turns разворотов в снабжение владельца.</summary>
	public void Stun(Card target, int turns)
	{
		target.SetTapped(true);
		target.SetStunTurns(Math.Max(target.StunTurns, turns));
	}

	/// <summary>Выстрел возможен: фаза атаки своего хода, существо на поле, боезапас не кончился.</summary>
	public bool CanShoot(Card shooter, string abilityId) =>
		_phase == BattlePhase.Attack
		&& shooter.Player == _currentPlayer
		&& LaneOf(shooter) != null
		&& shooter.AmmoLeft(AbilitiesData.Get(abilityId)) is null or > 0;

	/// <summary>Выстрел: урон цели (минус её блок), в ответ урона нет. Поворот делает сама активация.</summary>
	public void Shoot(Card shooter, Card target, string abilityId)
	{
		ShotInfo shot = AbilitiesData.Get(abilityId).Shot;
		shooter.UseAmmo(abilityId);
		ApplyDamage(target, AfterBlock(shot.Damage, target), shooter);
	}

	/// <summary>Спросить игрока и дождаться ответа (номер варианта).</summary>
	public Task<int> ChooseAsync(PlayerState player, string title, IReadOnlyList<string> options) =>
		_choiceView.Ask($"{player.Name}: {title}", options);

	/// <summary>
	/// Вызывать в конце эффекта, который ждал ответа игрока (await): эффект доиграл позже
	/// обычного, поэтому погибших и экран нужно обновить ещё раз.
	/// </summary>
	public void AfterAsyncEffect()
	{
		CleanupDead();
		ForgetDeadInAttacks();
		RefreshView();
	}

	/// <summary>Снять прикреплённую карту с существа и положить в сброс её владельца.</summary>
	public void DiscardAttachment(Card attachment)
	{
		Card host = AllCreatures.FirstOrDefault(c => c.Attachments.Contains(attachment));
		host?.Detach(attachment);
		SendToDiscard(attachment);
	}

	public void MoveCreature(Card creature, Lane lane) => PlaceOnLane(creature, lane);

	/// <summary>Взять карту сверх обычного добора (не тратит добор хода).</summary>
	public void DrawCardFor(PlayerState player) => DrawCard(player);

	public void DiscardRandomFromHand(PlayerState player)
	{
		List<Card> hand = player.Hand.GetChildren().OfType<Card>().ToList();
		if (hand.Count > 0)
			SendToDiscard(hand[Random.Shared.Next(hand.Count)]);
	}

	public void Immobilize(Card target, PlayerState by) => target.SetImmobilized(by);

	public int CreaturesInDiscard(PlayerState player) => player.Discard.Count(id => CardsData.Get(id).Is(CardType.Creature));

	// ───────────── Активация ─────────────

	/// <summary>Карта в игре: на линии или в преимуществах.</summary>
	private bool IsInPlay(Card card) => LaneOf(card) != null || card.GetParent() == card.Player?.Advantages;

	private IEnumerable<AbilityData> ActivatableAbilities(Card card) => card.Abilities.Where(a => CanActivate(card, a));

	/// <summary>
	/// Можно ли сейчас активировать способность. Активирует всегда тот, кто действует в фазе:
	/// свою карту в игре или, при контроле над сбросами, карту из открытого сброса.
	/// </summary>
	private bool CanActivate(Card card, AbilityData ability)
	{
		// Оглушённое существо не может использовать способности.
		if (ability.OnActivated == null || IsBusy || card.IsStunned)
			return false;

		PlayerState player = ActingPlayer;
		bool inPlay = IsInPlay(card);

		if (inPlay && card.Player != player)
			return false;
		if (!inPlay && !(ability.UsableFromDiscard && player.ControlsAllDiscards && _viewedDiscardOwner != null))
			return false;

		// Поворот и «соседняя линия» требуют, чтобы карта стояла на поле.
		if (ability.ActivationTapsSelf && (!inPlay || card.IsTapped))
			return false;
		if (ability.ActivationTarget is ActivationTarget.AdjacentCreature or ActivationTarget.AnyLane or ActivationTarget.ShotTarget
			&& LaneOf(card) == null)
			return false;

		return ability.ActivationCondition?.Invoke(Context(card, player, null)) ?? true;
	}

	private void OnActivateRequested(Card card, string abilityId)
	{
		AbilityData ability = AbilitiesData.Get(abilityId);
		if (!CanActivate(card, ability))
			return;

		// Карта из сброса показана в окне, которое скоро закроется и удалит её.
		// Поэтому делаем отдельную копию, она живёт до конца оплаты.
		Card source = card;
		bool temp = !IsInPlay(card);
		if (temp)
		{
			source = CardScene.Instantiate<Card>();
			source.Setup(card.Data.Id);
			source.Player = card.Player;
			_discardView.Close();
		}

		PlayerState player = ActingPlayer;
		if (ability.ActivationTarget == ActivationTarget.None)
		{
			PayForActivation(source, ability, player, null, null, temp);
		}
		else
		{
			_targeting = new Targeting(source, ability, player, temp);
			ShowHint($"«{ability.Name}»: {TargetPrompt(ability.ActivationTarget)}\nОтмена: клик по карте ещё раз или Esc.");
		}

		RefreshView();
	}

	private static string TargetPrompt(ActivationTarget target) => target switch
	{
		ActivationTarget.AnyLane => "кликните по линии.",
		ActivationTarget.AnyNonHeroCreature => "кликните по существу (не герою).",
		ActivationTarget.AdjacentCreature => "кликните по существу на соседней линии.",
		ActivationTarget.ShotTarget => "кликните по вражескому существу в пределах дистанции.",
		_ => "",
	};

	private void PayForActivation(Card source, AbilityData ability, PlayerState player, Card target, Lane lane, bool temp)
	{
		StartPayment(source, player, ability.ActivationCost, ability.ActivationLaborCost, fromHand: false, tempSource: temp,
			onComplete: () =>
			{
				if (ability.ActivationTapsSelf)
					source.SetTapped(true);
				ability.OnActivated(Context(source, player, target, lane));
			});
	}

	// ───────────── Выбор цели ─────────────

	private void OnTargetClicked(Card card)
	{
		if (card == _targeting.Source)
		{
			CancelTargeting();
			return;
		}

		TryChooseTarget(LaneOf(card), card.IsCreature ? card : null);
	}

	private void OnLaneGuiInput(Lane lane, InputEvent @event)
	{
		if (_targeting != null && @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			TryChooseTarget(lane, null);
	}

	private void TryChooseTarget(Lane lane, Card creature)
	{
		Targeting t = _targeting;
		if (lane == null || !IsValidTarget(t, lane, creature))
			return;

		_targeting = null;
		ShowHint("");
		PayForActivation(t.Source, t.Ability, t.Player, creature, lane, t.TempSource);
		RefreshView();
	}

	private bool IsValidTarget(Targeting t, Lane lane, Card creature) => t.Ability.ActivationTarget switch
	{
		ActivationTarget.AnyLane => lane != LaneOf(t.Source),
		ActivationTarget.AnyNonHeroCreature => creature is { IsHero: false },
		ActivationTarget.AdjacentCreature => creature != null && LaneOf(t.Source).IsAdjacentTo(lane),
		ActivationTarget.ShotTarget => creature != null
			&& creature.Player != t.Source.Player
			&& Math.Abs(LaneOf(t.Source).Row - lane.Row) <= t.Ability.Shot.Distance,
		_ => false,
	};

	private void CancelTargeting()
	{
		if (_targeting.TempSource)
			_targeting.Source.Free();

		_targeting = null;
		ShowHint("");
		RefreshView();
	}

	// ───────────── Сброс ─────────────

	/// <summary>Клик по карте в окне сброса или колоды.</summary>
	private void OnViewedCardClicked(Card card)
	{
		// Способности из сброса — только пока открыт сброс, не колода.
		_cardDetails.Open(card, _viewedDiscardOwner != null ? ActivatableAbilities(card) : null);
	}

	/// <summary>Сброс под контролем игрока: свой всегда, чужой — пока действует «Хозяин кладбища».</summary>
	private static bool Controls(PlayerState player, PlayerState discardOwner) =>
		player == discardOwner || player.ControlsAllDiscards;

	/// <summary>Барон Суббота: вместо добора можно взять случайную карту из подконтрольного сброса.</summary>
	private bool CanTakeRandomFrom(PlayerState discardOwner)
	{
		PlayerState player = ActingPlayer;
		return CanDraw(player)
			&& Controls(player, discardOwner)
			&& discardOwner.Discard.Count > 0
			&& AllCreatures.Any(c => c.Player == player && c.HasAny(a => a.DrawFromDiscard));
	}

	private void OnTakeRandomFromDiscard()
	{
		PlayerState owner = _viewedDiscardOwner;
		if (owner == null || !CanTakeRandomFrom(owner))
			return;

		PlayerState player = ActingPlayer;
		int index = Random.Shared.Next(owner.Discard.Count);
		string id = owner.Discard[index];
		owner.Discard.RemoveAt(index);

		CreateCard(id, player, player.Hand);
		player.HasDrawnThisTurn = true;
		ShowHint($"{player.Name} взял из сброса «{CardsData.Get(id).Name}».");
		RefreshView();
	}

	// ───────────── Победа ─────────────

	/// <summary>Погиб герой: если других героев у игрока нет (ни на поле, ни в руке, ни в колоде), он проиграл.</summary>
	private void CheckHeroDefeat(IEnumerable<PlayerState> lostHero)
	{
		List<PlayerState> losers = lostHero.Where(p => !HasAnyHero(p)).ToList();

		if (losers.Count == 2)
			EndGame(null, "Оба игрока потеряли всех героев.");
		else if (losers.Count == 1)
			EndGame(OpponentOf(losers[0]), $"У игрока «{losers[0].Name}» не осталось героев.");
	}

	private bool HasAnyHero(PlayerState player) =>
		player.Deck.Any(id => CardsData.Get(id).IsHero)
		|| player.Hand.GetChildren().OfType<Card>().Any(c => c.IsHero)
		|| AllCreatures.Any(c => c.Player == player && c.IsHero && !c.IsDead)
		|| (_pending is { FromHand: true } && _pending.Player == player && _pending.Card.IsHero);

	/// <summary>
	/// Захват территории, в начале твоей фазы снабжения: на первой линии соперника твоих существ
	/// хотя бы одно и не меньше, чем вдвое больше, чем его существ там же.
	/// </summary>
	private void CheckTerritoryCapture()
	{
		PlayerState opponent = OtherPlayer;
		Lane lane = opponent.NearestLane;
		int mine = lane.Creatures.Count(c => c.Player == _currentPlayer);
		int theirs = lane.Creatures.Count(c => c.Player == opponent);

		if (mine > 0 && mine >= theirs * 2)
			EndGame(_currentPlayer, $"Захват территории: {mine} против {theirs} на первой линии соперника.");
	}

	private void EndGame(PlayerState winner, string reason)
	{
		if (_gameEnded)
			return;

		_gameEnded = true;
		_gameOverText.Text = winner == null ? "Ничья" : $"Победил {winner.Name}";
		_gameOverReason.Text = reason;
		_gameOver.Show();
	}

	/// <summary>Способность ждёт, пока игрок кликнет по цели.</summary>
	private sealed class Targeting(Card source, AbilityData ability, PlayerState player, bool tempSource)
	{
		public Card Source { get; } = source;
		public AbilityData Ability { get; } = ability;
		public PlayerState Player { get; } = player;

		/// <summary>Source — копия карты из сброса, её нужно удалить при отмене.</summary>
		public bool TempSource { get; } = tempSource;
	}
}
