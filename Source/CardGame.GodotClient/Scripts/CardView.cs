using Godot;
using System;
using System.Linq;
using CardGame.Core.Cards.Models;
using CardGame.Core.Cards.Data;

public partial class CardView : Control
{
    [ExportGroup("UI Elements")]
    [Export] public Label NameLabel;
    [Export] public Label CostLabel;
    [Export] public Label HpLabel;
    [Export] public Label AtkLabel;
    [Export] public RichTextLabel DescLabel;          // Zmienione na Label (zgodnie z Twoją prośbą)
    [Export] public RichTextLabel SubtypeLabel; // Nowe: RichTextLabel dla podtypów

    [Export] public TextureRect Background;    // ZMIENIONE: TextureRect zamiast ColorRect
    [Export] public TextureRect Illustration;  // Nowe: Miejsce na obrazek potwora
    [Export] public TextureRect AttackSquare;  // Nowe: Ramka ataku
    [Export] public TextureRect HealthSquare;  // Nowe: Ramka HP

    [Export] public Control HighlightBorder;
    [Export] public HBoxContainer KeywordContainer;
    [Export] public Control XOverlay;
    [Export] public Label CountBadge;

    public CardInstance MyCardData { get; private set; }
    public event Action<CardView> OnClicked;

    //public override void _Ready()
    //{
    // To jest baza - mniejsza karta w edytorze talii
    //Vector2 baseSize = new Vector2(200, 280);
    //CustomMinimumSize = baseSize;

    // Pozwalamy karcie wypełniać miejsce, które dostanie od rodzica
    //SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill;
    //SizeFlagsVertical = SizeFlags.Expand | SizeFlags.Fill;

    //MouseFilter = MouseFilterEnum.Stop;

    // To sprawia, że ramka zawsze wypełnia kartę, nieważne jaki ma rozmiar
    // if (Background != null)
    //{
    //    Background.SetAnchorsPreset(LayoutPreset.FullRect);
    //    Background.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
    //    Background.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
    //}
    //}

    public override void _Ready()
    {
        // 1. Podstawowy rozmiar (zmieniony na 200x280 dla lepszej wydajności w Gridzie)
        CustomMinimumSize = new Vector2(200, 280);
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true; // To "obetnie" rogi ilustracji, jeśli ramka ma być na wierzchu

        // 2. Naprawa Background (Twoje działające linie)
        if (Background != null)
        {
            Background.SetAnchorsPreset(LayoutPreset.FullRect);
            Background.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            Background.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

            // OPCJONALNIE: Jeśli chcesz, by ramka przycinała rogi dzieci:
            // Background.ClipChildren = FillModeEnum.ClipAndDraw;
        }

        // 3. AUTOMATYCZNA NAPRAWA DZIECI (Illustration, Atk, Hp itd.)
        // Ta pętla przejdzie przez wszystko co wrzuciłeś do środka karty
        foreach (var child in GetChildren())
        {
            if (child is Control control && child != Background)
            {
                // Pozwalamy kontenerom (np. Gridowi) zarządzać rozmiarem karty
                control.SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill;
                control.SizeFlagsVertical = SizeFlags.Expand | SizeFlags.Fill;

                // Jeśli element nie jest jeszcze dzieckiem tła, 
                // to jego 'procentowe' pozycjonowanie może szaleć.
                // Najlepiej ustawić im wszystkim Anchors w edytorze na 'Layout -> Anchors to Selection'
            }
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            OnClicked?.Invoke(this);
        }
    }

    public void Render(CardInstance card)
    {
        if (card == null) return;
        MyCardData = card;

        // 1. Podstawowe teksty
        if (NameLabel != null) NameLabel.Text = card.Definition.Name.ToUpper();
        if (DescLabel != null) DescLabel.Text = card.Definition.Description;

        if (SubtypeLabel != null)
        {
            SubtypeLabel.BbcodeEnabled = true;
            if (card.Definition.Subtypes != null)
                SubtypeLabel.Text = $"[center]- {string.Join(", ", card.Definition.Subtypes).ToUpper()} -[/center]";
        }

        // 2. Koszt i rzymska mana
        if (CostLabel != null)
        {
            CostLabel.Text = IntToRoman(card.CurrentStats.BloodCost);
            CostLabel.Modulate = card.CurrentStats.BloodCost < card.Definition.BaseStats.BloodCost ? Colors.Green : Colors.White;
        }

        // 3. Statystyki Jednostek
        if (card.Definition.Type == CardType.Unit)
        {
            ShowUnitStats(true);
            if (AtkLabel != null)
            {
                AtkLabel.Text = card.CurrentStats.Attack.ToString();
                AtkLabel.Modulate = card.CurrentStats.Attack > card.Definition.BaseStats.Attack ? Colors.Green : Colors.White;
            }
            if (HpLabel != null)
            {
                HpLabel.Text = card.CurrentStats.Health.ToString();
                if (card.DamageTaken > 0) HpLabel.Modulate = Colors.Red;
                else if (card.CurrentStats.Health > card.Definition.BaseStats.Health) HpLabel.Modulate = Colors.Green;
                else HpLabel.Modulate = Colors.White;
            }
            // Kolor ramki (SelfModulate zamiast Color)
            if (Background != null) Background.SelfModulate = Colors.White;
        }
        else
        {
            ShowUnitStats(false);
            if (Background != null) Background.SelfModulate = new Color(0.6f, 0.6f, 1.0f); // Niebieski dla czarów
        }

        // 4. Obrazek (Path: res://Assets/Cards/Cards/CardImages/id.png)
        if (Illustration != null)
        {
            string imgPath = $"res://Assets/Cards/Cards/CardImages/{card.Definition.Id}.png";
            if (FileAccess.FileExists(imgPath))
                Illustration.Texture = GD.Load<Texture2D>(imgPath);
            else
                Illustration.Texture = GD.Load<Texture2D>("res://Assets/Cards/Cards/missing_texture.png");
        }

        RenderKeywords(card);
    }

    private void ShowUnitStats(bool show)
    {
        if (AtkLabel != null) AtkLabel.Visible = show;
        if (HpLabel != null) HpLabel.Visible = show;
        if (AttackSquare != null) AttackSquare.Visible = show; // Ukrywamy też kwadraty
        if (HealthSquare != null) HealthSquare.Visible = show;
    }

    private string IntToRoman(int n)
    {
        string[] romans = { "0", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return (n >= 0 && n < romans.Length) ? romans[n] : n.ToString();
    }

    // --- LOGIKA ORYGINALNA (Highlight, Drag&Drop, Count) ---

    public void RenderCardBack()
    {
        MyCardData = null;
        ShowUnitStats(false);
        if (NameLabel != null) NameLabel.Visible = false;
        if (DescLabel != null) DescLabel.Visible = false;
        if (CostLabel != null) CostLabel.Visible = false;
        if (Background != null) Background.SelfModulate = new Color(0.3f, 0.15f, 0.05f);
    }

    public void SetMulliganSelected(bool selected)
    {
        if (XOverlay != null) XOverlay.Visible = selected;
        Modulate = selected ? new Color(0.6f, 0.6f, 0.6f) : new Color(1, 1, 1);
    }

    public void SetHighlight(bool active, Color color)
    {
        if (HighlightBorder != null)
        {
            HighlightBorder.Visible = active;
            HighlightBorder.Modulate = color;
        }
    }

    private void RenderKeywords(CardInstance card)
    {
        if (card.CurrentStats.Keywords.Contains(Keyword.SoulGuard)) SetHighlight(true, Colors.Gray);
        else if (card.CurrentStats.Keywords.Contains(Keyword.Marked)) SetHighlight(true, Colors.Red);
        else SetHighlight(false, Colors.White);
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (MyCardData == null) return default;
        var preview = new Control();
        var visual = (Control)Duplicate();
        preview.AddChild(visual);
        visual.MouseFilter = MouseFilterEnum.Ignore;
        visual.Position = new Vector2(-100, -140);
        visual.Modulate = new Color(1, 1, 1, 0.8f);
        visual.RotationDegrees = 5;
        SetDragPreview(preview);
        return MyCardData.InstanceId;
    }

    public void UpdateCount(int current, int max)
    {
        if (CountBadge == null) return;
        CountBadge.Visible = true;
        CountBadge.Text = $"{current}/{max}";
        CountBadge.Modulate = current >= max ? Colors.Red : Colors.White;
        Modulate = current >= max ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
    }
}