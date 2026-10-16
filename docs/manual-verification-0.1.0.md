# Manual verification, 0.1.0

Deploy the build (More Players and Admin Menu installed too). Tick each line.

## The tab
- [ ] Title screen → Options: a **Mods** tab follows Keybinds, styled like the others.
- [ ] Close and reopen Options twice: still exactly one Mods tab; Options opens on Gameplay and Mods content isn't showing under it.
- [ ] Shoulder buttons / tab-cycle keys cycle Gameplay → … → Keybinds → Mods → Gameplay.
- [ ] Controller/arrow keys: down from the Mods tab button reaches the Mod picker; down from the last row reaches Reset Defaults; up from the picker returns to the tab button.
- [ ] Pause menu → Options shows the same Mods tab.

## Contents
- [ ] The Mod picker lists Admin Menu, Mod Settings and More Players; ◀ ▶ (left/right or click) wraps around.
- [ ] More Players: "Max players" is a slider with a whole-number label; dragging it updates the label; after closing Options, `BepInEx/config/*moreplayers*.cfg` shows the new value.
- [ ] Admin Menu: "Toggle key" shows F1. Click it, press F2: it shows F2 and F2 now opens the admin menu (as host in a level).
- [ ] Click it again and press Esc: the rebind cancels, the label returns, and Options stays open.
- [ ] Click it and press Backspace: shows "Not bound".
- [ ] Admin Menu's on/off settings cycle On/Off.
- [ ] Reset Defaults on the Mods tab asks for confirmation, then resets only the selected mod.
- [ ] Mod Settings: "Open key" shows F10.
- [ ] Descriptions show as a smaller line under each name; sections have headings.

## F10
- [ ] Title screen: F10 opens Options on the Mods tab. Back returns to the title screen.
- [ ] In a run (pause menu closed): F10 opens Options on Mods; Back goes to the pause menu; Esc/Resume back to play.
- [ ] With the pause menu open: F10 opens Options on Mods.
- [ ] With Options open on Gameplay: F10 switches to Mods.
- [ ] With the Reset Defaults confirmation open: F10 does nothing.
- [ ] With the Admin Menu open (F1): F10 does nothing.
- [ ] On a loading screen: F10 does nothing.

## Persistence
- [ ] Change More Players' max players and Admin Menu's key, quit the game, relaunch: both changes are still there.
