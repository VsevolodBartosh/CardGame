using System.Threading;
using System.Threading.Tasks;

namespace CardGame;

/// <summary>Помощники для async-кода в Godot.</summary>
public static class TaskHelpers
{
	/// <summary>
	/// Завершить задачу так, чтобы код после её await продолжился сразу и здесь же, в главном потоке.
	///
	/// Зачем: бой выполняет команды без SynchronizationContext (см. TestBattle.Network.cs), и код,
	/// который ждёт ответа игрока, «запомнил» пустой контекст. Если завершить задачу из обработчика кнопки,
	/// где контекст Godot есть, .NET не станет продолжать «чужой» код здесь и отправит его в пул потоков —
	/// а трогать узлы Godot из другого потока нельзя. Поэтому на время SetResult контекст убираем.
	/// </summary>
	public static void CompleteInline<T>(TaskCompletionSource<T> source, T result)
	{
		SynchronizationContext previous = SynchronizationContext.Current;
		SynchronizationContext.SetSynchronizationContext(null);
		try
		{
			source.SetResult(result);
		}
		finally
		{
			SynchronizationContext.SetSynchronizationContext(previous);
		}
	}
}
