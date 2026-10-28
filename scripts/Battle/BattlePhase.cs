namespace CardGame;

/// <summary>Фазы одного хода, по порядку.</summary>
public enum BattlePhase
{
	/// <summary>Ходит текущий игрок.</summary>
	Supply,

	/// <summary>Ходит противоположный игрок.</summary>
	Diversion,

	/// <summary>Текущий игрок нападает своими существами.</summary>
	Attack,

	/// <summary>Противоположный игрок защищается, в конце фазы схватки.</summary>
	Defense,

	/// <summary>Ходит текущий игрок, после неё ход переходит к сопернику.</summary>
	Trophy,
}

public static class BattlePhaseExtensions
{
	public static string ToRussian(this BattlePhase phase) => phase switch
	{
		BattlePhase.Supply => "Снабжение",
		BattlePhase.Diversion => "Диверсия",
		BattlePhase.Attack => "Атака",
		BattlePhase.Defense => "Защита",
		BattlePhase.Trophy => "Трофеи",
		_ => phase.ToString(),
	};
}
