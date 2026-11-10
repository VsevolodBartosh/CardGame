using System.Collections.Generic;
using System.Linq;

namespace CardGame;

/// <summary>
/// Часть TestBattle: стопки одинаковых карт на линиях.
///
/// Стопка — это не отдельный объект, а несколько обычных карт на одной линии:
/// верхняя (StackLeader == null, у неё StackMembers) видна, остальные спрятаны (Visible = false).
/// Контейнер линии спрятанные карты не показывает и места под них не оставляет.
/// Все правила боя продолжают работать с каждой картой по отдельности, а действие
/// над верхней картой просто повторяется для всей стопки.
/// </summary>
public partial class TestBattle
{
	/// <summary>
	/// Пересобрать стопки на всех линиях. Вызывается в начале RefreshView, поэтому
	/// стоит одной карте стать «не такой» (ранена, повёрнута, получила снаряжение),
	/// она сама выпадает из стопки.
	/// </summary>
	private void Restack()
	{
		foreach (Lane lane in AllLanes)
		{
			List<Card> creatures = lane.Creatures.ToList();
			foreach (Card card in creatures)
				card.ClearStack();

			var groups = creatures
				.Where(c => !c.NoStack)
				.Select(c => (Card: c, Key: StackKey(c)))
				.Where(x => x.Key != null)
				.GroupBy(x => x.Key, x => x.Card);

			foreach (IGrouping<string, Card> group in groups)
			{
				Card leader = group.First();
				foreach (Card member in group.Skip(1))
					leader.AddToStack(member);
			}
		}
	}

	/// <summary>
	/// «Отпечаток» карты: одинаковый отпечаток — карты полностью идентичны и складываются.
	/// null — карта не складывается ни с кем (на ней что-то прикреплено, на неё напали, она защищает).
	/// </summary>
	private string StackKey(Card card)
	{
		if (card.Attachments.Count > 0)
			return null;
		if (_attacks.Any(a => a.TargetCard == card || a.Defender == card))
			return null;

		// Нападающие складываются, только если нападают на одно и то же.
		Attack attack = _attacks.FirstOrDefault(a => a.Attacker == card);
		string role = attack == null
			? "-"
			: $"A:{attack.TargetCard?.GetInstanceId()}:{attack.TargetLane.Row}:{_attacks.Count(a => a.Attacker == card)}";

		string ammo = string.Join(",", card.Abilities.Where(a => a.Shot != null).Select(a => card.AmmoLeft(a)));
		string lanes = string.Join(",", card.VisitedLanes.Select(l => l.Row).OrderBy(r => r));

		return string.Join("|",
			card.Data.Id, card.Player.Name, card.OwnerPlayer?.Name,
			card.CurrentHealth, card.BonusAttack, card.BonusDefense, card.BonusHealth,
			card.IsTapped, card.StunTurns, card.ImmobilizedBy?.Name,
			card.LaborPaid, ammo, card.ChosenMana, card.StoredCards.Count, card.CostDiscount,
			card.TempAttack, card.IsPoisoned, card.LaborCount,
			lanes, role);
	}

	/// <summary>Двойной клик: стопку разложить, разложенные одинаковые карты — сложить обратно.</summary>
	private void OnCardDoubleClicked(Card card)
	{
		if (IsBusy || LaneOf(card) == null)
			return;

		if (card.StackMembers.Count > 0)
		{
			foreach (Card member in card.WholeStack)
				member.NoStack = true;
		}
		else if (card.NoStack)
		{
			string key = StackKey(card);
			foreach (Card same in LaneOf(card).Creatures.Where(c => c.NoStack && key != null && StackKey(c) == key))
				same.NoStack = false;
		}

		RefreshView();
	}

	/// <summary>Вся стопка, к которой относится карта (или только сама карта).</summary>
	private static IEnumerable<Card> StackOf(Card card) => (card.StackLeader ?? card).WholeStack;
}
