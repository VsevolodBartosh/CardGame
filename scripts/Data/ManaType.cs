using System;
using Godot;

namespace CardGame;

/// <summary>Типы маны: семь основных и один дополнительный (вера).</summary>
public enum ManaType
{
	Fire,
	Water,
	Light,
	Darkness,
	Grass,
	Gold,
	Food,
	Faith,
}

public static class ManaTypeExtensions
{
	/// <summary>Основные типы. Только для них есть карты оплаты.</summary>
	public static readonly ManaType[] MainTypes =
	[
		ManaType.Fire,
		ManaType.Water,
		ManaType.Light,
		ManaType.Darkness,
		ManaType.Grass,
		ManaType.Gold,
		ManaType.Food,
	];

	/// <summary>Все типы, включая веру. Для цен вида «4 любых».</summary>
	public static readonly ManaType[] AllTypes = Enum.GetValues<ManaType>();

	/// <summary>Элементальные: все основные, кроме еды и золота.</summary>
	public static readonly ManaType[] ElementalTypes =
	[
		ManaType.Fire,
		ManaType.Water,
		ManaType.Light,
		ManaType.Darkness,
		ManaType.Grass,
	];

	/// <summary>Название типа на русском, для отображения игроку.</summary>
	public static string ToRussian(this ManaType type) => type switch
	{
		ManaType.Fire => "Огонь",
		ManaType.Water => "Вода",
		ManaType.Light => "Свет",
		ManaType.Darkness => "Тьма",
		ManaType.Grass => "Трава",
		ManaType.Gold => "Золото",
		ManaType.Food => "Еда",
		ManaType.Faith => "Вера",
		_ => type.ToString(),
	};

	/// <summary>Временный значок маны: квадрат этого цвета.</summary>
	public static Color ToColor(this ManaType type) => type switch
	{
		ManaType.Fire => new Color(0.9f, 0.2f, 0.1f),
		ManaType.Water => new Color(0.15f, 0.45f, 0.95f),
		ManaType.Light => new Color(1f, 1f, 0.85f),
		ManaType.Darkness => new Color(0.3f, 0.1f, 0.4f),
		ManaType.Grass => new Color(0.2f, 0.75f, 0.2f),
		ManaType.Gold => new Color(1f, 0.7f, 0f),
		ManaType.Food => new Color(0.55f, 0.3f, 0.1f),
		ManaType.Faith => new Color(0.95f, 0.5f, 0.85f),
		_ => Colors.Gray,
	};
}
