using System.Collections.Generic;
using System.Linq;

namespace CardGame;

/// <summary>
/// Расчёты оплаты. Цена раскладывается на отдельные единицы маны:
/// «2 огня + 1 еда или золото» → [Огонь], [Огонь], [Еда, Золото].
/// Каждую единицу оплачивают одним типом из её списка.
/// </summary>
public static class ManaPayment
{
	public static List<ManaType[]> ToUnits(IEnumerable<ManaCost> cost) =>
		cost.SelectMany(c => Enumerable.Repeat(c.Types, c.Amount)).ToList();

	/// <summary>Хватит ли маны в накопителе на все единицы.</summary>
	public static bool CanPay(IReadOnlyList<ManaType[]> units, Dictionary<ManaType, int> mana) =>
		CanPayFrom(units, 0, new Dictionary<ManaType, int>(mana));

	/// <summary>
	/// Какими типами можно оплатить единицу units[index],
	/// чтобы на остальные единицы маны ещё хватило.
	/// </summary>
	public static List<ManaType> ValidChoices(IReadOnlyList<ManaType[]> units, int index, Dictionary<ManaType, int> mana)
	{
		List<ManaType[]> rest = units.Where((_, i) => i != index).ToList();
		var left = new Dictionary<ManaType, int>(mana);
		var result = new List<ManaType>();

		foreach (ManaType type in units[index].Distinct())
		{
			if (left.GetValueOrDefault(type) == 0)
				continue;

			left[type]--;
			if (CanPay(rest, left))
				result.Add(type);
			left[type]++;
		}

		return result;
	}

	// Перебор с возвратом: пробуем каждый тип для текущей единицы,
	// и если дальше оплатить не выходит, возвращаем ману и пробуем следующий.
	private static bool CanPayFrom(IReadOnlyList<ManaType[]> units, int index, Dictionary<ManaType, int> mana)
	{
		if (index == units.Count)
			return true;

		foreach (ManaType type in units[index].Distinct())
		{
			if (mana.GetValueOrDefault(type) == 0)
				continue;

			mana[type]--;
			bool paid = CanPayFrom(units, index + 1, mana);
			mana[type]++;

			if (paid)
				return true;
		}

		return false;
	}
}
