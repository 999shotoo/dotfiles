# Windows dotfiles

My Windows desktop setup with an adaptive YASB bar, a shared Warm Charcoal palette, and the editor and terminal settings I work with.

## Main setup

| App | Repo path | Restore location |
| --- | --- | --- |
| YASB | [yasb/](yasb/) | %USERPROFILE%/.config/yasb/ |
| Windows Terminal | [terminal/windows-terminal/](terminal/windows-terminal/) | Settings JSON opened from the Terminal settings menu |
| WezTerm | [terminal/wezterm/wezterm.lua](terminal/wezterm/wezterm.lua) | %USERPROFILE%/.wezterm.lua |
| Zed | [editors/zed/settings.json](editors/zed/settings.json) | %APPDATA%/Zed/settings.json |
| PowerShell | [shell/powershell/](shell/powershell/) | The matching PowerShell or WindowsPowerShell folder under Documents |
| Fastfetch | [shell/fastfetch/](shell/fastfetch/) | %USERPROFILE%/.config/fastfetch/ |
| Git | [git/](git/) | Merge the example into ~/.gitconfig and copy ignore into ~/.config/git/ |
| Brave | [brave/](brave/) | Warm Charcoal browser theme |
| Windows theme | [theme/](theme/) | Apply the .theme file from a local copy after setting its wallpaper |

## Restore a config

1. Back up the destination file and copy the corresponding file or directory from the table.
2. In the copied files, replace __HOME__ with your Windows home path in forward-slash form, such as C:/Users/your-name. Point the YASB wallpaper gallery at your image folder. Keep network-status.ps1 next to config.yaml so the Wi-Fi indicator can run it.
3. Replace __WALLPAPER__ in the Windows theme with the full path to a local image before applying it. The wallpaper itself lives outside the repo.
4. Install the apps, fonts, and prompt tools needed for the configs you use. The PowerShell profiles use Starship, and the Windows PowerShell startup banner uses Fastfetch when installed. Open a new app window or reload the app to pick up restored settings. Merge the Git example into your existing settings so your identity and credential configuration stay local.

## YASB controls

- **Alt+Space** opens Quick Launch.
- **Alt+W** cycles windows with live previews.
- **Alt+E** opens the wallpaper gallery.
- Left-click the notification bell for the YASB panel. Right-click opens Windows Notification Center.
- Left-click the network icon for Windows Quick Settings. Right-click shows connection details.
- The power button opens a compact dropdown.

## Warm Charcoal

The shared [CSS tokens](theme/palette.css) are ready to copy into another project. Windows Terminal includes the corresponding ANSI palette in its settings file.

| Role | Color |
| --- | --- |
| Background | #262626 |
| Surface | #393633 |
| Hover and selection | #49433B |
| Borders | #49423A |
| Text | #D8C9A5 |
| Muted text | #AAA08C |
| Accent | #A8A34B |
| Destructive action | #C47788 |

## Brave theme

Warm Charcoal is a local browser theme from the brave folder. To restore it, run this from the repo root after closing Brave.

    & "$env:ProgramFiles/BraveSoftware/Brave-Browser/Application/brave.exe" "--load-extension=$((Resolve-Path ./brave).Path)"

Brave writes its local theme cache beside the manifest. Keep the theme directory in place while the theme is active.

## Komorebi

[Komorebi and WHKD](komorebi/) includes window rules, a bar config, and shortcut definitions.
