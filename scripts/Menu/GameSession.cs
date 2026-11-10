namespace CardGame;

/// <summary>
/// Настройки текущей партии, которые главное меню передаёт в бой.
/// static: сцена меню при переходе в бой удаляется, а эти значения должны дожить до боя.
/// </summary>
public static class GameSession
{
	/// <summary>Порт по умолчанию для онлайн-игры.</summary>
	public const int DefaultPort = 24565;

	/// <summary>false — тестовая битва за одним экраном, true — онлайн против другого компьютера.</summary>
	public static bool Online { get; set; }

	/// <summary>За кого играет этот компьютер: 0 — Игрок 1 (хост), 1 — Игрок 2 (подключившийся).</summary>
	public static int LocalPlayerIndex { get; set; }

	/// <summary>
	/// Зерно генератора случайных чисел. Одинаковое у обоих игроков,
	/// поэтому «случайные» перемешивания и доборы у них совпадают.
	/// </summary>
	public static int Seed { get; set; }
}
