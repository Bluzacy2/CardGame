using Godot;
using CardGame.Core.Cards.Models;
using CardGame.Core.Cards.Data;

public partial class CardView : Control
{
    [Export] public Label NameLabel;
    [Export] public Label CostLabel;
    [Export] public Label HpLabel;
    [Export] public Label AtkLabel;
    [Export] public ColorRect Background;

    public CardInstance MyCardData { get; private set; }

    public override void _Ready()
    {
        // WYMUSZENIE USTAWIEŃ MYSZY KODEM (Dla pewności)
        this.MouseFilter = MouseFilterEnum.Stop; // Główny węzeł łapie mysz

        // Wszystkie dzieci ignorują mysz
        foreach (Node child in GetChildren())
        {
            if (child is Control c) c.MouseFilter = MouseFilterEnum.Ignore;
        }

        CustomMinimumSize = new Vector2(140, 190);
    }
    public void RenderCardBack()
    {
        // 1. Resetujemy dane (żeby nie dało się jej podnieść/podglądnąć)
        // MyCardData pozostaje null lub przypisujemy null, jeśli było coś wcześniej
        // Dzięki temu _GetDragData zwróci null i nie pozwoli chwycić karty.

        // 2. Ukrywamy teksty
        if (NameLabel != null) NameLabel.Visible = false;
        if (CostLabel != null) CostLabel.Visible = false;
        if (AtkLabel != null) AtkLabel.Visible = false;
        if (HpLabel != null) HpLabel.Visible = false;

        // 3. Zmieniamy wygląd na "Rewers"
        if (Background != null)
        {
            Background.Color = new Color(0.4f, 0.2f, 0.1f); // Brązowy kolor (Tył karty)
            // Jeśli masz teksturę rewersu, to tutaj byś ją załadował
        }

        // 4. Wyłączamy interakcję (opcjonalnie, choć brak MyCardData też to załatwia)
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Render(CardInstance card)
    {
        MyCardData = card;
        if (NameLabel != null) NameLabel.Visible = true;
        if (CostLabel != null) CostLabel.Visible = true;
        if (NameLabel != null) NameLabel.Text = card.Definition.Name;
        if (CostLabel != null) CostLabel.Text = card.CurrentStats.BloodCost.ToString();

        if (card.Definition.Type == CardType.Unit)
        {
            if (AtkLabel != null) AtkLabel.Text = card.CurrentStats.Attack.ToString();
            if (HpLabel != null) HpLabel.Text = card.CurrentStats.Health.ToString();
            if (Background != null) Background.Color = new Color(0.2f, 0.2f, 0.2f);
        }
        else
        {
            if (AtkLabel != null) AtkLabel.Text = "";
            if (HpLabel != null) HpLabel.Text = "";
            if (Background != null) Background.Color = new Color(0.1f, 0.1f, 0.4f);
        }
    }

    // --- DEBUGOWANIE KLIKNIĘCIA ---
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            GD.Print($"[DEBUG] Kliknięto w kartę: {MyCardData?.Definition.Name} (MouseFilter jest OK)");
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (MyCardData == null) return default;

        GD.Print("[DEBUG] Rozpoczynam przeciąganie!");

        // 1. Tworzymy kontener podglądu
        var preview = new Control();

        // 2. Klonujemy kartę
        var visual = (Control)Duplicate();

        // 3. NAJWAŻNIEJSZE: Najpierw dodajemy do drzewa!
        // Dzięki temu _Ready() wykona się teraz, ustawi MouseFilter na Stop...
        preview.AddChild(visual);

        // 4. ...a my TERAZ to nadpisujemy na Ignore.
        // Jeśli zrobisz to w odwrotnej kolejności, _Ready nadpisze Ignore z powrotem na Stop i zablokuje upuszczenie!
        visual.MouseFilter = MouseFilterEnum.Ignore;

        // 5. Usuwamy linię "SetScript", która powodowała błąd ObjectDisposedException
        // visual.SetScript(new Variant()); <--- TO BYŁ WINOWAJCA

        // 6. Ustawiamy wygląd
        visual.Position = new Vector2(-70, -95);
        visual.Modulate = new Color(1, 1, 1, 0.8f);
        visual.RotationDegrees = 5;

        // 7. Ustawiamy podgląd
        SetDragPreview(preview);

        return MyCardData.InstanceId;
    }
}