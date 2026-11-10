using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CardGame;

/// <summary>
/// Часть TestBattle: начало игры. Драфт героев, выбор героя на поле, восемь кругов драфта карт,
/// потом в колоды добавляются оплаты и игроки берут стартовую руку.
/// </summary>
public partial class TestBattle
{
	/// <summary>Сколько карт открывается в каждом круге драфта.</summary>
	private const int DraftRoundSize = 5;

	/// <summary>Сколько кругов драфта: 8 × 5 = 40 карт, по 20 каждому игроку.</summary>
	private const int DraftRounds = 8;

	private DraftView _draftView;

	private void ReadyDraftUi()
	{
		_draftView = GetNode<DraftView>("DraftView");
		_draftView.CardDetailsRequested += card => _cardDetails.Open(card);
	}

	/// <summary>
	/// async void: метод запускается из _Ready и сам ждёт выборов игроков через await.
	/// Пока игрок думает, игра не «висит»: Godot продолжает рисовать кадры, а метод
	/// продолжится с того же места, когда игрок нажмёт «Готово».
	/// </summary>
	private async void StartGame()
	{
		PlayerState first = _bottomPlayer;
		PlayerState second = _topPlayer;

		// 1. Герои: первый берёт 1 из 4, второй — 2 из 3 оставшихся, последний достаётся первому.
		List<string> heroes = CardsData.All.Where(c => c.IsHero).Select(c => c.Id).ToList();
		List<string> firstHeroes = await Pick(first, "выберите 1 героя", heroes, 1);
		List<string> rest = heroes.Except(firstHeroes).ToList();
		List<string> secondHeroes = await Pick(second, "выберите 2 героев", rest, 2);
		firstHeroes.AddRange(rest.Except(secondHeroes));

		// 2. Каждый выбирает, какого героя выставить на поле. Второй идёт в колоду.
		string firstOnField = (await Pick(first, "какого героя выставить на поле?", firstHeroes, 1))[0];
		string secondOnField = (await Pick(second, "какого героя выставить на поле?", secondHeroes, 1))[0];

		// 3. Круги драфта. Каждая карта существует в одном экземпляре на обе колоды,
		//    поэтому открытые карты сразу убираются из пула.
		string[] shuffled = CardsData.All.Where(c => !c.IsPayment && !c.IsHero && c.InDraft).Select(c => c.Id).ToArray();
		if (shuffled.Length < DraftRoundSize * DraftRounds)
			throw new InvalidOperationException($"Для драфта нужно минимум {DraftRoundSize * DraftRounds} карт, сейчас {shuffled.Length}.");
		_rng.Shuffle(shuffled);
		var pool = new Queue<string>(shuffled);

		// Первым выбирают по очереди: в нечётных кругах Игрок 2, в чётных Игрок 1.
		var picks = new Dictionary<PlayerState, List<string>> { [first] = [], [second] = [] };
		for (int round = 1; round <= DraftRounds; round++)
		{
			PlayerState opener = round % 2 == 1 ? second : first;
			await DraftRound(pool, picks, opener, OpponentOf(opener), round);
		}

		// 4. Колоды: выбранные карты + герой не на поле + оплаты.
		//    Цена героя на поле на оплаты не влияет: его нет в колоде.
		first.Deck.AddRange(picks[first]);
		first.Deck.AddRange(firstHeroes.Where(id => id != firstOnField));
		second.Deck.AddRange(picks[second]);
		second.Deck.AddRange(secondHeroes.Where(id => id != secondOnField));
		foreach (PlayerState player in Players)
			player.Deck.AddRange(PaymentCardsFor(player.Deck));

		PlaceOnLane(CreateCard(firstOnField, first, first.Hand), first.NearestLane);
		PlaceOnLane(CreateCard(secondOnField, second, second.Hand), second.NearestLane);

		for (int i = 0; i < StartingHandSize; i++)
		{
			DrawCard(first);
			DrawCard(second);
		}

		RefreshView();
	}

	/// <summary>
	/// Один круг: открывается 5 карт. opener берёт 1, остальные 4 уходят other,
	/// тот берёт 2, последние 2 достаются opener.
	/// </summary>
	private async Task DraftRound(Queue<string> pool, Dictionary<PlayerState, List<string>> picks,
		PlayerState opener, PlayerState other, int round)
	{
		List<string> cards = Enumerable.Range(0, DraftRoundSize).Select(_ => pool.Dequeue()).ToList();

		List<string> openerPick = await Pick(opener, $"драфт, круг {round}: выберите 1 карту", cards, 1);
		cards = cards.Except(openerPick).ToList();

		List<string> otherPick = await Pick(other, $"драфт, круг {round}: выберите 2 карты", cards, 2);
		cards = cards.Except(otherPick).ToList();

		picks[opener].AddRange(openerPick);
		picks[opener].AddRange(cards);
		picks[other].AddRange(otherPick);
	}

	private Task<List<string>> Pick(PlayerState player, string text, IEnumerable<string> ids, int count) =>
		PickAsync(player, text, ids, count, count);
}
