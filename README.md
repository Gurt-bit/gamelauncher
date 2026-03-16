# GameLauncher (WPF)

A fullscreen, borderless WPF game launcher shell. The UI includes:

- A left sidebar for the game library
- A main preview area with hero banner and details
- A prominent **Launch** button and **Exit / Minimize** controls

## Requirements

- .NET SDK 8.0 or later installed (`https://aka.ms/dotnet-download`)
- Windows with WPF support

## How to build and run

From this folder:

```bash
dotnet build
dotnet run
```

The app opens in **fullscreen, borderless** mode. Press **Esc** or the **Exit** button to close.

## Configure your games

Edit `MainWindow.xaml.cs` and adjust the `_games` list:

```csharp
private readonly List<GameEntry> _games = new()
{
    new GameEntry("My Game", "Description here.", @"C:\Path\To\MyGame.exe"),
};
```

On launch, the app will attempt to start the configured executable using `Process.Start`.

