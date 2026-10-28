using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace CardGame;

/// <summary>
/// Окно выбора из нескольких вариантов: «Как оплатить?», «Какую ману получить?» и т.п.
/// Корень сцены choice_view.tscn. Ask возвращает Task с номером выбранного варианта.
/// Если вопросов несколько сразу (две «Зависимости» в трофеях), они показываются по очереди.
/// </summary>
public partial class ChoiceView : Control
{
	private Label _title;
	private VBoxContainer _options;

	private readonly Queue<Question> _queue = new();
	private Question _current;

	public override void _Ready()
	{
		_title = GetNode<Label>("Center/Panel/Column/Title");
		_options = GetNode<VBoxContainer>("Center/Panel/Column/Options");
		Hide();
	}

	public Task<int> Ask(string title, IReadOnlyList<string> options)
	{
		var question = new Question(title, options, new TaskCompletionSource<int>());
		_queue.Enqueue(question);
		if (_current == null)
			ShowNext();
		return question.Result.Task;
	}

	private void ShowNext()
	{
		if (!_queue.TryDequeue(out _current))
		{
			Hide();
			return;
		}

		_title.Text = _current.Title;

		foreach (Node child in _options.GetChildren())
		{
			_options.RemoveChild(child);
			child.QueueFree();
		}

		for (int i = 0; i < _current.Options.Count; i++)
		{
			int index = i; // Копия для лямбды: иначе все кнопки вернули бы последнее значение i.
			var button = new Button { Text = _current.Options[i] };
			button.Pressed += () => Choose(index);
			_options.AddChild(button);
		}

		Show();
	}

	private void Choose(int index)
	{
		Question answered = _current;
		_current = null;
		ShowNext();
		answered.Result.SetResult(index);
	}

	private sealed record Question(string Title, IReadOnlyList<string> Options, TaskCompletionSource<int> Result);
}
