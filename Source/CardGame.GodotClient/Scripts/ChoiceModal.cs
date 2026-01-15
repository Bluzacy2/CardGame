using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ChoiceModal : Control
{
    [Export] public VBoxContainer MainPanel;
    [Export] public Label TitleLabel;

    private Action<int> _currentCallback;
    private VBoxContainer _dynamicButtonContainer;

    public override void _Ready()
    {
        Visible = false;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // Upewnij się, że tło jest na spodzie (jeśli w scenie jest bałagan)
        var bg = GetNodeOrNull<ColorRect>("ColorRect");
        if (bg != null) MoveChild(bg, 0);
    }

    public void SetCallback(Action<int> callback)
    {
        _currentCallback = callback;
    }

    // Nowa sygnatura: przyjmuje opcjonalną listę dostępności
    public void ShowOptions(IEnumerable<string> options, List<bool> enabledStates = null)
    {
        Visible = true;
        MoveToFront();

        if (MainPanel == null)
        {
            GD.PrintErr("CRITICAL: MainPanel is null in ChoiceModal!");
            return;
        }

        // Reset kontenera
        if (_dynamicButtonContainer != null)
        {
            _dynamicButtonContainer.QueueFree();
            _dynamicButtonContainer = null;
        }

        _dynamicButtonContainer = new VBoxContainer();
        _dynamicButtonContainer.AddThemeConstantOverride("separation", 15);
        _dynamicButtonContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _dynamicButtonContainer.SizeFlagsVertical = SizeFlags.ExpandFill;

        // Dodajemy kontener na przyciski do panelu głównego
        MainPanel.AddChild(_dynamicButtonContainer);

        int index = 0;
        var optionsList = options.ToList();

        for (int i = 0; i < optionsList.Count; i++)
        {
            string text = optionsList[i];
            bool isEnabled = (enabledStates == null) || (i >= enabledStates.Count) || enabledStates[i];

            var btn = new Button();
            btn.Text = text;
            btn.Disabled = !isEnabled; // Blokada systemowa

            btn.CustomMinimumSize = new Vector2(300, 60);
            btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // Stylizacja
            var styleNormal = new StyleBoxFlat();
            styleNormal.BgColor = new Color(0.1f, 0.3f, 0.8f); // Niebieski
            styleNormal.BorderColor = Colors.White;
            styleNormal.BorderWidthBottom = 2; styleNormal.BorderWidthTop = 2;
            styleNormal.BorderWidthLeft = 2; styleNormal.BorderWidthRight = 2;
            styleNormal.CornerRadiusTopLeft = 5; styleNormal.CornerRadiusTopRight = 5;
            styleNormal.CornerRadiusBottomRight = 5; styleNormal.CornerRadiusBottomLeft = 5;

            var styleDisabled = (StyleBoxFlat)styleNormal.Duplicate();
            styleDisabled.BgColor = new Color(0.2f, 0.2f, 0.2f); // Szary dla zablokowanego
            styleDisabled.BorderColor = new Color(0.5f, 0.5f, 0.5f);

            btn.AddThemeStyleboxOverride("normal", styleNormal);
            btn.AddThemeStyleboxOverride("hover", styleNormal);
            btn.AddThemeStyleboxOverride("pressed", styleNormal);
            btn.AddThemeStyleboxOverride("disabled", styleDisabled); // Styl zablokowany

            if (!isEnabled)
            {
                btn.AddThemeColorOverride("font_color_disabled", new Color(0.6f, 0.6f, 0.6f));
                btn.TooltipText = "Brak kart w tym źródle!";
            }
            else
            {
                btn.AddThemeColorOverride("font_color", Colors.White);
            }

            int capturedIndex = index;
            btn.Pressed += () => OptionClicked(capturedIndex);

            _dynamicButtonContainer.AddChild(btn);
            index++;
        }
    }

    private void OptionClicked(int index)
    {
        Visible = false;
        _currentCallback?.Invoke(index);

        if (_dynamicButtonContainer != null)
        {
            _dynamicButtonContainer.QueueFree();
            _dynamicButtonContainer = null;
        }
    }

    public void HideModal()
    {
        Visible = false;
    }
}