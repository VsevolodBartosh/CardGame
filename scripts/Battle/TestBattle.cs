using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardGame;

/// <summary>
/// Тестовый бой двух игроков за одним экраном (по очереди).
/// Игрок 1 внизу, Игрок 2 вверху. Корень сцены test_battle.tscn.
///
/// Класс большой, поэтому разбит на несколько файлов (partial class):
///   TestBattle.cs           — поле, розыгрыш, нападение, оплата, фазы, экран;
///   TestBattle.Draft.cs     — начало игры: драфт героев и карт;
///   TestBattle.Abilities.cs — действия для способностей, активация, победа.
/// </summary>
public partial class TestBattle : Control
{
	private const int StartingHandSize = 4;

	private static readonly Color AttackerColor = new(0.95f, 0.15f, 0.15f);
	private static readonly Color TargetColor = new(1f, 0.6f, 0.1f);
	private static readonly Color DefenderColor = new(0.3f, 0.6f, 1f);
	private static readonly Color BottomPlayerColor = new(0.3f, 0.85f, 0.4f);
	private static readonly Color TopPlayerColor = new(0.9f, 0.3f, 0.3f);
	private static readonly Color SelectedColor = new(1f, 0.85f, 0.2f);

	/// <summary>Сцена карты (test_card.tscn). Назначена в инспекторе.</summary>
	[Export]
	public PackedScene CardScene { get; set; }

	private PlayerState _bottomPlayer;
	private PlayerState _topPlayer;

	/// <summary>Чей сейчас ход (не фаза!). Меняется только после фазы трофеев.</summary>
	private PlayerState _currentPlayer;
	private BattlePhase _phase;

	private readonly List<Attack> _attacks = [];

	/// <summary>Идёт оплата (мана, труд). null — никто ничего не оплачивает.</summary>
	private PendingPayment _pending;

	/// <summary>Карта, которую сейчас тащат: её оригинал полупрозрачный.</summary>
	private Card _draggedCard;

	/// <summary>Чей сброс открыт в окне. null — окно закрыто или в нём колода.</summary>
	private PlayerState _viewedDiscardOwner;

	/// <summary>Линии, где в этом ходу умирали существа (для Енота грабителя).</summary>
	private readonly HashSet<Lane> _deathLanesThisTurn = [];

	/// <summary>Игрок отвечает на вопрос в окне выбора (как оплатить, кого изгнать).</summary>
	private bool _waitingForChoice;

	/// <summary>Сквозной номер фазы с начала игры: так видно, какая фаза была «прошлой».</summary>
	private int _phaseCounter;

	private CardDetails _cardDetails;
	private DiscardView _discardView;
	private Label _turnLabel;
	private Label _hintLabel;
	private Button _nextPhaseButton;
	private Control _paymentPrompt;
	private Label _paymentText;
	private Container _paymentSlot;
	private Button _paymentCancelButton;
	private Button _paymentDoneButton;
	private ChoiceView _choiceView;

	private PlayerState OtherPlayer => _currentPlayer == _bottomPlayer ? _topPlayer : _bottomPlayer;

	/// <summary>Кто действует в текущей фазе: в диверсии и защите это соперник.</summary>
	private PlayerState ActingPlayer =>
		_phase is BattlePhase.Diversion or BattlePhase.Defense ? OtherPlayer : _currentPlayer;

	private IEnumerable<PlayerState> Players => [_bottomPlayer, _topPlayer];
	private IEnumerable<Lane> AllLanes => Players.SelectMany(p => p.Lanes);
	private IEnumerable<Card> AllCreatures => AllLanes.SelectMany(l => l.Creatures);

	/// <summary>Пока идёт выбор цели, оплата или игра окончена, остальные действия заблокированы.</summary>
	private bool IsBusy => _pending != null || _targeting != null || _gameEnded || _waitingForChoice;

	public override void _Ready()
	{
		// _bottomPlayer и _topPlayer — это «Игрок 1» и «Игрок 2» по правилам (ряды 2–3 и 0–1).
		// На экране свой игрок всегда внизу: у Игрока 2 в онлайне узлы «Player…» и «Enemy…» меняются местами.
		bool flip = GameSession.Online && GameSession.LocalPlayerIndex == 1;
		string first = flip ? "Enemy" : "Player";
		string second = flip ? "Player" : "Enemy";

		_bottomPlayer = CreatePlayer("Игрок 1", first);
		_topPlayer = CreatePlayer("Игрок 2", second);
		_localPlayer = GameSession.Online ? (flip ? _topPlayer : _bottomPlayer) : null;

		// Ряды по правилам сверху вниз (0..3). Соседние линии отличаются номером ряда на 1,
		// поэтому линии 1 и 2 (центр поля) — соседние линии разных игроков.
		_topPlayer.Lanes.Add(new Lane(_topPlayer, 1, GetNode<PanelContainer>($"Lanes/{second}Lane1")));
		_topPlayer.Lanes.Add(new Lane(_topPlayer, 0, GetNode<PanelContainer>($"Lanes/{second}Lane2")));
		_bottomPlayer.Lanes.Add(new Lane(_bottomPlayer, 2, GetNode<PanelContainer>($"Lanes/{first}Lane1")));
		_bottomPlayer.Lanes.Add(new Lane(_bottomPlayer, 3, GetNode<PanelContainer>($"Lanes/{first}Lane2")));

		// Drag and drop: линия сама по себе не умеет принимать карты,
		// поэтому «пересылаем» проверку и сброс карты в методы боя.
		foreach (Lane lane in AllLanes)
		{
			lane.Panel.SetDragForwarding(
				new Callable(),
				Callable.From((Vector2 _, Variant data) => CanDrop(data.Obj as Card, lane, null)),
				Callable.From((Vector2 _, Variant data) => UiDrop(data.Obj as Card, lane, null)));
			// Клик по пустому месту линии нужен, когда способность просит выбрать линию.
			lane.Panel.GuiInput += @event => UiLaneInput(lane, @event);
		}

		_cardDetails = GetNode<CardDetails>("CardDetails");
		_discardView = GetNode<DiscardView>("DiscardView");
		_turnLabel = GetNode<Label>("TurnLabel");
		_hintLabel = GetNode<Label>("HintLabel");
		_nextPhaseButton = GetNode<Button>("NextPhaseButton");
		_paymentPrompt = GetNode<Control>("PaymentPrompt");
		_paymentText = GetNode<Label>("PaymentPrompt/Row/Column/Text");
		_paymentSlot = GetNode<Container>("PaymentPrompt/Row/Slot");
		_paymentCancelButton = GetNode<Button>("PaymentPrompt/Row/Column/Buttons/CancelButton");
		_paymentDoneButton = GetNode<Button>("PaymentPrompt/Row/Column/Buttons/DoneButton");
		_choiceView = GetNode<ChoiceView>("ChoiceView");

		// Кнопки и клики игрока не меняют бой напрямую, а отправляют команду (Ui… → Send).
		// Так в онлайне одно и то же действие выполняется у обоих игроков. Подробнее в TestBattle.Network.cs.
		foreach (PlayerState player in Players)
		{
			player.DeckButton.Pressed += () => UiDeckPressed(player);
			player.DeckButton.GuiInput += @event => OnDeckGuiInput(player, @event);
			player.DiscardButton.Pressed += () => OnDiscardPressed(player);
			player.ManaPool.ManaPressed += type => UiManaPressed(player, type);
		}

		_nextPhaseButton.Pressed += UiNextPhase;
		_paymentCancelButton.Pressed += UiCancelPayment;
		_paymentDoneButton.Pressed += UiPaymentDone;
		_discardView.CardClicked += OnViewedCardClicked;
		_discardView.TakeRandomRequested += UiTakeRandom;
		_cardDetails.ActivateRequested += UiActivate;
		ReadyAbilitiesUi();

		_currentPlayer = _bottomPlayer;
		_phase = BattlePhase.Supply;
		RefreshView();

		// Сначала драфт, бой начнётся, когда он закончится. В онлайне — когда оба игрока загрузили бой.
		ReadyNetwork();
	}

	private PlayerState CreatePlayer(string name, string nodePrefix) => new(
		name,
		GetNode<HBoxContainer>($"{nodePrefix}Hand/Cards"),
		GetNode<HBoxContainer>($"{nodePrefix}Advantages/Column/Cards"),
		GetNode<Button>($"{nodePrefix}Deck"),
		GetNode<Button>($"{nodePrefix}Discard"),
		GetNode<ManaPoolView>($"{nodePrefix}ManaPool"));

	/// <summary>Цвет рамки по стороне экрана: свои карты зелёные, чужие красные (в тестовой битве — Игрок 1 зелёный).</summary>
	private Color ColorFor(PlayerState player) =>
		player == (_localPlayer ?? _bottomPlayer) ? BottomPlayerColor : TopPlayerColor;

	// ───────────── Колоды и карты ─────────────

	/// <summary>
	/// Сколько карт оплаты нужно колоде: по каждому типу маны половина
	/// от суммарной стоимости, с округлением вверх. Труд в расчёт не входит.
	/// </summary>
	private static List<string> PaymentCardsFor(IEnumerable<string> deck)
	{
		List<ManaCost> costs = deck.SelectMany(id => CardsData.Get(id).Cost).ToList();

		// 1. Сначала части цены без выбора: «1 еда», «2 огня».
		var fixedNeed = new Dictionary<ManaType, int>();
		foreach (ManaCost cost in costs.Where(c => c.Types.Length == 1))
			fixedNeed[cost.Types[0]] = fixedNeed.GetValueOrDefault(cost.Types[0]) + cost.Amount;

		// 2. Части с выбором («еда или золото») считаем за тот тип,
		//    который нужнее всего колоде. При равенстве — первый в списке.
		var totalNeed = new Dictionary<ManaType, int>(fixedNeed);
		foreach (ManaCost cost in costs.Where(c => c.Types.Length > 1))
		{
			ManaType main = cost.Types.MaxBy(t => fixedNeed.GetValueOrDefault(t));
			totalNeed[main] = totalNeed.GetValueOrDefault(main) + cost.Amount;
		}

		// 3. Половина с округлением вверх: (need + 1) / 2 в целых числах.
		var result = new List<string>();
		foreach ((ManaType type, int need) in totalNeed)
		{
			CardData payment = CardsData.FindPayment(type);
			if (payment == null)
				continue;

			for (int i = 0; i < (need + 1) / 2; i++)
				result.Add(payment.Id);
		}

		return result;
	}

	/// <summary>Берёт случайную карту из колоды игрока и кладёт её в руку.</summary>
	private void DrawCard(PlayerState player)
	{
		if (player.Deck.Count == 0)
			return;

		int index = _rng.Next(player.Deck.Count);
		string cardId = player.Deck[index];
		player.Deck.RemoveAt(index);

		CreateCard(cardId, player, player.Hand);
	}

	/// <summary>Создаёт карту боя: со всеми подписками на клики и перетаскивание.</summary>
	private Card CreateCard(string cardId, PlayerState player, Node parent)
	{
		var card = CardScene.Instantiate<Card>();
		card.Setup(cardId);
		RegisterCard(card); // Номер карты, одинаковый у обоих игроков (для команд по сети).
		card.Clicked += UiCardClicked;
		card.DoubleClicked += UiCardDoubleClicked;
		card.RightClicked += UiCardLongPressed;
		card.DelayClicks = true;
		card.AttachmentClicked += attachment => _cardDetails.Open(attachment);
		card.SetDragForwarding(
			Callable.From((Vector2 _) => StartDrag(card)),
			Callable.From((Vector2 _, Variant data) => CanDrop(data.Obj as Card, LaneOf(card), card)),
			Callable.From((Vector2 _, Variant data) => UiDrop(data.Obj as Card, LaneOf(card), card)));
		card.Player = player;
		card.OwnerPlayer = player;
		parent.AddChild(card);
		// Рамка цвета стороны: так видно, чья карта, даже на чужой линии.
		card.SetBaseBorder(ColorFor(player));
		return card;
	}

	private void OnDeckPressed(PlayerState player)
	{
		if (!CanDraw(player) || player.Deck.Count == 0)
			return;

		DrawCard(player);
		player.HasDrawnThisTurn = true;
		RefreshView();
	}

	/// <summary>Брать можно раз за свой ход, в любой из своих фаз.</summary>
	private bool CanDraw(PlayerState player) =>
		!IsBusy
		&& player == _currentPlayer
		&& player == ActingPlayer
		&& !player.HasDrawnThisTurn;

	// Посмотреть колоду: правый клик мышью или зажатие (на телефоне). Левый клик по-прежнему берёт карту.
	// GuiInput приходит и выключенной кнопке, поэтому смотреть можно, даже когда брать нельзя.
	private void OnDeckGuiInput(PlayerState player, InputEvent @event)
	{
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
		{
			ShowDeck(player);
			return;
		}

		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
		{
			if (mouse.Pressed)
				WaitDeckLongPress(player);
			else
				_deckPressToken++; // Отпустили раньше — это обычный клик.
		}
	}

	// Номер нажатия на колоду: если кнопку отпустили, номер меняется и зажатие не срабатывает.
	private int _deckPressToken;

	// Зажали колоду на LongPressSeconds — показываем её, а сам клик (добор) отменяем.
	private async void WaitDeckLongPress(PlayerState player)
	{
		int token = ++_deckPressToken;
		await ToSignal(GetTree().CreateTimer(Card.LongPressSeconds), SceneTreeTimer.SignalName.Timeout);
		if (token != _deckPressToken)
			return;

		_suppressDeckPress = true;
		ShowDeck(player);
	}

	// Сработало зажатие: следующее «нажатие» кнопки колоды — это отпускание после зажатия, не добор.
	private bool _suppressDeckPress;

	private void ShowDeck(PlayerState player)
	{
		// Только владельцу, иначе соперник увидит чужую колоду. В тестовой битве — тому, кто сейчас действует.
		PlayerState viewer = _localPlayer ?? ActingPlayer;
		if (player != viewer || player.Deck.Count == 0)
			return;

		_viewedDiscardOwner = null;
		_discardView.Open($"Колода: {player.Name}", SortForView(player.Deck), CardScene, player);
	}

	/// <summary>
	/// Порядок для просмотра: по типу карты (как в CardType), затем по id.
	/// Id сравниваются «по-человечески»: card_2 раньше card_10.
	/// </summary>
	private static IEnumerable<string> SortForView(IEnumerable<string> ids) =>
		ids.OrderBy(id => CardsData.Get(id).Types.FirstOrDefault())
			.ThenBy(id => id.TrimEnd("0123456789".ToCharArray()), StringComparer.Ordinal)
			.ThenBy(id => int.TryParse(id[id.TrimEnd("0123456789".ToCharArray()).Length..], out int n) ? n : 0);

	private void OnDiscardPressed(PlayerState player)
	{
		if (player.Discard.Count == 0)
			return;

		_viewedDiscardOwner = player;
		bool canTake = IsLocal(ActingPlayer) && CanTakeRandomFrom(player);
		_discardView.Open($"Сброс: {player.Name}", player.Discard, CardScene, player, canTake);
	}

	/// <summary>
	/// Клик по карте — действие в игре? (выбор цели, отмена активации, сброс или жертва в оплату, труд).
	/// Если нет, клик просто открывает подробности карты.
	/// </summary>
	private bool IsGameClick(Card card)
	{
		if (_targeting != null)
			return true;
		if (_pending == null)
			return false;

		return (!_pending.FromHand && card == _pending.Card)
			|| CanSelectForDiscard(card)
			|| CanSelectForSacrifice(card)
			|| CanPayLabor(card);
	}

	/// <summary>Кто сейчас «отвечает» кликами: выбирает цель или платит.</summary>
	private PlayerState ClickingPlayer => _targeting?.Player ?? _pending?.Player;

	/// <summary>Выполнить игровой клик по карте (у обоих игроков, по команде).</summary>
	private void ExecuteCardClick(Card card)
	{
		// Идёт выбор цели способности: клик выбирает цель или (по самой карте) отменяет.
		if (_targeting != null)
		{
			OnTargetClicked(card);
			return;
		}

		if (_pending == null)
			return;

		// Активацию отменяют повторным кликом по той же карте.
		if (!_pending.FromHand && card == _pending.Card)
			CancelPayment();
		// Клик по карте в руке выбирает её для сброса, по своему существу — жертва (цена X) или труд.
		else if (CanSelectForDiscard(card))
			ToggleDiscardSelection(card);
		else if (CanSelectForSacrifice(card))
			ToggleSacrificeSelection(card);
		else if (CanPayLabor(card))
			PayLabor(card);
	}

	/// <summary>Подробности карты. Кнопки способностей — только тому, кто сейчас может их нажать.</summary>
	private void ShowDetails(Card card)
	{
		// Рубашку соперника открыть нельзя, иначе он увидит чужие карты.
		if (card.IsFaceDown)
			return;

		_cardDetails.Open(card, IsLocal(ActingPlayer) ? ActivatableAbilities(card) : null);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Esc отменяет выбор цели или оплату способности.
		if (!@event.IsActionPressed("ui_cancel"))
			return;

		bool canCancel = _targeting != null || _pending is { FromHand: false };
		if (!canCancel || !IsLocal(ClickingPlayer))
			return;

		Send(BattleCommand.Of("cancel"));
		GetViewport().SetInputAsHandled();
	}

	/// <summary>Отмена выбора цели или оплаты способности (у обоих игроков, по команде).</summary>
	private void ExecuteCancel()
	{
		if (_targeting != null)
			CancelTargeting();
		else if (_pending is { FromHand: false })
			CancelPayment();
	}

	// ───────────── Drag and drop ─────────────

	// Вызывается Godot, когда игрок начал тащить карту.
	// Вернуть пустой Variant значит «эту карту тащить нельзя».
	private Variant StartDrag(Card card)
	{
		// Тащить может только тот, кто сидит за этим экраном и сейчас действует.
		if (!IsLocal(ActingPlayer) || !CanDrag(card))
			return default;

		// Превью рисуется от курсора вправо-вниз. Сдвигаем его,
		// чтобы курсор оказался в центре карты.
		var preview = CardScene.Instantiate<Card>();
		preview.Setup(card.Data.Id);
		preview.Modulate = new Color(1, 1, 1, 0.85f);
		preview.Position = -card.Size / 2;
		var holder = new Control();
		holder.AddChild(preview);
		card.SetDragPreview(holder);

		_draggedCard = card;
		card.Modulate = new Color(1, 1, 1, 0.4f);
		return Variant.From(card);
	}

	public override void _Notification(int what)
	{
		// Перетаскивание закончилось (картой куда-то попали или отпустили мимо).
		if (what == NotificationDragEnd && _draggedCard != null)
		{
			if (IsInstanceValid(_draggedCard))
				_draggedCard.Modulate = Colors.White;
			_draggedCard = null;
		}
	}

	// Сам корень сцены — это «поле»: сюда падает карта, брошенная мимо линий и рук.
	public override bool _CanDropData(Vector2 atPosition, Variant data) => CanDrop(data.Obj as Card, null, null);

	public override void _DropData(Vector2 atPosition, Variant data) => Drop(data.Obj as Card, null, null);

	private bool CanDrag(Card card)
	{
		if (IsBusy)
			return false;

		PlayerState acting = ActingPlayer;
		// Существ — только в снабжение, если у них нет «Мига».
		if (card.GetParent() == acting.Hand)
			return !card.IsCreature || _phase == BattlePhase.Supply || card.HasAny(a => a.Flash);

		Lane lane = LaneOf(card);
		if (lane == null)
			return false;

		// Выпавшее снаряжение берёт любое существо действующего игрока на этой линии.
		if (card.Data.Is(CardType.Equipment))
			return lane.Creatures.Any(c => c.Player == acting);

		// Своё существо может стоять и на чужой линии, поэтому смотрим на хозяина карты.
		if (!card.IsCreature || card.Player != acting)
			return false;

		return _phase switch
		{
			BattlePhase.Attack => CanStartAttack(card),
			// Защищать может только существо, на которое не напали и которое ещё не защищает.
			BattlePhase.Defense => !_attacks.Any(a => a.TargetCard == card || a.Defender == card),
			_ => false,
		};
	}

	/// <summary>
	/// Обездвиженное и повёрнутое не нападают. Каждое существо нападает один раз,
	/// кроме существ с «Много целей»: они добавляют цели, пока нападают только на существ.
	/// </summary>
	private bool CanStartAttack(Card card)
	{
		if (card.IsImmobilized)
			return false;

		List<Attack> own = _attacks.Where(a => a.Attacker == card).ToList();
		if (own.Count == 0)
			return !card.IsTapped;

		return card.HasAny(a => a.MultiTarget) && own.All(a => a.TargetCard != null);
	}

	/// <summary>
	/// Можно ли бросить карту сюда. lane — линия под курсором (null — пустое поле),
	/// target — карта под курсором (null — не на карту).
	/// </summary>
	private bool CanDrop(Card dragged, Lane lane, Card target)
	{
		if (dragged == null || dragged == target)
			return false;

		// Карты в руках и преимуществах не цели.
		if (target != null && lane == null)
			return false;

		// Целью бывает только существо. Бросок на выпавшее снаряжение считаем броском на линию.
		if (target is { IsCreature: false })
			target = null;

		if (dragged.GetParent() == ActingPlayer.Hand)
			return CanPlayFromHand(dragged, lane, target);

		if (dragged.Data.Is(CardType.Equipment))
			return target != null && target.Player == ActingPlayer && LaneOf(target) == LaneOf(dragged);

		return _phase switch
		{
			BattlePhase.Attack => CanAttack(dragged, lane, target),
			BattlePhase.Defense => target != null && FindDefendableAttack(dragged, target) != null,
			_ => false,
		};
	}

	/// <summary>Куда можно разыграть карту — зависит от её типа.</summary>
	private bool CanPlayFromHand(Card card, Lane lane, Card target)
	{
		CardData data = card.Data;
		PlayerState player = ActingPlayer;

		if (data.Is(CardType.Creature))
		{
			// Легион можно бросить на любое существо: тогда он разыгрывается как чары.
			if (data.EnchantmentFormId != null && target != null)
				return true;

			return lane == player.NearestLane
				|| (player.CanPlayOnSecondLine && lane == player.SecondLane)
				|| (data.FreeAfterDeathThisTurn && lane != null && _deathLanesThisTurn.Contains(lane));
		}
		if (data.Is(CardType.Enchantment))
			return target != null;
		if (data.Is(CardType.Equipment))
			return lane == player.NearestLane; // На пустое место линии или на существо на ней.
		if (data.Is(CardType.Spell) && data.TargetsCreature)
			return target != null;

		// Оплата, преимущество, заклинание без цели — в любое место поля.
		return true;
	}

	private void Drop(Card dragged, Lane lane, Card target)
	{
		ShowHint("");
		if (target is { IsCreature: false })
			target = null;

		if (dragged.GetParent() == ActingPlayer.Hand)
			StartPlay(dragged, lane, target);
		else if (dragged.Data.Is(CardType.Equipment))
			target.Attach(dragged);
		else if (_phase == BattlePhase.Attack)
			StartAttack(dragged, lane, target);
		else if (_phase == BattlePhase.Defense)
			DeclareDefense(dragged, target);

		RefreshView();
	}

	// ───────────── Нападение и защита ─────────────

	/// <summary>
	/// Нападать можно на соседнюю линию (даже свою: так существо выходит вперёд)
	/// или на вражеское существо на своей или соседней линии.
	/// С полётом или скоростью — на 1 линию дальше.
	/// </summary>
	private bool CanAttack(Card attacker, Lane lane, Card target)
	{
		if (lane == null)
			return false;

		int range = attacker.HasAny(a => a.Flight || a.Speed) ? 2 : 1;
		int distance = Math.Abs(LaneOf(attacker).Row - lane.Row);

		// Существо с «Много целей» добирает цели: только существа, каждое один раз.
		bool alreadyAttacking = _attacks.Any(a => a.Attacker == attacker);
		if (alreadyAttacking && (target == null || _attacks.Any(a => a.Attacker == attacker && a.TargetCard == target)))
			return false;

		if (target == null)
			return distance >= 1 && distance <= range;

		return target.Player == OtherPlayer && distance <= range;
	}

	/// <summary>
	/// Объявить нападение. Стопка нападает целиком: каждая её карта, которая может, нападает на ту же цель.
	/// Если у существа «Лень», сначала нужно заплатить (за каждую карту стопки).
	/// </summary>
	private void StartAttack(Card attacker, Lane lane, Card target)
	{
		List<Card> attackers = StackOf(attacker).Where(c => CanStartAttack(c) && CanAttack(c, lane, target)).ToList();

		ManaCost[] laziness = attacker.Abilities.SelectMany(a => a.LazinessCost).ToArray();
		bool firstAttack = !_attacks.Any(a => a.Attacker == attacker);

		if (laziness.Length > 0 && firstAttack)
		{
			ManaCost[] total = attackers.SelectMany(_ => laziness).ToArray();
			StartPayment(attacker, attacker.Player, total, 0, fromHand: false,
				onComplete: () => attackers.ForEach(c => DeclareAttack(c, lane, target)));
			return;
		}

		attackers.ForEach(c => DeclareAttack(c, lane, target));
	}

	private void DeclareAttack(Card attacker, Lane lane, Card target)
	{
		_attacks.Add(new Attack(attacker, target, lane, LaneOf(attacker)));
		// Нападение на существо поворачивает нападающего.
		if (target == null)
			return;

		attacker.SetTapped(true);
		foreach (AbilityData ability in attacker.Abilities)
			ability.OnAttackDeclared?.Invoke(Context(attacker, attacker.Player, target));
	}

	/// <summary>
	/// Защита. Стопка защитников раздаётся по нападениям стопки нападающих:
	/// каждый защитник берёт себе одно ещё не заблокированное нападение.
	/// </summary>
	private void DeclareDefense(Card defender, Card attackerCard)
	{
		foreach (Card member in StackOf(defender).ToList())
		{
			Attack attack = FindDefendableAttack(member, attackerCard);
			if (attack == null)
				break;
			attack.Defender = member;
		}
	}

	/// <summary>
	/// Какое нападение этого нападающего может перехватить защитник. null — никакое.
	/// Обычно защищать можно, если цель нападения или сам нападающий стоят на линии защитника.
	/// Полёт: только с линии цели. Скорость: ещё и с линии, через которую нападающий проходит.
	/// </summary>
	private Attack FindDefendableAttack(Card defender, Card attackerCard)
	{
		Lane defenderLane = LaneOf(defender);

		return _attacks.FirstOrDefault(attack =>
		{
			if (!StackOf(attackerCard).Contains(attack.Attacker) || attack.Defender != null)
				return false;

			if (attack.Attacker.HasAny(a => a.Unblockable))
				return false;

			if (attack.Attacker.HasAny(a => a.Flight))
				return defenderLane == attack.TargetLane;

			bool canBlock = defenderLane == attack.TargetLane || defenderLane == attack.OriginLane;

			bool passesThrough = Math.Abs(attack.OriginLane.Row - attack.TargetLane.Row) == 2;
			if (attack.Attacker.HasAny(a => a.Speed) && passesThrough)
				canBlock |= defenderLane.Row == (attack.OriginLane.Row + attack.TargetLane.Row) / 2;

			return canBlock;
		});
	}

	/// <summary>
	/// Схватки в конце защиты.
	/// 1. Существа с «Отступлением», на которых напали без защитника, отходят назад.
	/// 2. Остальные бьются: напавший получает урон, равный защите того, с кем бьётся
	///    (повёрнутый урон защитой не наносит); второй получает урон, равный атаке напавшего.
	///    Блок уменьшает каждый полученный в схватке урон.
	/// 3. Погибшие уходят в сброс, победившие нападающие переходят на линию цели.
	/// </summary>
	private void ResolveFights()
	{
		var retreated = new HashSet<Card>();
		foreach (Attack attack in _attacks.Where(a => a.Defender == null && a.TargetCard != null))
		{
			Card target = attack.TargetCard;
			if (retreated.Contains(target) || (target.HasAbility(AbilitiesData.RetreatId) && TryRetreat(target)))
			{
				retreated.Add(target);
				attack.TargetRetreated = true;
			}
		}

		foreach (Attack attack in _attacks.Where(a => !a.TargetRetreated))
		{
			Card attacker = attack.Attacker;
			Card victim = attack.Defender ?? attack.TargetCard;

			// Нападение на линию без защиты обходится без схватки.
			// Если кто-то уже погиб в прошлой схватке, эта не состоится.
			if (victim == null || attacker.IsDead || victim.IsDead)
				continue;

			// «Много целей»: атака делится поровну между всеми целями, с округлением вниз.
			int targets = _attacks.Count(a => a.Attacker == attacker);
			int damageToVictim = attacker.Attack / targets;

			bool noDefenseDamage = victim.IsTapped || attacker.HasAny(a => a.IgnoresDefenseWhenAttacking);
			int damageToAttacker = AfterBlock(noDefenseDamage ? 0 : victim.Defense, attacker);
			damageToVictim = AfterBlock(damageToVictim, victim);

			// Первый удар: кто с ним убивает противника, сам урона не получает.
			// Если первый удар у обоих — он ни на кого не действует.
			bool attackerFirst = attacker.HasAny(a => a.FirstStrike);
			bool victimFirst = victim.HasAny(a => a.FirstStrike);
			if (attackerFirst && !victimFirst && damageToVictim >= victim.CurrentHealth)
				damageToAttacker = 0;
			else if (victimFirst && !attackerFirst && damageToAttacker >= attacker.CurrentHealth)
				damageToVictim = 0;

			ApplyDamage(attacker, damageToAttacker, victim);
			ApplyDamage(victim, damageToVictim, attacker);

			// Фари: после схватки оглушает того, с кем билась.
			if (attack.TargetCard != null && attacker.HasAny(a => a.StunsWhenAttacking) && !victim.IsDead)
				Stun(victim, 1);
		}

		CleanupDead();

		// Перемещаем после уборки погибших: только тогда видно, кто выжил.
		foreach (Attack attack in _attacks)
		{
			Card attacker = attack.Attacker;
			if (attacker.IsDead || _attacks.Count(a => a.Attacker == attacker) > 1)
				continue; // Нападавший на несколько целей остаётся на месте.

			if (ShouldMoveToTarget(attack))
				PlaceOnLane(attacker, attack.TargetLane);
			else if (attacker.HasAny(a => a.Flight) && attack.Defender is { IsDead: false })
				MoveOneStepToward(attacker, attack.TargetLane);
		}

		_attacks.Clear();
	}

	/// <summary>
	/// Весь урон существам идёт через этот метод: здесь запоминается, кто ударил
	/// (чтобы знать убийцу), и срабатывают свойства «когда получает урон».
	/// </summary>
	private void ApplyDamage(Card target, int amount, Card source)
	{
		if (amount <= 0)
			return;

		target.LastDamageSource = source;
		target.TakeDamage(amount);

		foreach (AbilityData ability in target.Abilities)
			ability.OnDamaged?.Invoke(Context(target, target.Player, source));
	}

	/// <summary>
	/// Поставить карту на линию: при розыгрыше, перемещении, отступлении.
	/// Если существо здесь ещё не было, срабатывают свойства «новая линия» (Фонарщик).
	/// </summary>
	private void PlaceOnLane(Card card, Lane lane)
	{
		if (card.GetParent() == null)
			lane.Cards.AddChild(card);
		else
			card.Reparent(lane.Cards, keepGlobalTransform: false);

		if (!card.IsCreature || !card.VisitedLanes.Add(lane))
			return;

		foreach (AbilityData ability in card.Abilities)
			ability.OnEnterNewLane?.Invoke(Context(card, card.Player, null, lane));
	}

	/// <summary>Блок уменьшает урон, но не ниже нуля.</summary>
	private static int AfterBlock(int damage, Card card) => Math.Max(0, damage - card.Block);

	/// <summary>Полёт: заблокированный, но не остановленный — всё равно продвигается на 1 линию.</summary>
	private void MoveOneStepToward(Card creature, Lane target)
	{
		Lane from = LaneOf(creature);
		int row = from.Row + Math.Sign(target.Row - from.Row);
		Lane next = AllLanes.First(l => l.Row == row);
		PlaceOnLane(creature, next);
	}

	/// <summary>Отойти на 1 линию назад, к своей руке. Если сзади линии нет — не выходит.</summary>
	private bool TryRetreat(Card creature)
	{
		int backRow = LaneOf(creature).Row + (creature.Player == _bottomPlayer ? 1 : -1);
		Lane back = AllLanes.FirstOrDefault(l => l.Row == backRow);
		if (back == null)
			return false;

		PlaceOnLane(creature, back);
		return true;
	}

	/// <summary>
	/// Напал на линию и никто не защитил — переходит на неё, даже если там стоят враги.
	/// Цель отступила — то же самое, как при нападении на линию без защиты.
	/// Напал на существо и сам его убил (без защитника) — переходит на его линию.
	/// Напал на линию, а защитника убил — тоже переходит: его больше никто не держит.
	/// Иначе остаётся на месте.
	/// </summary>
	private static bool ShouldMoveToTarget(Attack attack)
	{
		bool lineAttack = attack.TargetCard == null || attack.TargetRetreated;

		if (attack.Defender != null)
			return lineAttack && attack.Defender.IsDead;

		return lineAttack || attack.TargetCard.IsDead;
	}

	/// <summary>
	/// Убирает погибших существ. Чары с них уходят в сброс,
	/// снаряжение выпадает на линию, где стояло существо.
	/// Если погиб герой, проверяем, остались ли у игрока ещё герои.
	/// </summary>
	private void CleanupDead()
	{
		var lostHero = new HashSet<PlayerState>();

		// «Не может умереть»: от смертельного урона остаётся с 1 здоровьем.
		foreach (Card undying in AllCreatures.Where(c => c.IsDead && c.HasAny(a => a.CantDie)))
			undying.Heal(1);

		// ToList: снимаем копию, потому что в цикле убираем карты с линий.
		foreach (Card creature in AllCreatures.Where(c => c.IsDead).ToList())
		{
			Lane lane = LaneOf(creature);
			foreach (Card attachment in creature.Attachments.ToList())
			{
				creature.Detach(attachment);
				if (attachment.Data.Is(CardType.Equipment))
					lane.Cards.AddChild(attachment);
				else
					SendToDiscard(attachment);
			}

			if (creature.IsHero)
				lostHero.Add(creature.Player);

			// Кто нанёс последний урон, тот и убил: срабатывает его «когда убивает».
			if (creature.LastDamageSource is { IsCreature: true } killer && killer != creature)
			{
				foreach (AbilityData ability in killer.Abilities)
					ability.OnKill?.Invoke(Context(killer, killer.Player, creature));
			}

			_deathLanesThisTurn.Add(lane);

			// Кладбище хозяина существа забирает его себе вместо сброса. Чужое кладбище не трогает.
			Card graveyard = FindGraveyard(creature.OwnerPlayer ?? creature.Player);
			if (graveyard != null && !creature.Data.IsToken)
			{
				graveyard.StoredCards.Add(creature.Data.DiscardAsId ?? creature.Data.Id);
				creature.GetParent()?.RemoveChild(creature);
				creature.QueueFree();
				graveyard.Heal(0); // Обновить отметку [N] на карте.
			}
			else
			{
				SendToDiscard(creature);
			}
		}

		CheckHeroDefeat(lostHero);
	}

	/// <summary>
	/// Заклинание может убить существо посреди атаки или защиты.
	/// Нападения погибших и на погибших отменяются, погибший защитник просто перестаёт защищать.
	/// </summary>
	private void ForgetDeadInAttacks()
	{
		_attacks.RemoveAll(a => a.Attacker.IsDead || a.TargetCard is { IsDead: true });
		foreach (Attack attack in _attacks.Where(a => a.Defender is { IsDead: true }))
			attack.Defender = null;
	}

	// ───────────── Розыгрыш ─────────────

	/// <summary>
	/// Розыгрыш из руки. async: иногда нужно спросить игрока (кого изгнать для скидки Зомби,
	/// каким способом платить), и метод ждёт ответа через await.
	/// </summary>
	private async void StartPlay(Card card, Lane lane, Card target)
	{
		PlayerState player = ActingPlayer;
		CardData data = card.Data;

		// Цена X: отдельный режим оплаты с кнопкой «Готово».
		if (data.XCost)
		{
			StartXPayment(card, player, x => ResolvePlay(card, player, lane, target, x));
			return;
		}

		_waitingForChoice = true;
		try
		{
			// Легион: брошен на существо — можно разыграть как чары (отдельная карта «Легион (чары)»).
			if (data.EnchantmentFormId != null && target != null)
			{
				bool asCreature = lane == player.NearestLane || (player.CanPlayOnSecondLine && lane == player.SecondLane);
				bool asEnchantment = !asCreature
					|| await ChooseAsync(player, $"Как разыграть «{data.Name}»?", ["Как существо", "Как чары"]) == 1;
				if (asEnchantment)
				{
					card = SwapCard(card, data.EnchantmentFormId);
					data = card.Data;
				}
			}

			// Скидка Зомби: изгнать до N существ из своего сброса, каждое −1 к цене.
			List<string> exiled = [];
			List<string> creaturesInDiscard = player.Discard.Where(id => CardsData.Get(id).Is(CardType.Creature)).ToList();
			if (data.ExileDiscountMax > 0 && creaturesInDiscard.Count > 0)
			{
				int max = Math.Min(data.ExileDiscountMax, creaturesInDiscard.Count);
				exiled = await PickAsync(player, $"изгнать до {max} существ из сброса (−1 к цене за каждое)",
					creaturesInDiscard, 0, max);
			}

			List<CostOption> options = CostOptionsFor(card, exiled.Count + card.CostDiscount)
				.Where(o => CanAfford(player, card, o))
				.ToList();

			if (options.Count == 0)
			{
				// Карту так и не забрали из руки, поэтому она просто остаётся там.
				ShowHint($"Не хватает маны на «{data.Name}».");
				return;
			}

			CostOption chosen = options.Count == 1
				? options[0]
				: options[await ChooseAsync(player, $"Как оплатить «{data.Name}»?", options.Select(o => o.ToString()).ToList())];

			StartPayment(card, player, chosen.Mana, chosen.Labor, fromHand: true, discard: chosen.Discard,
				onComplete: () =>
				{
					foreach (string id in exiled)
						player.Discard.Remove(id); // Изгнанные исчезают из игры совсем.
					ResolvePlay(card, player, lane, target, 0);
				});
		}
		finally
		{
			_waitingForChoice = false;
			RefreshView();
		}
	}

	/// <summary>
	/// Все способы оплатить карту: основной (Cost + LaborCost) и альтернативные.
	/// Сброс карт из DiscardCost добавляется к каждому способу.
	/// Енот грабитель после смерти существа в этом ходу — бесплатно.
	/// </summary>
	private List<CostOption> CostOptionsFor(Card card, int exileDiscount)
	{
		CardData data = card.Data;
		if (data.FreeAfterDeathThisTurn && _deathLanesThisTurn.Count > 0)
			return [new CostOption { Discard = data.DiscardCost }];

		var options = new List<CostOption>
		{
			new() { Mana = Discounted(data.Cost, exileDiscount), Labor = data.LaborCost, Discard = data.DiscardCost },
		};
		options.AddRange(data.AlternativeCosts.Select(o =>
			new CostOption { Mana = Discounted(o.Mana, exileDiscount), Labor = o.Labor, Discard = o.Discard + data.DiscardCost }));
		return options;
	}

	/// <summary>Уменьшить первую часть цены на discount (не ниже нуля).</summary>
	private static ManaCost[] Discounted(ManaCost[] cost, int discount)
	{
		if (discount == 0 || cost.Length == 0)
			return cost;

		ManaCost first = cost[0];
		int amount = Math.Max(0, first.Amount - discount);
		return [new ManaCost(amount, first.Types), .. cost.Skip(1)];
	}

	/// <summary>Хватит ли маны и карт в руке. Труд можно доплачивать позже, поэтому его не проверяем.</summary>
	private static bool CanAfford(PlayerState player, Card card, CostOption option)
	{
		int otherCardsInHand = player.Hand.GetChildren().OfType<Card>().Count(c => c != card);
		return ManaPayment.CanPay(ManaPayment.ToUnits(option.Mana), player.Mana) && otherCardsInHand >= option.Discard;
	}

	/// <summary>Карта оплачена: кладём её туда, куда положено её типу, и запускаем способности.</summary>
	private void ResolvePlay(Card card, PlayerState player, Lane lane, Card target, int x)
	{
		CardData data = card.Data;
		card.PlayedPhase = _phaseCounter;
		card.CostDiscount = 0;

		// «Каждый раз, когда вы разыгрываете карту» (Водный Элементаль). Сама карта себя не считает.
		foreach (Card watcher in AllCreatures.Where(c => c.Player == player && c != card).ToList())
		{
			foreach (AbilityData ability in watcher.Abilities)
				ability.OnOwnerPlayedCard?.Invoke(Context(watcher, player, card));
		}

		if (data.IsPayment || data.Is(CardType.Spell))
		{
			RunOnPlayed(card, player, target, x);
			if (data.ReturnsToDeck)
			{
				// Трюк пиромана: вместо сброса обратно в колоду (добор случайный, так что это и есть «замешать»).
				player.Deck.Add(data.Id);
				card.GetParent()?.RemoveChild(card);
				card.QueueFree();
			}
			else
			{
				SendToDiscard(card);
			}
			return;
		}

		if (data.Is(CardType.Enchantment))
			target.Attach(card);
		else if (data.Is(CardType.Equipment) && target != null)
			target.Attach(card);
		else if (data.Is(CardType.Advantage))
			card.Reparent(player.Advantages, keepGlobalTransform: false);
		else
			PlaceOnLane(card, lane);

		RunOnPlayed(card, player, target, x);
	}

	private void RunOnPlayed(Card card, PlayerState player, Card target, int x = 0)
	{
		foreach (AbilityData ability in card.Abilities)
			ability.OnPlayed?.Invoke(Context(card, player, target, x: x));
	}

	private AbilityContext Context(Card source, PlayerState player, Card target, Lane targetLane = null, int x = 0) =>
		new() { Player = player, Source = source, Target = target, TargetLane = targetLane, X = x, Battle = this };

	/// <summary>
	/// Карта уходит в сброс своего хозяина (не того, кто её сейчас контролирует).
	/// Чары Легиона уходят в сброс как обычный Легион (DiscardAsId).
	/// </summary>
	private static void SendToDiscard(Card card)
	{
		// Жетоны (Крысы) в сброс не попадают — просто исчезают.
		if (card.Data.IsToken)
		{
			card.GetParent()?.RemoveChild(card);
			card.QueueFree();
			return;
		}

		PlayerState owner = card.OwnerPlayer ?? card.Player;
		owner.Discard.Add(card.Data.DiscardAsId ?? card.Data.Id);
		card.GetParent()?.RemoveChild(card);
		card.QueueFree();
	}

	/// <summary>Кладбище этого игрока: оно забирает только его собственных существ. null — кладбища нет.</summary>
	private static Card FindGraveyard(PlayerState owner) =>
		owner.Advantages.GetChildren().OfType<Card>()
			.FirstOrDefault(c => c.HasAny(a => a.CollectsDeadCreatures));

	// ───────────── Оплата: мана, труд, сброс карт, цена X ─────────────

	/// <summary>
	/// Начать оплату. Где выбора нет, мана списывается сама. Если всё оплачено сразу,
	/// эффект срабатывает тут же; иначе карта ждёт в окне оплаты (из руки) или
	/// подсказка пишется справа (способность, нападение с «Ленью»).
	/// discard — сколько карт из руки нужно сбросить (игрок кликает по ним).
	/// tempSource — карта создана только для этой оплаты (способность из сброса), её удалим в конце.
	/// </summary>
	private void StartPayment(Card card, PlayerState player, IEnumerable<ManaCost> cost, int laborCost,
		bool fromHand, Action onComplete, bool tempSource = false, int discard = 0)
	{
		var pending = new PendingPayment(card, player, ManaPayment.ToUnits(cost), laborCost, fromHand, _ => onComplete(), tempSource)
		{
			DiscardNeeded = discard,
		};

		if (!ManaPayment.CanPay(pending.Units, player.Mana))
		{
			// Карту так и не забрали из руки, поэтому она просто остаётся там.
			ShowHint($"Не хватает маны на «{card.Data.Name}».");
			if (tempSource)
				card.Free();
			return;
		}

		_pending = pending;
		AutoPay(pending);

		if (pending.IsDone)
		{
			CompletePayment();
			return;
		}

		ShowPaymentPrompt(pending);
	}

	/// <summary>
	/// Цена X («Выпить любой ценой»): каждый клик по мане, по карте в руке (сброс)
	/// или по своему существу (жертва) добавляет 1 к X. Закончить — кнопка «Готово».
	/// </summary>
	private void StartXPayment(Card card, PlayerState player, Action<int> onComplete)
	{
		_pending = new PendingPayment(card, player, [], 0, fromHand: true, onComplete, tempSource: false) { XMode = true };
		ShowPaymentPrompt(_pending);
		RefreshView();
	}

	// Из руки карта «зависает» в окне оплаты. Окно встаёт поверх руки соперника
	// (там всё равно рубашки), чтобы не закрывать линии: по существам кликают за труд.
	// При активации окна нет: подсказка пишется справа, отмена — клик по карте.
	private void ShowPaymentPrompt(PendingPayment pending)
	{
		if (!pending.FromHand)
			return;

		var opponentHand = (Control)OpponentOf(pending.Player).Hand.GetParent();
		_paymentPrompt.Position = opponentHand.Position;
		_paymentPrompt.Size = opponentHand.Size;
		pending.Card.Reparent(_paymentSlot, keepGlobalTransform: false);

		// Окно с кнопками — только тому, кто платит. У соперника карта просто ждёт (подсказка справа).
		if (IsLocal(pending.Player))
			_paymentPrompt.Show();
	}

	/// <summary>Платит сам за те единицы маны, где выбора на самом деле нет.</summary>
	private static void AutoPay(PendingPayment pending)
	{
		bool paidSomething;
		do
		{
			paidSomething = false;
			for (int i = 0; i < pending.Units.Count; i++)
			{
				List<ManaType> choices = ManaPayment.ValidChoices(pending.Units, i, pending.Player.Mana);
				if (choices.Count == 1)
				{
					pending.Pay(i, choices[0]);
					paidSomething = true;
					break;
				}
			}
		}
		while (paidSomething);
	}

	private void OnManaPressed(PlayerState player, ManaType type)
	{
		if (_pending == null || _pending.Player != player)
			return;

		if (_pending.XMode)
		{
			// В цене X подходит любая мана: просто списываем ещё одну единицу.
			if (player.Mana.GetValueOrDefault(type) > 0)
				_pending.PayAny(type);
			RefreshView();
			return;
		}

		for (int i = 0; i < _pending.Units.Count; i++)
		{
			if (ManaPayment.ValidChoices(_pending.Units, i, player.Mana).Contains(type))
			{
				_pending.Pay(i, type);
				AutoPay(_pending);
				break;
			}
		}

		if (_pending.IsDone)
			CompletePayment();

		RefreshView();
	}

	/// <summary>Труд платят своими неповёрнутыми существами на поле.</summary>
	private bool CanPayLabor(Card worker) =>
		_pending.LaborRemaining > 0
		&& worker.IsCreature
		&& LaneOf(worker) != null
		&& worker.Player == _pending.Player
		&& !worker.IsTapped;

	private void PayLabor(Card worker)
	{
		// Усердный после нескольких оплат трудом даёт 2 труда за раз.
		int doubleAfter = worker.Abilities.Select(a => a.DoubleLaborAfter).FirstOrDefault(n => n > 0);
		int amount = doubleAfter > 0 && worker.LaborCount >= doubleAfter ? 2 : 1;

		worker.SetTapped(true);
		worker.LaborCount++;
		_pending.Card.AddLabor(amount);

		if (_pending.IsDone)
			CompletePayment();

		RefreshView();
	}

	/// <summary>Карта в руке плательщика, которую можно выбрать для сброса в оплату.</summary>
	private bool CanSelectForDiscard(Card card) =>
		card.GetParent() == _pending.Player.Hand
		&& (_pending.XMode || _pending.SelectedDiscards.Contains(card) || _pending.SelectedDiscards.Count < _pending.DiscardNeeded);

	private void ToggleDiscardSelection(Card card)
	{
		if (!_pending.SelectedDiscards.Remove(card))
			_pending.SelectedDiscards.Add(card);

		if (_pending.IsDone)
			CompletePayment();

		RefreshView();
	}

	/// <summary>Цена X: своё существо на поле можно пожертвовать вместо единицы маны.</summary>
	private bool CanSelectForSacrifice(Card card) =>
		_pending.XMode && card.IsCreature && LaneOf(card) != null && card.Player == _pending.Player;

	private void ToggleSacrificeSelection(Card card)
	{
		if (!_pending.SelectedSacrifices.Remove(card))
			_pending.SelectedSacrifices.Add(card);
		RefreshView();
	}

	private void OnPaymentDonePressed()
	{
		if (_pending is { XMode: true } && _pending.X >= 1)
			CompletePayment();
	}

	private void CompletePayment()
	{
		PendingPayment payment = _pending;
		_pending = null;
		_paymentPrompt.Hide();

		if (!payment.FromHand)
			ShowHint("");

		// Сброшенные карты уходят в сброс, пожертвованные существа погибают (уберутся в CleanupDead).
		int x = payment.X;
		foreach (Card discarded in payment.SelectedDiscards)
			SendToDiscard(discarded);
		foreach (Card sacrificed in payment.SelectedSacrifices)
			Kill(sacrificed);

		payment.Card.ResetLabor();
		payment.Card.LastPaidMana = payment.PaidMana;
		payment.OnComplete(x);
		if (payment.TempSource)
			payment.Card.Free();

		CleanupDead();
		ForgetDeadInAttacks();
		RefreshView();
	}

	/// <summary>
	/// Отменить оплату: мана возвращается в накопитель, карта — в руку, выбранные для сброса
	/// карты остаются в руке. Вложенный труд остаётся на карте: существа уже повёрнуты, доплатить можно позже.
	/// </summary>
	private void CancelPayment()
	{
		if (_pending == null)
			return;

		PendingPayment payment = _pending;
		_pending = null;
		_paymentPrompt.Hide();

		payment.Refund();
		if (payment.FromHand)
		{
			payment.Card.Reparent(payment.Player.Hand, keepGlobalTransform: false);
			// «Легион (чары)» вернулся в руку — снова становится обычным Легионом.
			if (payment.Card.Data.DiscardAsId != null)
				SwapCard(payment.Card, payment.Card.Data.DiscardAsId);
		}

		ShowHint("");
		if (payment.Card.LaborPaid > 0)
			ShowHint($"Труд на «{payment.Card.Data.Name}» сохранён: {payment.Card.LaborPaid} из {payment.LaborCost}.");

		if (payment.TempSource)
			payment.Card.Free();

		RefreshView();
	}

	// ───────────── Фазы ─────────────

	// async: в конце хода может понадобиться заплатить «Голод», и игра ждёт выбора игрока.
	private async void OnNextPhasePressed()
	{
		if (IsBusy)
			return;

		_cardDetails.Close();
		ShowHint("");
		_phaseCounter++;

		switch (_phase)
		{
			case BattlePhase.Defense:
				ResolveFights();
				_phase = BattlePhase.Trophy;
				RunTrophyEffects();
				break;
			case BattlePhase.Trophy:
				_waitingForChoice = true;
				try
				{
					await PayHunger();
				}
				finally
				{
					_waitingForChoice = false;
				}

				if (!_gameEnded)
					StartNextTurn();
				break;
			default:
				_phase++;
				break;
		}

		RefreshView();
	}

	/// <summary>Ход переходит к сопернику, он начинает со снабжения.</summary>
	private void StartNextTurn()
	{
		_currentPlayer = OtherPlayer;
		_currentPlayer.HasDrawnThisTurn = false;
		_phase = BattlePhase.Supply;

		// Эффекты «до конца хода» заканчиваются.
		foreach (PlayerState player in Players)
		{
			player.CanPlayOnSecondLine = false;
			player.ControlsAllDiscards = false;
		}

		_deathLanesThisTurn.Clear();

		// Бонусы «до конца хода» пропадают.
		foreach (Card creature in AllCreatures)
			creature.ClearTempBonuses();

		// Контроль «до конца хода» возвращается хозяину.
		foreach (Card creature in AllCreatures.Where(c => c.OwnerPlayer != null && c.Player != c.OwnerPlayer))
		{
			creature.Player = creature.OwnerPlayer;
			creature.SetBaseBorder(ColorFor(creature.Player));
		}

		UntapAll(_currentPlayer);

		// Обездвиживание длится до снабжения того, кто его наложил.
		foreach (Card creature in AllCreatures.Where(c => c.ImmobilizedBy == _currentPlayer))
			creature.SetImmobilized(null);

		// Захват проверяется в снабжение захватчика: у соперника был целый ход, чтобы ответить.
		CheckTerritoryCapture();
	}

	/// <summary>
	/// В снабжение игрок разворачивает все свои карты. Оглушённое существо вместо этого
	/// пропускает один разворот. Развернувшееся существо будит чары «когда развернётся».
	/// </summary>
	private void UntapAll(PlayerState player)
	{
		foreach (Card advantage in player.Advantages.GetChildren().OfType<Card>())
			advantage.SetTapped(false);

		foreach (Card creature in AllCreatures.Where(c => c.Player == player).ToList())
		{
			if (creature.IsStunned)
			{
				creature.SetStunTurns(creature.StunTurns - 1);
				continue;
			}

			if (!creature.IsTapped)
				continue;

			creature.SetTapped(false);
			foreach (Card attachment in creature.Attachments.ToList())
			{
				foreach (AbilityData ability in attachment.Abilities)
					ability.OnHostUntapped?.Invoke(Context(attachment, attachment.Player, creature));
			}
		}
	}

	/// <summary>
	/// Эффекты «в фазу трофеев». Чары и снаряжение срабатывают в трофеи владельца существа,
	/// к которому прикреплены (а не владельца самих чар). Преимущества — в трофеи своего владельца.
	/// </summary>
	private void RunTrophyEffects()
	{
		foreach (Card creature in AllCreatures.Where(c => c.Player == _currentPlayer).ToList())
		{
			// Свои эффекты существа (Водный Элементаль теряет здоровье).
			foreach (AbilityData ability in creature.Abilities)
				ability.OnTrophyPhase?.Invoke(Context(creature, creature.Player, null));

			if (creature.IsPoisoned)
				ApplyDamage(creature, 1, null);

			foreach (Card attachment in creature.Attachments)
			{
				// Player в контексте — хозяин чар: он их разыграл, даже если сработали они в чужой ход.
				foreach (AbilityData ability in attachment.Abilities)
					ability.OnTrophyPhase?.Invoke(Context(attachment, attachment.Player, creature));
			}
		}

		foreach (Card advantage in _currentPlayer.Advantages.GetChildren().OfType<Card>())
		{
			foreach (AbilityData ability in advantage.Abilities)
				ability.OnTrophyPhase?.Invoke(Context(advantage, _currentPlayer, null));
		}

		CleanupDead();
	}

	// ───────────── Экран ─────────────

	private Lane LaneOf(Card card) => AllLanes.FirstOrDefault(l => card.GetParent() == l.Cards);

	private void ShowHint(string text) => _hintLabel.Text = text;

	/// <summary>Приводит экран в соответствие с текущим состоянием боя.</summary>
	private void RefreshView()
	{
		PlayerState acting = ActingPlayer;
		Restack();

		foreach (PlayerState player in Players)
		{
			// В онлайне свою руку видит только её хозяин. В тестовой битве — тот, кто сейчас действует.
			bool handVisible = _localPlayer != null ? player == _localPlayer : player == acting;
			foreach (Card card in player.Hand.GetChildren().OfType<Card>())
				card.SetFaceDown(!handVisible);

			player.DeckButton.Text = $"Колода\n{player.Deck.Count}";
			player.DeckButton.Disabled = !CanDraw(player) || player.Deck.Count == 0 || !IsLocal(player);
			player.DiscardButton.Text = $"Сброс\n{player.Discard.Count}";
			player.DiscardButton.Disabled = player.Discard.Count == 0;

			player.ManaPool.Refresh(player.Mana);
			player.ManaPool.SetSelecting(_pending?.Player == player && (_pending.Units.Count > 0 || _pending.XMode));

			// Карты в руке, выбранные для сброса в оплату, подсвечены.
			foreach (Card card in player.Hand.GetChildren().OfType<Card>())
				card.SetHighlight(_pending != null && _pending.SelectedDiscards.Contains(card) ? SelectedColor : null);
		}

		foreach (Lane lane in AllLanes)
		{
			lane.SetHighlight(null);
			foreach (Card card in lane.AllCards)
			{
				card.SetHighlight(null);
				card.Redraw(); // Характеристики могли измениться от маны (Фанатики).
			}
		}

		foreach (Attack attack in _attacks)
		{
			attack.Attacker.SetHighlight(AttackerColor);
			if (attack.TargetCard != null)
				attack.TargetCard.SetHighlight(TargetColor);
			else
				attack.TargetLane.SetHighlight(TargetColor);
			attack.Defender?.SetHighlight(DefenderColor);
		}

		if (_pending != null)
		{
			foreach (Card sacrificed in _pending.SelectedSacrifices)
				sacrificed.SetHighlight(SelectedColor);

			if (IsLocal(_pending.Player))
				UpdatePaymentPrompt();
			else
				ShowRemotePaymentHint();
		}

		string you = _localPlayer != null ? $"\nВы: {_localPlayer.Name}" : "";
		_turnLabel.Text = $"Ход: {_currentPlayer.Name}\nФаза: {_phase.ToRussian()}\nДействует: {acting.Name}{you}";
		_nextPhaseButton.Text = _phase == BattlePhase.Trophy ? "Конец хода" : "Далее";
		_nextPhaseButton.Disabled = IsBusy || !IsLocal(acting);
	}

	private void UpdatePaymentPrompt()
	{
		var lines = new List<string> { $"{_pending.Player.Name}: оплата «{_pending.Card.Data.Name}»." };
		if (_pending.Units.Count > 0)
			lines.Add($"Мана: осталось {_pending.Units.Count}, выберите в накопителе.");
		if (_pending.LaborRemaining > 0)
			lines.Add($"Труд: осталось {_pending.LaborRemaining}, кликайте по своим неповёрнутым существам.");
		if (_pending.DiscardNeeded > _pending.SelectedDiscards.Count)
			lines.Add($"Сброс: выберите в руке ещё {_pending.DiscardNeeded - _pending.SelectedDiscards.Count} карт(у).");
		if (_pending.XMode)
			lines.Add($"X = {_pending.X}. +1: мана, карта из руки или своё существо.");

		_paymentDoneButton.Visible = _pending.XMode;
		_paymentDoneButton.Disabled = _pending.X < 1;

		if (_pending.FromHand)
		{
			_paymentText.Text = string.Join("\n", lines);
			return;
		}

		lines.Add("Отмена: клик по карте ещё раз или Esc.");
		ShowHint(string.Join("\n", lines));
	}

	/// <summary>У соперника идёт оплата: показываем только короткую подсказку.</summary>
	private void ShowRemotePaymentHint() =>
		ShowHint($"{_pending.Player.Name} оплачивает «{_pending.Card.Data.Name}»…");

	// ───────────── Вспомогательные классы ─────────────

	/// <summary>Одно нападение: кто, откуда, на кого (существо или линия) и кто защищает.</summary>
	private sealed class Attack(Card attacker, Card targetCard, Lane targetLane, Lane originLane)
	{
		public Card Attacker { get; } = attacker;

		/// <summary>Существо, на которое напали; null — напали на линию.</summary>
		public Card TargetCard { get; } = targetCard;

		/// <summary>Линия цели: линия существа или сама атакованная линия.</summary>
		public Lane TargetLane { get; } = targetLane;

		/// <summary>Линия, с которой напали. Нужна для правил блока (полёт, скорость).</summary>
		public Lane OriginLane { get; } = originLane;

		public Card Defender { get; set; }

		/// <summary>Цель ушла «Отступлением»: схватки не было.</summary>
		public bool TargetRetreated { get; set; }
	}

	/// <summary>
	/// Идёт оплата карты или способности. Мана списывается сразу, но при отмене возвращается.
	/// Труд копится на самой карте (Card.LaborPaid) и при отмене не пропадает.
	/// </summary>
	private sealed class PendingPayment(Card card, PlayerState player, List<ManaType[]> units, int laborCost,
		bool fromHand, Action<int> onComplete, bool tempSource)
	{
		private readonly List<ManaType> _paid = [];

		/// <summary>Карта, которую разыгрывают или чью способность активируют.</summary>
		public Card Card { get; } = card;

		public PlayerState Player { get; } = player;

		/// <summary>Неоплаченные единицы маны.</summary>
		public List<ManaType[]> Units { get; } = units;

		public int LaborCost { get; } = laborCost;

		public int LaborRemaining => Math.Max(0, LaborCost - Card.LaborPaid);

		/// <summary>true — разыгрываем из руки; false — способность или нападение карты в игре.</summary>
		public bool FromHand { get; } = fromHand;

		/// <summary>Что сделать, когда всё оплачено. Получает X (для обычной цены — 0).</summary>
		public Action<int> OnComplete { get; } = onComplete;

		/// <summary>Карта создана только для оплаты (способность из сброса) и удаляется в конце.</summary>
		public bool TempSource { get; } = tempSource;

		/// <summary>Сколько карт из руки нужно сбросить.</summary>
		public int DiscardNeeded { get; init; }

		/// <summary>Карты в руке, выбранные для сброса. Сбрасываются только при завершении оплаты.</summary>
		public List<Card> SelectedDiscards { get; } = [];

		/// <summary>Цена X: свои существа, выбранные в жертву.</summary>
		public List<Card> SelectedSacrifices { get; } = [];

		/// <summary>Цена X: игрок сам решает, когда хватит, и жмёт «Готово».</summary>
		public bool XMode { get; init; }

		/// <summary>Сколько всего заплачено единиц для цены X.</summary>
		public int X => _paid.Count + SelectedDiscards.Count + SelectedSacrifices.Count;

		public bool IsDone =>
			!XMode && Units.Count == 0 && LaborRemaining == 0 && SelectedDiscards.Count >= DiscardNeeded;

		public void Pay(int unitIndex, ManaType type)
		{
			Player.Mana[type]--;
			_paid.Add(type);
			Units.RemoveAt(unitIndex);
		}

		/// <summary>Какой маной уже заплачено.</summary>
		public IReadOnlyList<ManaType> PaidMana => _paid.ToList();

		/// <summary>Цена X: списать одну единицу маны любого типа.</summary>
		public void PayAny(ManaType type)
		{
			Player.Mana[type]--;
			_paid.Add(type);
		}

		/// <summary>Вернуть в накопитель всё, что уже списали.</summary>
		public void Refund()
		{
			foreach (ManaType type in _paid)
				Player.AddMana(type, 1);
			_paid.Clear();
		}
	}
}
