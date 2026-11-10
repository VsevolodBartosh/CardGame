using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace CardGame;

/// <summary>
/// Одно действие игрока, которое меняет бой: «бросил карту», «нажал Далее», «выбрал вариант 2».
/// Ints и Strings — параметры (номера карт, ряд линии, id способности…).
/// record — короткая запись класса-данных: конструктор, свойства и сравнение компилятор делает сам.
/// </summary>
public sealed record BattleCommand(string Type, int[] Ints, string[] Strings)
{
	public static BattleCommand Of(string type, params int[] ints) => new(type, ints, []);

	public static BattleCommand WithStrings(string type, int[] ints, params string[] strings) => new(type, ints, strings);
}

/// <summary>
/// Часть TestBattle: онлайн и команды.
///
/// Как устроен онлайн. Оба компьютера запускают ОДИН И ТОТ ЖЕ бой и выполняют одни и те же команды
/// в одном и том же порядке. Тогда и результат у них одинаковый — так называемый lockstep.
/// Чтобы это работало:
///   1. Любой клик или перетаскивание не меняет бой сразу, а превращается в команду (Send).
///   2. Порядок команд задаёт хост: подключившийся отправляет команду хосту, хост рассылает её обоим.
///   3. Всё «случайное» берётся из _rng с общим зерном (GameSession.Seed), поэтому совпадает.
///   4. Карты в командах указываются номером NetId: он раздаётся по порядку и одинаков у обоих.
/// В тестовой битве команды выполняются сразу, без сети — код один и тот же.
/// </summary>
public partial class TestBattle
{
	/// <summary>Генератор случайных чисел боя. В онлайне у обоих игроков одно зерно.</summary>
	private Random _rng = new();

	/// <summary>Игрок за этим компьютером. null — тестовая битва, где за экраном оба игрока по очереди.</summary>
	private PlayerState _localPlayer;

	private readonly Dictionary<int, Card> _cardsByNetId = new();
	private int _nextNetId;

	// Вопросы игрокам (окно выбора, драфт) по порядку. Ответ приходит командой и закрывает первый вопрос.
	private readonly Queue<PendingAnswer> _pendingAnswers = new();

	private bool _started;

	/// <summary>Этот игрок сидит за этим экраном? В тестовой битве — всегда да.</summary>
	private bool IsLocal(PlayerState player) => _localPlayer == null || player == _localPlayer;

	private PlayerState PlayerByIndex(int index) => index == 0 ? _bottomPlayer : _topPlayer;

	private int IndexOf(PlayerState player) => player == _bottomPlayer ? 0 : 1;

	private Lane LaneByRow(int row) => AllLanes.First(l => l.Row == row);

	private void RegisterCard(Card card)
	{
		card.NetId = ++_nextNetId;
		_cardsByNetId[card.NetId] = card;
	}

	private Card CardByNetId(int id) =>
		_cardsByNetId.TryGetValue(id, out Card card) && IsInstanceValid(card) ? card : null;

	// ───────────── Запуск ─────────────

	private void ReadyNetwork()
	{
		if (!GameSession.Online)
		{
			Send(BattleCommand.Of("start"));
			return;
		}

		_rng = new Random(GameSession.Seed);
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ServerDisconnected += OnServerDisconnected;

		if (!Multiplayer.IsServer())
			SayReadyUntilStarted();
		else
			ShowHint("Ждём, пока соперник загрузит бой…");
	}

	// Подключившийся повторяет «я готов», пока хост не начнёт: хост мог загрузить бой чуть позже.
	private async void SayReadyUntilStarted()
	{
		while (!_started && IsInsideTree())
		{
			RpcId(1, MethodName.ClientReady);
			await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientReady()
	{
		if (Multiplayer.IsServer() && !_started)
			Send(BattleCommand.Of("start"));
	}

	private void OnPeerDisconnected(long id) => ShowDisconnected();

	private void OnServerDisconnected() => ShowDisconnected();

	private void ShowDisconnected()
	{
		_gameEnded = true;
		_gameOverText.Text = "Связь потеряна";
		_gameOverReason.Text = "Соперник отключился.";
		_gameOver.Show();
	}

	// ───────────── Отправка и выполнение команд ─────────────

	/// <summary>Отправить команду: в тестовой битве — выполнить сразу, в онлайне — через хоста обоим.</summary>
	private void Send(BattleCommand command)
	{
		if (!GameSession.Online)
		{
			ExecuteCommand(command);
			return;
		}

		string json = JsonSerializer.Serialize(command);
		if (Multiplayer.IsServer())
			Rpc(MethodName.ApplyCommand, json);
		else
			RpcId(1, MethodName.RequestCommand, json);
	}

	/// <summary>Подключившийся просит хоста выполнить команду; хост рассылает её обоим.</summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RequestCommand(string json)
	{
		if (Multiplayer.IsServer())
			Rpc(MethodName.ApplyCommand, json);
	}

	/// <summary>Хост рассылает команду. CallLocal = true — выполняется и у самого хоста.</summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ApplyCommand(string json) => ExecuteCommand(JsonSerializer.Deserialize<BattleCommand>(json));

	/// <summary>
	/// Выполнить команду. На время выполнения убираем SynchronizationContext Godot:
	/// тогда код после await (например, после ответа в окне выбора) продолжается сразу,
	/// внутри команды-ответа, а не «когда-нибудь в следующем кадре». Иначе у двух игроков
	/// он мог бы выполниться в разном порядке с другими командами, и бой бы разошёлся.
	/// </summary>
	private void ExecuteCommand(BattleCommand command)
	{
		SynchronizationContext previous = SynchronizationContext.Current;
		SynchronizationContext.SetSynchronizationContext(null);
		try
		{
			Execute(command);
		}
		finally
		{
			SynchronizationContext.SetSynchronizationContext(previous);
		}
	}

	private void Execute(BattleCommand c)
	{
		int I(int n) => c.Ints[n];

		switch (c.Type)
		{
			case "start":
				_started = true;
				ShowHint("");
				StartGame();
				break;
			case "draw":
				OnDeckPressed(PlayerByIndex(I(0)));
				break;
			case "mana":
				OnManaPressed(PlayerByIndex(I(0)), (ManaType)I(1));
				break;
			case "next":
				OnNextPhasePressed();
				break;
			case "cancel_payment":
				CancelPayment();
				break;
			case "payment_done":
				OnPaymentDonePressed();
				break;
			case "cancel":
				ExecuteCancel();
				break;
			case "take_random":
				OnTakeRandomFromDiscard(PlayerByIndex(I(0)));
				break;
			case "activate":
				if (CardByNetId(I(0)) is { } source)
					OnActivateRequested(source, c.Strings[0], fromDiscard: false);
				break;
			case "activate_discard":
			{
				// Копия карты из сброса: живёт до конца оплаты способности.
				var copy = CardScene.Instantiate<Card>();
				copy.Setup(c.Strings[0]);
				copy.Player = PlayerByIndex(I(0));
				OnActivateRequested(copy, c.Strings[1], fromDiscard: true);
				break;
			}
			case "lane":
				if (_targeting != null)
					TryChooseTarget(LaneByRow(I(0)), null);
				break;
			case "click":
				if (CardByNetId(I(0)) is { } clicked)
					ExecuteCardClick(clicked);
				break;
			case "dblclick":
				if (CardByNetId(I(0)) is { } stacked)
					OnCardDoubleClicked(stacked);
				break;
			case "drop":
				ExecuteDrop(CardByNetId(I(0)), I(1) >= 0 ? LaneByRow(I(1)) : null, I(2) >= 0 ? CardByNetId(I(2)) : null);
				break;
			case "answer":
				AnswerChoice(I(0));
				break;
			case "pick":
				AnswerPick(c.Strings.ToList());
				break;
		}

		RefreshView();
	}

	private void ExecuteDrop(Card dragged, Lane lane, Card target)
	{
		// Проверяем ещё раз: у второго игрока за это время могло что-то измениться.
		if (dragged != null && CanDrag(dragged) && CanDrop(dragged, lane, target))
			Drop(dragged, lane, target);
	}

	// ───────────── Действия игрока на этом экране → команды ─────────────

	private void UiDeckPressed(PlayerState player)
	{
		if (_suppressDeckPress)
		{
			_suppressDeckPress = false; // Это было зажатие (просмотр колоды), а не добор.
			return;
		}

		if (IsLocal(player))
			Send(BattleCommand.Of("draw", IndexOf(player)));
	}

	private void UiManaPressed(PlayerState player, ManaType type)
	{
		if (IsLocal(player))
			Send(BattleCommand.Of("mana", IndexOf(player), (int)type));
	}

	private void UiNextPhase()
	{
		if (IsLocal(ActingPlayer) && !IsBusy)
			Send(BattleCommand.Of("next"));
	}

	private void UiCancelPayment()
	{
		if (_pending != null && IsLocal(_pending.Player))
			Send(BattleCommand.Of("cancel_payment"));
	}

	private void UiPaymentDone()
	{
		if (_pending != null && IsLocal(_pending.Player))
			Send(BattleCommand.Of("payment_done"));
	}

	private void UiTakeRandom()
	{
		if (_viewedDiscardOwner != null && IsLocal(ActingPlayer))
			Send(BattleCommand.Of("take_random", IndexOf(_viewedDiscardOwner)));
	}

	private void UiActivate(Card card, string abilityId)
	{
		if (!IsLocal(ActingPlayer))
			return;

		if (IsInPlay(card))
			Send(BattleCommand.WithStrings("activate", [card.NetId], abilityId));
		else
			Send(BattleCommand.WithStrings("activate_discard", [IndexOf(card.Player)], card.Data.Id, abilityId));
	}

	private void UiLaneInput(Lane lane, InputEvent @event)
	{
		if (_targeting != null && IsLocal(_targeting.Player)
			&& @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			Send(BattleCommand.Of("lane", lane.Row));
		}
	}

	private void UiCardClicked(Card card)
	{
		if (IsGameClick(card) && IsLocal(ClickingPlayer))
			Send(BattleCommand.Of("click", card.NetId));
		else
			ShowDetails(card);
	}

	// Зажатие (или правый клик) — сразу подробности, без ожидания двойного клика.
	private void UiCardLongPressed(Card card) => ShowDetails(card);

	private void UiCardDoubleClicked(Card card)
	{
		if (IsLocal(card.Player))
			Send(BattleCommand.Of("dblclick", card.NetId));
	}

	private void UiDrop(Card dragged, Lane lane, Card target)
	{
		if (dragged != null)
			Send(BattleCommand.Of("drop", dragged.NetId, lane?.Row ?? -1, target?.NetId ?? -1));
	}

	// ───────────── Вопросы игрокам ─────────────

	/// <summary>
	/// Спросить игрока и дождаться ответа (номер варианта). Окно показывается только у того,
	/// кого спрашивают; его ответ приходит обоим командой «answer».
	/// </summary>
	public Task<int> ChooseAsync(PlayerState player, string title, IReadOnlyList<string> options)
	{
		var answer = new PendingAnswer(player, new TaskCompletionSource<int>(), null);
		_pendingAnswers.Enqueue(answer);

		if (IsLocal(player))
			AskHere(player, title, options);
		else
			ShowHint($"Ждём: {player.Name} выбирает…");

		return answer.Choice.Task;
	}

	private async void AskHere(PlayerState player, string title, IReadOnlyList<string> options)
	{
		int index = await _choiceView.Ask($"{player.Name}: {title}", options);
		Send(BattleCommand.Of("answer", index));
	}

	/// <summary>Выбрать карты в окне драфта (от min до max). Как ChooseAsync, только ответ — список id.</summary>
	private Task<List<string>> PickAsync(PlayerState player, string text, IEnumerable<string> ids, int min, int max)
	{
		var answer = new PendingAnswer(player, null, new TaskCompletionSource<List<string>>());
		_pendingAnswers.Enqueue(answer);

		if (IsLocal(player))
			PickHere(player, text, ids.ToList(), min, max);
		else
			ShowHint($"Ждём: {player.Name} выбирает карты…");

		return answer.Pick.Task;
	}

	private async void PickHere(PlayerState player, string text, List<string> ids, int min, int max)
	{
		List<string> picked = await _draftView.Pick($"{player.Name}: {text}", ids, min, max, CardScene);
		Send(BattleCommand.WithStrings("pick", [], [.. picked]));
	}

	private void AnswerChoice(int index)
	{
		if (_pendingAnswers.TryDequeue(out PendingAnswer answer) && answer.Choice != null)
		{
			ShowHint("");
			answer.Choice.SetResult(index);
		}
	}

	private void AnswerPick(List<string> ids)
	{
		if (_pendingAnswers.TryDequeue(out PendingAnswer answer) && answer.Pick != null)
		{
			ShowHint("");
			answer.Pick.SetResult(ids);
		}
	}

	private void OnRestartPressed()
	{
		if (GameSession.Online)
		{
			Multiplayer.MultiplayerPeer = null;
			GetTree().ChangeSceneToFile("res://scenes/main.tscn");
		}
		else
		{
			GetTree().ReloadCurrentScene();
		}
	}

	/// <summary>Вопрос, который ждёт ответа: либо выбор варианта (Choice), либо выбор карт (Pick).</summary>
	private sealed record PendingAnswer(PlayerState Player, TaskCompletionSource<int> Choice, TaskCompletionSource<List<string>> Pick);
}
