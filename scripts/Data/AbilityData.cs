using System;

namespace CardGame;

/// <summary>
/// Что знает способность, когда срабатывает.
/// Battle даёт доступ к действиям боя: нанести урон, повернуть существо.
/// </summary>
public sealed class AbilityContext
{
	/// <summary>Игрок, который разыграл или активировал карту.</summary>
	public required PlayerState Player { get; init; }

	/// <summary>Карта, у которой эта способность.</summary>
	public required Card Source { get; init; }

	/// <summary>Цель: существо, на которое разыграли карту или к которому она прикреплена. Может быть null.</summary>
	public Card Target { get; init; }

	/// <summary>Цель-линия (например, куда переместиться). Может быть null.</summary>
	public Lane TargetLane { get; init; }

	/// <summary>Сколько заплачено за карту с ценой X («Выпить любой ценой»).</summary>
	public int X { get; init; }

	public required TestBattle Battle { get; init; }
}

/// <summary>
/// Параметры выстрела. Создаётся через AbilitiesData.Shot(...).
/// Выстрел наносит урон цели и не получает урона в ответ; блок цели урон уменьшает.
/// </summary>
public sealed class ShotInfo
{
	public required int Damage { get; init; }

	/// <summary>На сколько линий можно выстрелить. 0 — только в существо на своей линии.</summary>
	public required int Distance { get; init; }

	/// <summary>Сколько всего выстрелов у существа. null — бесконечно.</summary>
	public int? Ammo { get; init; }
}

/// <summary>Что нужно выбрать игроку перед активацией способности.</summary>
public enum ActivationTarget
{
	None,

	/// <summary>Любая линия.</summary>
	AnyLane,

	/// <summary>Любое существо на поле, кроме героев.</summary>
	AnyNonHeroCreature,

	/// <summary>Существо на линии, соседней с линией карты-источника.</summary>
	AdjacentCreature,

	/// <summary>Вражеское существо в пределах дистанции выстрела.</summary>
	ShotTarget,
}

/// <summary>
/// Описание одной способности: данные и, если есть, что она делает.
/// Все поля, кроме Id, Name и Description, необязательные: заполняется только нужное.
/// </summary>
public sealed class AbilityData
{
	/// <summary>Уникальный ключ, по которому карта ссылается на способность.</summary>
	public required string Id { get; init; }

	/// <summary>Название, которое видит игрок.</summary>
	public required string Name { get; init; }

	/// <summary>Текст способности в подробном окне карты.</summary>
	public required string Description { get; init; }

	/// <summary>
	/// Слова из Glossary, которые встречаются в способности.
	/// Их пояснения показываются справа от карты в окне подробностей.
	/// </summary>
	public string[] GlossaryTerms { get; init; } = [];

	// ───────── Срабатывают сами ─────────

	/// <summary>Срабатывает, когда карту разыграли.</summary>
	public Action<AbilityContext> OnPlayed { get; init; }

	/// <summary>
	/// Срабатывает в начале фазы трофеев. У чар и снаряжения — в трофеи владельца существа,
	/// к которому они прикреплены; у преимущества — в трофеи его владельца.
	/// </summary>
	public Action<AbilityContext> OnTrophyPhase { get; init; }

	// ───────── Постоянные свойства существа ─────────

	/// <summary>Прибавка к атаке существа, к которому прикреплена карта (снаряжение, чары).</summary>
	public int AttackBonus { get; init; }

	/// <summary>Полёт: нападает на 1 линию дальше, заблокировать могут только с линии цели.</summary>
	public bool Flight { get; init; }

	/// <summary>Скорость: нападает на 1 линию дальше, могут заблокировать и с линии, через которую проходит.</summary>
	public bool Speed { get; init; }

	/// <summary>Может нападать на несколько существ сразу, урон делится поровну (с округлением вниз).</summary>
	public bool MultiTarget { get; init; }

	/// <summary>Нападая, не получает урон от защиты цели.</summary>
	public bool IgnoresDefenseWhenAttacking { get; init; }

	/// <summary>Лень: чтобы напасть, нужно заплатить эту ману. Пусто — лени нет.</summary>
	public ManaCost[] LazinessCost { get; init; } = [];

	/// <summary>Можно взять случайную карту из подконтрольного сброса вместо добора из колоды.</summary>
	public bool DrawFromDiscard { get; init; }

	/// <summary>Первый удар: убил в схватке — не получил урона. Если первый удар у обоих, он не действует.</summary>
	public bool FirstStrike { get; init; }

	/// <summary>Миг: существо можно разыграть в любую фазу.</summary>
	public bool Flash { get; init; }

	/// <summary>Нападая на существо, после схватки оглушает того, с кем билось.</summary>
	public bool StunsWhenAttacking { get; init; }

	/// <summary>Выстрел. null — у способности нет выстрела.</summary>
	public ShotInfo Shot { get; init; }

	// ───────── Срабатывают по событию ─────────

	/// <summary>Это существо убило другое (в схватке или выстрелом). Target — убитый.</summary>
	public Action<AbilityContext> OnKill { get; init; }

	/// <summary>Это существо получило урон (больше нуля).</summary>
	public Action<AbilityContext> OnDamaged { get; init; }

	/// <summary>Существо попало на линию, где ещё не было (в том числе при розыгрыше). TargetLane — эта линия.</summary>
	public Action<AbilityContext> OnEnterNewLane { get; init; }

	/// <summary>Для чар: существо, к которому они прикреплены, развернулось. Target — это существо.</summary>
	public Action<AbilityContext> OnHostUntapped { get; init; }

	// ───────── Активируемая способность ─────────

	/// <summary>
	/// Активируемая способность: игрок сам нажимает кнопку в окне карты.
	/// null — способность не активируется.
	/// </summary>
	public Action<AbilityContext> OnActivated { get; init; }

	/// <summary>Сколько маны стоит активация.</summary>
	public ManaCost[] ActivationCost { get; init; } = [];

	/// <summary>Сколько труда стоит активация.</summary>
	public int ActivationLaborCost { get; init; }

	/// <summary>При активации карта поворачивается, поэтому повёрнутую активировать нельзя.</summary>
	public bool ActivationTapsSelf { get; init; }

	/// <summary>Что выбрать перед активацией.</summary>
	public ActivationTarget ActivationTarget { get; init; }

	/// <summary>Дополнительное условие активации. null — условия нет.</summary>
	public Func<AbilityContext, bool> ActivationCondition { get; init; }

	/// <summary>
	/// Можно активировать, пока карта лежит в сбросе (если игрок контролирует сброс).
	/// Только для способностей без поворота и без привязки к месту на поле.
	/// </summary>
	public bool UsableFromDiscard { get; init; }
}
