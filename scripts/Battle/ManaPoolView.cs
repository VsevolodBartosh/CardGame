using System;
using System.Collections.Generic;
using Godot;

namespace CardGame;

/// <summary>
/// Накопитель маны одного игрока: строка на каждый тип, который у него есть.
/// Значок — цветной квадрат, рядом количество. Типы с нулём не показываются.
/// Корень узлов PlayerManaPool и EnemyManaPool в test_battle.tscn.
/// </summary>
public partial class ManaPoolView : PanelContainer
{
	/// <summary>Игрок кликнул по мане, пока идёт выбор оплаты.</summary>
	[Signal]
	public delegate void ManaPressedEventHandler(ManaType type);

	private static readonly Color SelectingBorder = new(1f, 0.9f, 0.2f);

	private readonly Dictionary<ManaType, Button> _rows = new();
	private StyleBoxFlat _style;
	private Color _normalBorder;
	private bool _selecting;

	public override void _Ready()
	{
		_style = (StyleBoxFlat)GetThemeStylebox("panel").Duplicate();
		AddThemeStyleboxOverride("panel", _style);
		_normalBorder = _style.BorderColor;

		var rows = GetNode<VBoxContainer>("Rows");
		foreach (ManaType type in Enum.GetValues<ManaType>())
		{
			// Button умеет и значок, и текст, и клик. Клик нужен для выбора маны при оплате.
			var row = new Button
			{
				Icon = CreateSquareIcon(type.ToColor()),
				Flat = true,
				Alignment = HorizontalAlignment.Left,
				TooltipText = type.ToRussian(),
				FocusMode = FocusModeEnum.None,
				Visible = false,
			};
			row.AddThemeFontSizeOverride("font_size", 12);
			row.Pressed += () => OnRowPressed(type);
			rows.AddChild(row);
			_rows[type] = row;
		}
	}

	public void Refresh(Dictionary<ManaType, int> mana)
	{
		foreach ((ManaType type, Button row) in _rows)
		{
			int count = mana.GetValueOrDefault(type);
			row.Visible = count > 0;
			row.Text = count.ToString();
		}
	}

	/// <summary>Подсветить накопитель и разрешить клики по мане.</summary>
	public void SetSelecting(bool selecting)
	{
		_selecting = selecting;
		_style.BorderColor = selecting ? SelectingBorder : _normalBorder;
		foreach (Button row in _rows.Values)
			row.Flat = !selecting;
	}

	private void OnRowPressed(ManaType type)
	{
		if (_selecting)
			EmitSignal(SignalName.ManaPressed, Variant.From(type));
	}

	private static Texture2D CreateSquareIcon(Color color)
	{
		Image image = Image.CreateEmpty(12, 12, false, Image.Format.Rgba8);
		image.Fill(color);
		return ImageTexture.CreateFromImage(image);
	}
}
