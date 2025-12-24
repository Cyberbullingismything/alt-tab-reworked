# Smooth Tab Transition

A Windows application that provides a beautiful, smooth animated window switcher when you press Alt+Tab.

## Features

- 🎨 **Smooth Animations**: Elegant fade-in/fade-out transitions
- 🖼️ **Window Preview**: See all your open windows in a grid layout
- ⌨️ **Keyboard Navigation**: Use Alt+Tab to cycle through windows
- 🖱️ **Mouse Support**: Click on any window to switch to it
- 🎯 **Auto-Focus**: Automatically switches to the selected window when released

## Requirements

- Windows 10/11
- .NET 10.0 SDK or Runtime
- Administrator privileges (recommended for keyboard hook)

## Building

1. Make sure you have the .NET 8.0 SDK installed
2. Open a terminal in the project directory
3. Run:
   ```bash
   dotnet build
   ```

## Running

1. Build the project (see above)
2. Run the executable:
   ```bash
   dotnet run
   ```
   
   Or run the built executable from `bin/Debug/net10.0-windows/SmoothTabTransition.exe`

## Usage

1. Start the application (it will run in the background)
2. Press **Alt+Tab** to open the smooth window switcher
3. While holding Alt, press **Tab** again to cycle through windows
4. Release **Alt** to switch to the selected window
5. Or click on any window thumbnail to switch to it
6. Press **Escape** to close the switcher without switching

## How It Works

- The app uses a low-level keyboard hook to detect Alt+Tab key combinations
- When detected, it displays a fullscreen overlay with all open windows
- Windows are displayed in a grid with smooth animations
- The selected window is highlighted with a blue border
- When Alt is released, the app switches to the selected window

## Notes

- The application needs to run with elevated privileges for the keyboard hook to work properly
- Some system windows may be filtered out from the list
- The switcher shows up to 12 windows in a 4x3 grid

## Troubleshooting

If the Alt+Tab hook doesn't work:
- Try running the application as Administrator
- Make sure no other applications are intercepting Alt+Tab
- Check Windows security settings

## License

This project is provided as-is for educational and personal use.

