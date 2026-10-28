using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardGame;

/// <summary>
/// Одна линия на поле: панель на экране, её хозяин и номер ряда.
/// Ряды считаются сверху вниз: 0 и 1 у верхнего игрока, 2 и 3 у нижнего.
/// </summary>
public sealed class Lane
{
	private readonly StyleBoxFlat _style;
	private readonly Color _normalBorder;

	public Lane(PlayerState owner, int row, PanelContainer panel)
	{
		Owner = owner;
		Row = row;
		Panel = panel;
		Cards = panel.GetNode<HBoxContainer>("Cards");

		// Стиль в сцене общий для всех линий одного игрока. Делаем копию,
		// чтобы подсветка одной линии не красила остальные.
		_style = (StyleBoxFlat)panel.GetThemeStylebox("panel").Duplicate();
		panel.AddThemeStyleboxOverride("panel", _style);
		_normalBorder = _style.BorderColor;
	}

	public PlayerState Owner { get; }
	public int Row { get; }
	public PanelContainer Panel { get; }
	public HBoxContainer Cards { get; }

	/// <summary>Все карты на линии: существа обоих игроков и выпавшее снаряжение.</summary>
	public IEnumerable<Card> AllCards => Cards.GetChildren().OfType<Card>();

	/// <summary>Существа на линии, обоих игроков.</summary>
	public IEnumerable<Card> Creatures => AllCards.Where(c => c.IsCreature);

	/// <summary>Соседние линии — те, что стоят вплотную сверху или снизу.</summary>
	public bool IsAdjacentTo(Lane other) => Math.Abs(Row - other.Row) == 1;

	/// <summary>Цвет рамки линии; null возвращает обычный.</summary>
	public void SetHighlight(Color? color) => _style.BorderColor = color ?? _normalBorder;
}
