using System.Linq;

namespace CardGame;

/// <summary>
/// Одна часть цены карты: сколько маны и каких типов.
/// Если типов несколько, каждую единицу можно оплатить любым из них:
/// new ManaCost(1, ManaType.Food, ManaType.Gold) — «1 еда или 1 золото».
/// </summary>
public sealed class ManaCost
{
	public ManaCost(int amount, params ManaType[] types)
	{
		Amount = amount;
		Types = types;
	}

	public int Amount { get; }
	public ManaType[] Types { get; }

	/// <summary>Подходит любой тип маны (все 8).</summary>
	public bool IsAny => SameTypes(ManaTypeExtensions.AllTypes);

	/// <summary>Подходит любая элементальная мана (основные, кроме еды и золота).</summary>
	public bool IsElemental => SameTypes(ManaTypeExtensions.ElementalTypes);

	/// <summary>«4 Любых», «1 Элементальная», «1 Еда или Золото».</summary>
	public override string ToString()
	{
		if (IsAny)
			return $"{Amount} {(Amount == 1 ? Glossary.AnyMana : "Любых")}";
		if (IsElemental)
			return $"{Amount} {(Amount == 1 ? Glossary.ElementalMana : "Элементальных")}";
		return $"{Amount} {string.Join(" или ", Types.Select(t => t.ToRussian()))}";
	}

	// Порядок типов не важен, поэтому сравниваем как множества.
	private bool SameTypes(ManaType[] other) => Types.Length == other.Length && !Types.Except(other).Any();
}
