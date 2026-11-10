using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace CardGame;

/// <summary>
/// Окно драфта: показывает карты на выбор, игрок кликами отмечает нужное количество и жмёт «Готово».
/// Корень сцены draft_view.tscn.
/// </summary>
public partial class DraftView : Control
{
	/// <summary>Правый клик по карте: бой откроет её подробности.</summary>
	[Signal]
	public delegate void CardDetailsRequestedEventHandler(Card card);

	private static readonly Color SelectedColor = new(1f, 0.85f, 0.2f);

	private Label _title;
	private HBoxContainer _cards;
	private Button _confirmButton;

	private readonly List<Card> _selected = [];
	private int _minCount;
	private int _count;
	private TaskCompletionSource<List<string>> _result;

	public override void _Ready()
	{
		_title = GetNode<Label>("Center/Panel/Column/Title");
		_cards = GetNode<HBoxContainer>("Center/Panel/Column/Cards");
		_confirmButton = GetNode<Button>("Center/Panel/Column/ConfirmButton");
		_confirmButton.Pressed += OnConfirmPressed;
		Hide();
	}

	/// <summary>
	/// Показать карты и дождаться выбора ровно count штук.
	/// Возвращает Task: вызывающий код пишет await и продолжает, когда игрок нажмёт «Готово».
	/// </summary>
	public Task<List<string>> Pick(string title, IEnumerable<string> cardIds, int count, PackedScene cardScene) =>
		Pick(title, cardIds, count, count, cardScene);

	/// <summary>Выбрать от minCount до maxCount карт (например, «до 2 существ» — от 0 до 2).</summary>
	public Task<List<string>> Pick(string title, IEnumerable<string> cardIds, int minCount, int maxCount, PackedScene cardScene)
	{
		_title.Text = title;
		_minCount = minCount;
		_count = maxCount;
		_selected.Clear();

		foreach (Node child in _cards.GetChildren())
		{
			_cards.RemoveChild(child);
			child.QueueFree();
		}

		foreach (string id in cardIds)
		{
			// Под картой подпись с названием: на самой карте пока только жёлтый квадрат.
			var column = new VBoxContainer();
			var card = cardScene.Instantiate<Card>();
			card.Setup(id);
			card.Clicked += ToggleSelection;
			card.RightClicked += c => EmitSignal(SignalName.CardDetailsRequested, c);
			column.AddChild(card);

			var name = new Label { Text = card.Data.Name, HorizontalAlignment = HorizontalAlignment.Center };
			name.AddThemeFontSizeOverride("font_size", 11);
			column.AddChild(name);
			_cards.AddChild(column);
		}

		UpdateConfirmButton();
		Show();

		_result = new TaskCompletionSource<List<string>>();
		return _result.Task;
	}

	private void ToggleSelection(Card card)
	{
		if (_selected.Remove(card))
		{
			card.SetHighlight(null);
		}
		else
		{
			// Если нужна одна карта, новый клик просто меняет выбор.
			if (_count == 1 && _selected.Count == 1)
			{
				_selected[0].SetHighlight(null);
				_selected.Clear();
			}

			if (_selected.Count < _count)
			{
				_selected.Add(card);
				card.SetHighlight(SelectedColor);
			}
		}

		UpdateConfirmButton();
	}

	private void UpdateConfirmButton()
	{
		_confirmButton.Disabled = _selected.Count < _minCount || _selected.Count > _count;
		_confirmButton.Text = _minCount == _count
			? $"Готово ({_selected.Count} из {_count})"
			: $"Готово (выбрано {_selected.Count}, можно до {_count})";
	}

	private void OnConfirmPressed()
	{
		List<string> ids = _selected.Select(c => c.Data.Id).ToList();
		Hide();
		TaskHelpers.CompleteInline(_result, ids);
	}
}
