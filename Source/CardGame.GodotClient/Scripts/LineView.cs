using Godot;
using System;
using CardGame.Core.State.Models;
using CardGame.Core.Cards.Models;
/// <summary>
/// Visual representation of a single board line (lane).
/// Manages two slots: one for the enemy unit (Top) and one for the player unit (Bottom).
/// </summary>
public partial class LineView : Control
{
	[Export] public Label InfoLabel;
	[Export] public Control EnemySlot;
	[Export] public Control PlayerSlot;
    /// <summary>
    /// Signal emitted when a card is dropped onto this line.
    /// </summary>
    [Signal] public delegate void CardDroppedOnLineEventHandler(int cardInstanceId, int lineIndex);

	private int _myIndex;
	private readonly string[] _romans = { "I", "II", "III", "IV" };

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
	}

    /// <summary>
    /// Renders the current state of the line based on game data.
    /// </summary>
    /// <param name="lineData">Data model for the line.</param>
    /// <param name="cardScene">Template scene for instantiating cards.</param>
    /// <param name="onClickHandler">Action to bind to card click events (for targeting).</param>
    public void Render(Line lineData, PackedScene cardScene, Action<CardView> onClickHandler)
	{
		_myIndex = lineData.Index;

		if (InfoLabel != null)
		{
			InfoLabel.Text = _romans[_myIndex];
			InfoLabel.HorizontalAlignment = HorizontalAlignment.Center;
			InfoLabel.VerticalAlignment = VerticalAlignment.Center;
			InfoLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			InfoLabel.Modulate = new Color(1, 1, 1, 0.2f); 
		}

		ClearSlot(EnemySlot);
		ClearSlot(PlayerSlot);
		if (lineData.Player2Unit != null)
		{
			var botUnit = cardScene.Instantiate<CardView>();
			EnemySlot.AddChild(botUnit);
			botUnit.Render(lineData.Player2Unit);

			botUnit.MouseFilter = MouseFilterEnum.Stop; 
			botUnit.Modulate = new Color(1, 0.8f, 0.8f);

			if (onClickHandler != null) botUnit.OnClicked += onClickHandler;
		}
		if (lineData.Player1Unit != null)
		{
			var myUnit = cardScene.Instantiate<CardView>();
			PlayerSlot.AddChild(myUnit);
			myUnit.Render(lineData.Player1Unit);

			myUnit.MouseFilter = MouseFilterEnum.Stop;
			if (onClickHandler != null) myUnit.OnClicked += onClickHandler;
		}
	}

	private void ClearSlot(Control slot)
	{
		if (slot == null) return;
		foreach (Node child in slot.GetChildren())
		{
            // Preserve the Ghost Unit (recognized by transparency)
            // UIManager manages the Ghost lifecycle, LineView shouldn't delete it during re-render
            if (child is CardView cv && cv.Modulate.A < 0.9f) continue;
			child.QueueFree();
		}
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.VariantType == Variant.Type.Int;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		int cardId = (int)data;
		EmitSignal(SignalName.CardDroppedOnLine, cardId, _myIndex);
	}
}
