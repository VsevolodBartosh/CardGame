using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardGame;

/// <summary>
/// Одна карта на экране: в руке, на линии или в преимуществах. Корень сцены test_card.tscn.
/// Показывает только картинку и характеристики; подробности открывает CardDetails.
/// Перетаскивание (drag and drop) настраивает TestBattle, сама карта о нём не знает.
/// </summary>
public partial class Card : Control
{
	private static readonly Vector2 NormalSize = new(70, 84);

	// Повёрнутая карта шире: 84 по горизонтали плюс небольшой отступ.
	private static readonly Vector2 TappedSize = new(88, 84);

	/// <summary>Срабатывает при клике левой кнопкой мыши по карте (без перетаскивания).</summary>
	[Signal]
	public delegate void ClickedEventHandler(Card card);

	/// <summary>Клик по значку прикреплённой карты (чары, снаряжение).</summary>
	[Signal]
	public delegate void AttachmentClickedEventHandler(Card attachment);

	/// <summary>Клик правой кнопкой: в драфте открывает подробности.</summary>
	[Signal]
	public delegate void RightClickedEventHandler(Card card);

	public CardData Data { get; private set; }
	public IReadOnlyList<AbilityData> Abilities { get; private set; } = [];
	public bool IsFaceDown { get; private set; }

	/// <summary>
	/// Чья это карта. Не Owner: это имя у Node уже занято (владелец в дереве сцены).
	/// Не путать и с хозяином линии: напавшее существо может перейти на вражескую линию и остаться своим.
	/// </summary>
	public PlayerState Player { get; set; }

	/// <summary>Здоровье в бою. В начале равно Data.Health, уменьшается от урона.</summary>
	public int CurrentHealth { get; private set; }

	public bool IsDead => CurrentHealth <= 0;

	public bool IsCreature => Data.Is(CardType.Creature);

	/// <summary>Повёрнута ли карта. Повёрнутую нельзя повернуть снова.</summary>
	public bool IsTapped { get; private set; }

	/// <summary>
	/// Сколько труда уже вложено в карту: в розыгрыш, пока она в руке,
	/// или в активацию, пока она на поле. Сохраняется между фазами и ходами.
	/// </summary>
	public int LaborPaid { get; private set; }

	/// <summary>Чары и снаряжение, прикреплённые к этому существу.</summary>
	public IReadOnlyList<Card> Attachments => _attachments;

	/// <summary>Атака с учётом прибавок от прикреплённых карт.</summary>
	public int Attack => Data.Attack + _attachments.SelectMany(a => a.Abilities).Sum(a => a.AttackBonus);

	public int Block => Data.Block;

	public bool IsHero => Data.IsHero;

	/// <summary>Кто обездвижил это существо. null — не обездвижено.</summary>
	public PlayerState ImmobilizedBy { get; private set; }

	public bool IsImmobilized => ImmobilizedBy != null;

	/// <summary>Есть ли у карты способность, подходящая под условие. Например: card.HasAny(a => a.Flight).</summary>
	public bool HasAny(Func<AbilityData, bool> predicate) => Abilities.Any(predicate);

	public void SetImmobilized(PlayerState by)
	{
		ImmobilizedBy = by;
		Refresh();
	}

	/// <summary>Сколько ещё своих снабжений оглушённое существо пропустит, не разворачиваясь. 0 — не оглушено.</summary>
	public int StunTurns { get; private set; }

	public bool IsStunned => StunTurns > 0;

	public void SetStunTurns(int turns)
	{
		StunTurns = turns;
		Refresh();
	}

	/// <summary>Тип маны, выбранный для этой карты («Зависимость»). null — не выбран.</summary>
	public ManaType? ChosenMana { get; set; }

	/// <summary>Кто последним нанёс урон: если существо от этого погибло, это его убийца.</summary>
	public Card LastDamageSource { get; set; }

	/// <summary>Линии, на которых существо уже побывало (для «Фонарщика»).</summary>
	public HashSet<Lane> VisitedLanes { get; } = [];

	// Сколько выстрелов уже сделано по каждой способности-выстрелу.
	private readonly Dictionary<string, int> _shotsFired = new();

	/// <summary>Сколько выстрелов осталось. null — боезапас бесконечный.</summary>
	public int? AmmoLeft(AbilityData shot) =>
		shot.Shot.Ammo is { } ammo ? ammo - _shotsFired.GetValueOrDefault(shot.Id) : null;

	public void UseAmmo(string abilityId) => _shotsFired[abilityId] = _shotsFired.GetValueOrDefault(abilityId) + 1;

	private readonly List<Card> _attachments = [];

	private Control _frame;
	private Control _front;
	private Control _back;
	private TextureRect _image;
	private ColorRect _manaMark;
	private HBoxContainer _chips;
	private Label _laborLabel;
	private Label _typeLabel;
	private Label _badge;
	private Control _stats;
	private Label _attack;
	private Label _defense;
	private Label _health;
	private Label _block;

	private StyleBoxFlat _frameStyle;
	private Color _normalBorder;

	// Нажали на карту и ещё не начали тащить. Клик засчитываем при отпускании кнопки.
	private bool _pressed;

	public override void _Ready()
	{
		_frame = GetNode<Control>("Frame");
		_front = GetNode<Control>("Frame/Front");
		_back = GetNode<Control>("Frame/Back");
		_image = GetNode<TextureRect>("Frame/Front/Image");
		_manaMark = GetNode<ColorRect>("Frame/Front/Image/ManaMark");
		_chips = GetNode<HBoxContainer>("Frame/Front/Image/Chips");
		_laborLabel = GetNode<Label>("Frame/Front/Image/LaborLabel");
		_typeLabel = GetNode<Label>("Frame/Front/TypeLabel");
		_badge = GetNode<Label>("Frame/Front/Image/Badge");
		_stats = GetNode<Control>("Frame/Front/Stats");
		_attack = GetNode<Label>("Frame/Front/Stats/Attack");
		_defense = GetNode<Label>("Frame/Front/Stats/Defense");
		_health = GetNode<Label>("Frame/Front/Stats/Health");
		_block = GetNode<Label>("Frame/Front/Stats/Block");

		// Копия стиля рамки, чтобы подсветка одной карты не красила все.
		_frameStyle = (StyleBoxFlat)_frame.GetThemeStylebox("panel").Duplicate();
		_frame.AddThemeStyleboxOverride("panel", _frameStyle);
		_normalBorder = _frameStyle.BorderColor;

		UpdateView();
	}

	/// <summary>
	/// Заполняет карту по Id из CardsData, способности берутся из AbilitiesData.
	/// Можно вызывать и до, и после добавления карты в дерево сцены.
	/// </summary>
	public void Setup(string cardId)
	{
		Data = CardsData.Get(cardId);
		Abilities = Data.AbilityIds.Select(AbilitiesData.Get).ToList();
		CurrentHealth = Data.Health;
		Refresh();
	}

	public bool HasAbility(string abilityId) => Data.AbilityIds.Contains(abilityId);

	/// <summary>true: видна только рубашка, false: лицевая сторона.</summary>
	public void SetFaceDown(bool faceDown)
	{
		IsFaceDown = faceDown;
		Refresh();
	}

	public void TakeDamage(int amount)
	{
		CurrentHealth = Math.Max(0, CurrentHealth - amount);
		Refresh();
	}

	/// <summary>
	/// Повернуть или развернуть карту.
	/// Контейнеры Godot (HBoxContainer и т.п.) сбрасывают поворот своих детей,
	/// поэтому поворачиваем не саму карту, а её внутреннюю рамку Frame.
	/// </summary>
	public void SetTapped(bool tapped)
	{
		IsTapped = tapped;
		CustomMinimumSize = tapped ? TappedSize : NormalSize;
		Refresh();
	}

	public void AddLabor(int amount)
	{
		LaborPaid += amount;
		Refresh();
	}

	public void ResetLabor()
	{
		LaborPaid = 0;
		Refresh();
	}

	/// <summary>Прикрепить карту к этому существу. Узел прикреплённой карты убирается из дерева сцены.</summary>
	public void Attach(Card attachment)
	{
		attachment.GetParent()?.RemoveChild(attachment);
		_attachments.Add(attachment);
		Refresh();
	}

	public void Detach(Card attachment)
	{
		_attachments.Remove(attachment);
		Refresh();
	}

	/// <summary>Обычный цвет рамки. Им показываем, чьё существо, когда на линии стоят оба игрока.</summary>
	public void SetBaseBorder(Color color)
	{
		_normalBorder = color;
		_frameStyle.BorderColor = color;
	}

	/// <summary>Цвет рамки (нападает, атакован, защищает); null возвращает обычный.</summary>
	public void SetHighlight(Color? color)
	{
		_frameStyle.BorderColor = color ?? _normalBorder;
		_frameStyle.SetBorderWidthAll(color == null ? 2 : 4);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
		{
			EmitSignal(SignalName.RightClicked, this);
			AcceptEvent();
			return;
		}

		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
			return;

		if (mouse.Pressed)
		{
			_pressed = true;
		}
		else if (_pressed && new Rect2(Vector2.Zero, Size).HasPoint(mouse.Position))
		{
			_pressed = false;
			EmitSignal(SignalName.Clicked, this);
		}

		AcceptEvent();
	}

	public override void _Notification(int what)
	{
		// Godot сообщает всем узлам о начале перетаскивания: это уже не клик.
		if (what == NotificationDragBegin)
			_pressed = false;

		// Карту удаляют. Прикреплённые карты не в дереве сцены, Godot сам их не удалит.
		if (what == NotificationPredelete)
		{
			foreach (Card attachment in _attachments)
			{
				if (IsInstanceValid(attachment) && !attachment.IsInsideTree())
					attachment.Free();
			}
		}
	}

	private void Refresh()
	{
		if (IsNodeReady())
			UpdateView();
	}

	private void UpdateView()
	{
		_front.Visible = !IsFaceDown;
		_back.Visible = IsFaceDown;
		_frame.RotationDegrees = IsTapped ? 90 : 0;

		if (Data == null)
			return;

		_image.Texture = GD.Load<Texture2D>(Data.ImagePath);

		// Характеристики есть только у существ. У карты оплаты в центре квадрат цвета маны,
		// у остальных внизу подписан тип.
		_stats.Visible = IsCreature;
		_manaMark.Visible = Data.IsPayment;
		if (Data.PaymentManaType is { } manaType)
			_manaMark.Color = manaType.ToColor();
		_typeLabel.Visible = !IsCreature && !Data.IsPayment;
		_typeLabel.Text = string.Join(", ", Data.Types.Select(t => t.ToRussian()));

		_laborLabel.Visible = LaborPaid > 0;
		_laborLabel.Text = $"Труд {LaborPaid}";

		// Значок внизу картинки: герой и/или обездвиженное существо.
		var badges = new List<string>();
		if (IsHero)
			badges.Add("Герой");
		if (IsImmobilized)
			badges.Add("Обезд.");
		if (IsStunned)
			badges.Add("Оглуш.");
		_badge.Visible = badges.Count > 0;
		_badge.Text = string.Join(" ", badges);

		_attack.Text = Attack.ToString();
		_defense.Text = Data.Defense.ToString();
		_health.Text = CurrentHealth.ToString();
		_block.Text = Data.Block.ToString();

		UpdateChips();
	}

	// Значки прикреплённых карт в углу картинки: первая буква типа, подсказка — название.
	private void UpdateChips()
	{
		foreach (Node chip in _chips.GetChildren())
		{
			_chips.RemoveChild(chip);
			chip.QueueFree();
		}

		foreach (Card attachment in _attachments)
		{
			var chip = new Button
			{
				Text = attachment.Data.Types.FirstOrDefault().ToRussian()[..1],
				TooltipText = attachment.Data.Name,
				CustomMinimumSize = new Vector2(16, 16),
				FocusMode = FocusModeEnum.None,
				// Pass: клик достаётся значку, а брошенная на значок карта «проваливается» к самой карте.
				MouseFilter = MouseFilterEnum.Pass,
			};
			chip.AddThemeFontSizeOverride("font_size", 9);
			chip.Pressed += () => EmitSignal(SignalName.AttachmentClicked, attachment);
			_chips.AddChild(chip);
		}
	}
}
