using System.Linq;

namespace CardGame;

/// <summary>Типы карт. От типа зависит, когда и куда карту можно разыграть.</summary>
public enum CardType
{
	/// <summary>Стоит на линии, нападает и защищается.</summary>
	Creature,

	/// <summary>Даёт ману и уходит в сброс.</summary>
	Payment,

	/// <summary>Мгновенный эффект, потом карта уходит в сброс.</summary>
	Spell,

	/// <summary>Разыгрывается на существо и действует, пока оно живо.</summary>
	Enchantment,

	/// <summary>Разыгрывается на ближайшую линию или на существо на ней. После гибели существа выпадает на линию.</summary>
	Equipment,

	/// <summary>Лежит в контейнере преимуществ игрока и действует долго.</summary>
	Advantage,
}

public static class CardTypeExtensions
{
	/// <summary>Название типа на русском, для отображения игроку.</summary>
	public static string ToRussian(this CardType type) => type switch
	{
		CardType.Creature => "Существо",
		CardType.Payment => "Оплата",
		CardType.Spell => "Заклинание",
		CardType.Enchantment => "Чары",
		CardType.Equipment => "Снаряжение",
		CardType.Advantage => "Преимущество",
		_ => type.ToString(),
	};
}

/// <summary>
/// Тэги карты. Сами по себе ничего не делают, их проверяют свойства других карт
/// (например, «все Люди получают +1 к атаке»).
/// </summary>
public enum CardTag
{
	Human,
	Demon,
	Elemental,
	Warrior,
	Loa,
	Undead,
	Squad,
	Soldier,
	Beast,
	Fairy,
	Spirit,
	Unclean,
	Artifact,
	Golem,
	Skaven,
	Priest,
}

public static class CardTagExtensions
{
	public static string ToRussian(this CardTag tag) => tag switch
	{
		CardTag.Human => "Человек",
		CardTag.Demon => "Демон",
		CardTag.Elemental => "Элементаль",
		CardTag.Warrior => "Воин",
		CardTag.Loa => "Лоа",
		CardTag.Undead => "Нежить",
		CardTag.Squad => "Отряд",
		CardTag.Soldier => "Солдат",
		CardTag.Beast => "Зверь",
		CardTag.Fairy => "Фея",
		CardTag.Spirit => "Дух",
		CardTag.Unclean => "Нечисть",
		CardTag.Artifact => "Артефакт",
		CardTag.Golem => "Голем",
		CardTag.Skaven => "Скавен",
		CardTag.Priest => "Священник",
		_ => tag.ToString(),
	};
}

/// <summary>
/// Описание одной карты: только данные, без узлов Godot.
/// Узел карты на экране (Card) берёт отсюда всё, что показывает.
/// </summary>
public sealed class CardData
{
	public required string Id { get; init; }
	public required string Name { get; init; }

	/// <summary>Путь к картинке внутри проекта (res://...).</summary>
	public required string ImagePath { get; init; }

	public int Health { get; init; }
	public int Attack { get; init; }
	public int Defense { get; init; }
	public int Block { get; init; }

	public CardType[] Types { get; init; } = [];

	/// <summary>Тэги карты: Человек, Демон и т.д.</summary>
	public CardTag[] Tags { get; init; } = [];

	/// <summary>
	/// Герой — особое существо. У каждого игрока их два: один стоит на поле с начала игры, второй в колоде.
	/// Погиб герой и других героев у игрока нет — игрок проиграл.
	/// </summary>
	public bool IsHero { get; init; }

	/// <summary>Цена розыгрыша в мане. Пустой массив — мана не нужна.</summary>
	public ManaCost[] Cost { get; init; } = [];

	/// <summary>Цена розыгрыша в труде: сколько своих существ повернуть.</summary>
	public int LaborCost { get; init; }

	/// <summary>
	/// Другие способы оплатить карту, кроме Cost + LaborCost. Например «2 золота или 1 вера»:
	/// Cost = [2 золота], AlternativeCosts = [new CostOption { Mana = [1 вера] }].
	/// </summary>
	public CostOption[] AlternativeCosts { get; init; } = [];

	/// <summary>Сколько карт из руки нужно сбросить в добавок к цене (Трюк пиромана).</summary>
	public int DiscardCost { get; init; }

	/// <summary>
	/// Цена X: игрок сам решает, сколько заплатить (не меньше 1). Каждую единицу можно
	/// оплатить любой маной, сбросом карты из руки или жертвой своего существа.
	/// </summary>
	public bool XCost { get; init; }

	/// <summary>Заклинание после розыгрыша замешивается обратно в колоду, а не идёт в сброс.</summary>
	public bool ReturnsToDeck { get; init; }

	/// <summary>
	/// Зомби: при розыгрыше можно изгнать до стольких существ из своего сброса,
	/// каждое уменьшает цену на 1 (первой части Cost).
	/// </summary>
	public int ExileDiscountMax { get; init; }

	/// <summary>
	/// Енот грабитель: если в этом ходу умерло любое существо, карта бесплатная
	/// и её можно разыграть на линию, где умерло существо.
	/// </summary>
	public bool FreeAfterDeathThisTurn { get; init; }

	/// <summary>
	/// Существо, которое можно разыграть как чары (Легион): id карты-чар, в которую оно превращается.
	/// Бросаешь на существо — игра спросит, как разыграть.
	/// </summary>
	public string EnchantmentFormId { get; init; }

	/// <summary>В сброс уходит не эта карта, а карта с этим id (чары Легиона снова становятся Легионом).</summary>
	public string DiscardAsId { get; init; }

	/// <summary>false — служебная карта, в драфт не попадает (например, «Легион (чары)»).</summary>
	public bool InDraft { get; init; } = true;

	/// <summary>Жетон (Крыса): создаётся способностями, в драфт не попадает, при смерти просто исчезает.</summary>
	public bool IsToken { get; init; }

	/// <summary>Заклинание нужно бросить на существо (любого игрока).</summary>
	public bool TargetsCreature { get; init; }

	/// <summary>
	/// Задан только у карт оплаты: какую ману карта даёт.
	/// Такие карты не попадают в общий пул при сборке колод.
	/// </summary>
	public ManaType? PaymentManaType { get; init; }

	public bool IsPayment => PaymentManaType != null;

	/// <summary>Id способностей из AbilitiesData. Пустой массив, если способностей нет.</summary>
	public string[] AbilityIds { get; init; } = [];

	public bool Is(CardType type) => Types.Contains(type);

	public bool Has(CardTag tag) => Tags.Contains(tag);
}
