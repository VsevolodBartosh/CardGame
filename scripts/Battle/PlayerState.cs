using System.Collections.Generic;
using Godot;

namespace CardGame;

/// <summary>
/// Всё, что относится к одному игроку в бою: колода, сброс, мана и его зоны на экране.
/// Это обычный C#-класс, не узел Godot: он только хранит ссылки на узлы.
/// </summary>
public sealed class PlayerState
{
	public PlayerState(string name, HBoxContainer hand, HBoxContainer advantages, Button deckButton, Button discardButton, ManaPoolView manaPool)
	{
		Name = name;
		Hand = hand;
		Advantages = advantages;
		DeckButton = deckButton;
		DiscardButton = discardButton;
		ManaPool = manaPool;
	}

	public string Name { get; }

	/// <summary>Id карт, которые лежат в колоде.</summary>
	public List<string> Deck { get; } = [];

	/// <summary>Id карт в сбросе, в порядке попадания туда.</summary>
	public List<string> Discard { get; } = [];

	/// <summary>Сколько маны каждого типа в накопителе. Нет ключа — значит 0.</summary>
	public Dictionary<ManaType, int> Mana { get; } = new();

	/// <summary>Ряд, куда кладутся карты руки.</summary>
	public HBoxContainer Hand { get; }

	/// <summary>Ряд под накопителем, где лежат разыгранные преимущества.</summary>
	public HBoxContainer Advantages { get; }

	/// <summary>Две линии игрока: [0] ближе к центру поля, [1] у руки.</summary>
	public List<Lane> Lanes { get; } = [];

	public Button DeckButton { get; }
	public Button DiscardButton { get; }
	public ManaPoolView ManaPool { get; }

	/// <summary>Брал ли игрок карту из колоды в этот ход.</summary>
	public bool HasDrawnThisTurn { get; set; }

	/// <summary>Ближайшая к руке линия: сюда разыгрываются существа.</summary>
	public Lane NearestLane => Lanes[1];

	/// <summary>Вторая линия игрока (ближе к центру поля).</summary>
	public Lane SecondLane => Lanes[0];

	/// <summary>До конца хода можно разыгрывать существ и на вторую линию (Изнанка).</summary>
	public bool CanPlayOnSecondLine { get; set; }

	/// <summary>
	/// До конца хода игрок контролирует оба сброса и может активировать
	/// свойства существ из любого сброса (Барон Суббота).
	/// </summary>
	public bool ControlsAllDiscards { get; set; }

	public void AddMana(ManaType type, int amount) => Mana[type] = Mana.GetValueOrDefault(type) + amount;
}
