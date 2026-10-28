using System.Collections.Generic;
using System.Linq;

namespace CardGame;

/// <summary>
/// Один способ оплатить карту: мана, труд и/или сброс карт из руки.
/// У карты основной способ — Cost + LaborCost, остальные лежат в AlternativeCosts.
/// </summary>
public sealed class CostOption
{
	public ManaCost[] Mana { get; init; } = [];
	public int Labor { get; init; }

	/// <summary>Сколько карт из руки сбросить (игрок выбирает какие).</summary>
	public int Discard { get; init; }

	/// <summary>Текст для окна «Как оплатить?»: «2 Золото + 1 сброс карты».</summary>
	public override string ToString()
	{
		var parts = new List<string>(Mana.Select(m => m.ToString()));
		if (Labor > 0)
			parts.Add($"{Labor} труда");
		if (Discard > 0)
			parts.Add($"сбросить {Discard} карт(у)");
		return parts.Count == 0 ? "бесплатно" : string.Join(" + ", parts);
	}
}
