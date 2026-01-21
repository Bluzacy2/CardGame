using Godot;
using System;

public partial class AudioManager : Node
{
	// Singleton - pozwala odwołać się do tego skryptu z każdego miejsca w grze
	public static AudioManager Instance { get; private set; }

	[Export] public AudioStreamPlayer MusicPlayer;

	private bool _isMusicOn = true;

	public override void _Ready()
	{
		Instance = this;
		
		// Jeśli nie przypisałeś w inspektorze, spróbuj znaleźć dziecko
		if (MusicPlayer == null)
			MusicPlayer = GetNode<AudioStreamPlayer>("AudioStreamPlayer");
			
		// Upewnij się, że gra (jeśli włączone w Autoplay, to to jest nadmiarowe, ale bezpieczne)
		if (MusicPlayer != null && !MusicPlayer.Playing && _isMusicOn)
		{
			MusicPlayer.Play();
		}
	}

	public void ToggleMusic()
	{
		if (MusicPlayer == null) return;

		_isMusicOn = !_isMusicOn;

		if (_isMusicOn)
		{
			if (!MusicPlayer.Playing) MusicPlayer.Play();
			// Opcjonalnie: Ustawienie głośności na normalną
			MusicPlayer.VolumeDb = 0; 
		}
		else
		{
			// Zatrzymanie lub wyciszenie
			// MusicPlayer.Stop(); // Stop resetuje utwór do początku
			MusicPlayer.VolumeDb = -80; // Wyciszenie (lepsze, bo utwór leci w tle)
		}
	}

	public bool IsMusicPlaying()
	{
		return _isMusicOn;
	}
}
