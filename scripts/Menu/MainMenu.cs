using System;
using System.Linq;
using Godot;

namespace CardGame;

/// <summary>
/// Главное меню (main.tscn): тестовая битва за одним экраном или онлайн-битва.
///
/// Онлайн работает через ENet (встроенная в Godot сетевая библиотека):
/// один игрок создаёт игру (хост, сервер), второй подключается к нему по IP-адресу и порту.
/// </summary>
public partial class MainMenu : Control
{
	private const string BattleScene = "res://scenes/test_battle.tscn";

	private Control _mainButtons;
	private Control _onlinePanel;
	private LineEdit _portEdit;
	private LineEdit _addressEdit;
	private Label _status;

	public override void _Ready()
	{
		_mainButtons = GetNode<Control>("Center/Column/MainButtons");
		_onlinePanel = GetNode<Control>("Center/Column/OnlinePanel");
		_portEdit = GetNode<LineEdit>("Center/Column/OnlinePanel/PortRow/Port");
		_addressEdit = GetNode<LineEdit>("Center/Column/OnlinePanel/JoinRow/Address");
		_status = GetNode<Label>("Center/Column/OnlinePanel/Status");

		GetNode<Button>("Center/Column/MainButtons/TestBattleButton").Pressed += OnTestBattlePressed;
		GetNode<Button>("Center/Column/MainButtons/OnlineButton").Pressed += () => ShowOnlinePanel(true);
		GetNode<Button>("Center/Column/OnlinePanel/HostButton").Pressed += OnHostPressed;
		GetNode<Button>("Center/Column/OnlinePanel/JoinRow/JoinButton").Pressed += OnJoinPressed;
		GetNode<Button>("Center/Column/OnlinePanel/BackButton").Pressed += OnBackPressed;

		_portEdit.Text = GameSession.DefaultPort.ToString();

		// Обработчики — методы, а не лямбды: тогда Godot сам отключит их, когда меню удалится.
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.ConnectionFailed += OnConnectionFailed;

		ShowOnlinePanel(false);
	}

	private void OnTestBattlePressed()
	{
		GameSession.Online = false;
		GameSession.LocalPlayerIndex = 0;
		GameSession.Seed = (int)GD.Randi();
		GetTree().ChangeSceneToFile(BattleScene);
	}

	private void ShowOnlinePanel(bool show)
	{
		_mainButtons.Visible = !show;
		_onlinePanel.Visible = show;
		_status.Text = "";
	}

	private void OnHostPressed()
	{
		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateServer(Port(), 1);
		if (error != Error.Ok)
		{
			_status.Text = $"Не удалось создать игру: {error}. Возможно, порт занят.";
			return;
		}

		Multiplayer.MultiplayerPeer = peer;

		// Свои адреса в локальной сети: их нужно сообщить второму игроку.
		string addresses = string.Join(", ", IP.GetLocalAddresses()
			.Where(a => a.Contains('.') && !a.StartsWith("127.") && !a.StartsWith("169.254.")));
		_status.Text = $"Игра создана, ждём соперника.\nВаш адрес: {addresses}, порт {Port()}.";
	}

	private void OnJoinPressed()
	{
		string address = _addressEdit.Text.Trim();
		if (address.Length == 0)
		{
			_status.Text = "Введите адрес хоста.";
			return;
		}

		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateClient(address, Port());
		if (error != Error.Ok)
		{
			_status.Text = $"Не удалось подключиться: {error}.";
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		_status.Text = $"Подключаемся к {address}:{Port()}…";
	}

	private void OnBackPressed()
	{
		Multiplayer.MultiplayerPeer = null;
		ShowOnlinePanel(false);
	}

	// Сработает у хоста, когда подключится второй игрок.
	private void OnPeerConnected(long id)
	{
		if (!Multiplayer.IsServer())
			return;

		int seed = (int)GD.Randi();
		Rpc(MethodName.StartOnlineBattle, seed);
	}

	private void OnConnectionFailed() => _status.Text = "Не удалось подключиться. Проверьте адрес, порт и сеть.";

	/// <summary>
	/// Хост вызывает у обоих (CallLocal = true — и у себя тоже): начать бой с общим зерном.
	/// Authority — вызывать этот метод может только хост.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void StartOnlineBattle(int seed)
	{
		GameSession.Online = true;
		GameSession.LocalPlayerIndex = Multiplayer.IsServer() ? 0 : 1;
		GameSession.Seed = seed;
		GetTree().ChangeSceneToFile(BattleScene);
	}

	private int Port() => int.TryParse(_portEdit.Text, out int port) && port is > 0 and < 65536 ? port : GameSession.DefaultPort;
}
