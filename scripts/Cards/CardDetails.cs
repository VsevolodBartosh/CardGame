using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardGame;

/// <summary>
/// Окно подробностей карты (по макету): слева картинка и характеристики,
/// справа название, типы, цена и описание способностей, ещё правее — пояснения к ключевым словам.
/// Корень сцены card_details.tscn. Разыгрывают карты перетаскиванием, здесь просмотр и активация.
/// </summary>
public partial class CardDetails : Control
{
    /// <summary>Игрок нажал кнопку активации способности abilityId этой карты.</summary>
    [Signal]
    public delegate void ActivateRequestedEventHandler(Card card, string abilityId);

    private Card _card;
    private VBoxContainer _activations;
    private TextureRect _image;
    private ColorRect _manaMark;
    private Control _stats;
    private Label _attack;
    private Label _defense;
    private Label _health;
    private Label _block;
    private Label _name;
    private Label _types;
    private Label _manaCost;
    private Label _description;
    private Control _glossaryPanel;
    private Label _glossaryText;

    public override void _Ready()
    {
        const string left = "Center/Columns/LeftPanel/Left";
        const string right = "Center/Columns/RightPanel/Right";

        _image = GetNode<TextureRect>($"{left}/Image");
        _manaMark = GetNode<ColorRect>($"{left}/Image/ManaMark");
        _stats = GetNode<Control>($"{left}/Stats");
        _attack = GetNode<Label>($"{left}/Stats/Attack");
        _defense = GetNode<Label>($"{left}/Stats/Defense");
        _health = GetNode<Label>($"{left}/Stats/Health");
        _block = GetNode<Label>($"{left}/Stats/Block");
        _name = GetNode<Label>($"{right}/NameBox/Name");
        _types = GetNode<Label>($"{right}/TypeRow/TypesBox/Types");
        _manaCost = GetNode<Label>($"{right}/TypeRow/ManaBox/ManaCost");
        _description = GetNode<Label>($"{right}/DescriptionBox/DescriptionColumn/Description");
        _activations = GetNode<VBoxContainer>($"{right}/Activations");
        _glossaryPanel = GetNode<Control>("Center/Columns/GlossaryPanel");
        _glossaryText = GetNode<Label>("Center/Columns/GlossaryPanel/Column/Text");

        GetNode<Button>($"{right}/Buttons/CloseButton").Pressed += Close;
        GetNode<Control>("Dimmer").GuiInput += OnDimmerGuiInput;

        Hide();
    }

    /// <summary>
    /// Показать окно для карты. activatable — способности, которые сейчас можно активировать:
    /// для каждой появится своя кнопка.
    /// </summary>
    public void Open(Card card, IEnumerable<AbilityData> activatable = null)
    {
        _card = card;
        CardData data = card.Data;

        _image.Texture = GD.Load<Texture2D>(data.ImagePath);
        _manaMark.Visible = data.IsPayment;
        if (data.PaymentManaType is { } manaType)
            _manaMark.Color = manaType.ToColor();

        _stats.Visible = card.IsCreature;
        _attack.Text = card.Attack.ToString();
        _defense.Text = card.Defense.ToString();
        _health.Text = card.CurrentHealth.ToString();
        _block.Text = card.Block.ToString();

        _name.Text = data.Name;
        _types.Text = TypesText(data);
        _manaCost.Text = $"Цена: {CostText(data)}";
        _description.Text = card.Abilities.Count == 0
            ? "Нет способностей."
            : string.Join("\n", card.Abilities.Select(a => AbilityLine(card, a)));

        if (card.IsTapped)
            _description.Text += "\n\n(Карта повёрнута.)";
        if (card.IsImmobilized)
            _description.Text += "\n(Существо обездвижено.)";
        if (card.IsPoisoned)
            _description.Text += "\n(Отравлено.)";
        if (card.TempAttack > 0)
            _description.Text += $"\n(Атака +{card.TempAttack} до конца хода.)";
        if (card.LaborCount > 0)
            _description.Text += $"\n(Трудилось раз: {card.LaborCount}.)";
        if (card.IsStunned)
            _description.Text += $"\n(Оглушено: пропустит разворот ещё {card.StunTurns} раз.)";
        if (card.StackMembers.Count > 0)
            _description.Text += $"\n(Стопка: {card.StackMembers.Count + 1} карт. Двойной клик — разложить.)";
        if (card.StoredCards.Count > 0)
            _description.Text += $"\n(На карте лежит существ: {card.StoredCards.Count}.)";
        if (card.CostDiscount > 0)
            _description.Text += $"\n(Цена уменьшена на {card.CostDiscount}.)";
        if (card.Player != null && card.OwnerPlayer != null && card.Player != card.OwnerPlayer)
            _description.Text += $"\n(До конца хода под контролем: {card.Player.Name}.)";
        if (card.ChosenMana is { } chosen)
            _description.Text += $"\n(Выбран тип маны: {chosen.ToRussian()}.)";

        FillActivations(activatable ?? []);
        FillGlossary(card);
        Show();
    }

    public void Close()
    {
        _card = null;
        Hide();
    }

    // «Существо, Герой — Демон»: тип карты, отметка героя, затем тэги.
    private static string TypesText(CardData data)
    {
        var kinds = data.Types.Select(t => t.ToRussian()).ToList();
        if (data.IsHero)
            kinds.Add("Герой");

        string text = kinds.Count == 0 ? "—" : string.Join(", ", kinds);
        if (data.Tags.Length > 0)
            text += " — " + string.Join(", ", data.Tags.Select(t => t.ToRussian()));
        return text;
    }

    // У выстрела с боезапасом показываем, сколько выстрелов осталось.
    private static string AbilityLine(Card card, AbilityData ability)
    {
        string line = $"{ability.Name}: {ability.Description}";
        if (ability.Shot != null && card.AmmoLeft(ability) is { } left)
            line += $" Осталось выстрелов: {left}.";
        return line;
    }

    // «2 Золото или 1 Вера»: основная цена и альтернативные через «или».
    private static string CostText(CardData data)
    {
        if (data.XCost)
            return "X";

        var main = new CostOption { Mana = data.Cost, Labor = data.LaborCost, Discard = data.DiscardCost };
        string text = data.Cost.Length == 0 && data.LaborCost == 0 && data.DiscardCost == 0 ? "0" : main.ToString();
        foreach (CostOption alt in data.AlternativeCosts)
            text += $" или {alt}";
        return text;
    }

    private void FillActivations(IEnumerable<AbilityData> abilities)
    {
        foreach (Node child in _activations.GetChildren())
        {
            _activations.RemoveChild(child);
            child.QueueFree();
        }

        foreach (AbilityData ability in abilities)
        {
            var button = new Button { Text = $"Активировать: {ability.Name}" };
            button.Pressed += () => OnActivatePressed(ability.Id);
            _activations.AddChild(button);
        }
    }

    // Справа пояснения к словам из GlossaryTerms всех способностей карты
    // и к словам «Любая» / «Элементальная», если они встречаются в ценах.
    private void FillGlossary(Card card)
    {
        List<string> terms = card.Abilities.SelectMany(a => a.GlossaryTerms).ToList();

        IEnumerable<ManaCost> costs = card.Data.Cost
            .Concat(card.Data.AlternativeCosts.SelectMany(o => o.Mana))
            .Concat(card.Abilities.SelectMany(a => a.ActivationCost.Concat(a.LazinessCost)));
        foreach (ManaCost cost in costs)
        {
            if (cost.IsAny)
                terms.Add(Glossary.AnyMana);
            else if (cost.IsElemental)
                terms.Add(Glossary.ElementalMana);
        }

        terms = terms.Distinct().ToList();
        _glossaryPanel.Visible = terms.Count > 0;
        _glossaryText.Text = string.Join("\n\n", terms.Select(t => $"{t}: {Glossary.Terms[t]}"));
    }

    private void OnActivatePressed(string abilityId)
    {
        Card card = _card;
        Close();
        EmitSignal(SignalName.ActivateRequested, card, abilityId);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // Клик по затемнённому фону вокруг окна закрывает его.
    private void OnDimmerGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            Close();
    }
}
