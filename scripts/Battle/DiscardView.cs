using System.Collections.Generic;
using Godot;

namespace CardGame;

/// <summary>
/// Окно со списком карт: сброс или колода игрока. Корень сцены discard_view.tscn.
/// </summary>
public partial class DiscardView : Control
{
	/// <summary>Клик по карте в окне: бой откроет её подробности.</summary>
	[Signal]
	public delegate void CardClickedEventHandler(Card card);

	/// <summary>Нажата кнопка «Взять случайную карту» (Барон Суббота).</summary>
	[Signal]
	public delegate void TakeRandomRequestedEventHandler();

	private Label _title;
	private GridContainer _cards;
	private Button _takeRandomButton;

	public override void _Ready()
	{
		_title = GetNode<Label>("Center/Panel/Column/Title");
		_cards = GetNode<GridContainer>("Center/Panel/Column/Scroll/Cards");
		_takeRandomButton = GetNode<Button>("Center/Panel/Column/Buttons/TakeRandomButton");

		GetNode<Button>("Center/Panel/Column/Buttons/CloseButton").Pressed += Close;
		_takeRandomButton.Pressed += () =>
		{
			Close();
			EmitSignal(SignalName.TakeRandomRequested);
		};
		GetNode<Control>("Dimmer").GuiInput += OnDimmerGuiInput;

		Hide();
	}

	/// <summary>
	/// Показать карты. owner — чьи это карты (у карт в окне будет этот Player).
	/// showTakeRandom — показать кнопку «Взять случайную карту».
	/// </summary>
	public void Open(string title, IEnumerable<string> cardIds, PackedScene cardScene, PlayerState owner = null, bool showTakeRandom = false)
	{
		_title.Text = title;
		_takeRandomButton.Visible = showTakeRandom;

		// Убираем карты от прошлого открытия. RemoveChild сразу, QueueFree удалит узел в конце кадра.
		foreach (Node child in _cards.GetChildren())
		{
			_cards.RemoveChild(child);
			child.QueueFree();
		}

		foreach (string id in cardIds)
		{
			var card = cardScene.Instantiate<Card>();
			card.Setup(id);
			card.Player = owner;
			card.Clicked += c => EmitSignal(SignalName.CardClicked, c);
			_cards.AddChild(card);
		}

		Show();
	}

	public void Close() => Hide();

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Visible && @event.IsActionPressed("ui_cancel"))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnDimmerGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			Close();
	}
}
