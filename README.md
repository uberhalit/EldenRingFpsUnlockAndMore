# EXPERIMENTAL! NOT YET TESTED MUCH
Please make sure your game is set to offline and you start the game without EAC through the utility.

# Elden Ring FPS Unlocker and more

A small utility to remove frame rate limit, change FOV (Field of View), alter Game Speed and various game modifications for [Elden Ring](https://en.bandainamcoent.eu/elden-ring/elden-ring) written in C#. More features soon!
Patches games memory while running, does not modify any game files. Works with every game version (legit steam & oh-not-so-legit), should work with all future updates.

## Download

**[Get the latest release here](https://github.com/uberhalit/EldenRingFpsUnlockAndMore/releases)**

## Features

* does not modify any game files, RAM patches only
* works with legit, unmodified steam version as well as with unpacked, not-so-legit versions
* unlock frame rate (remove FPS limit) by setting a new custom limit (currently limited to (borderless) window mode)
* increase or decrease field of view (FOV)
* game modifications
  * global game speed modifier (increase or decrease)
  * disable losing Runes on death

## Usage

The graphic setup has to be done only once but as the patcher hot-patches the memory **you have to start the patcher every time you want to use any of its features**.

The game enforces VSYNC and forces 60 Hz in fullscreen even on 144 Hz monitors so we have to override these.

#### Nvidia: Use Nvidia Control Panel to set 'Vsync' to 'Off' and 'Preferred Refreshrate' to 'Highest available' on a Elden Ring Profile, then use borderless windows mode.
#### AMD: Use Radeon Settings to set 'Wait for Vertical Refresh' to 'Enhanced Sync' on a Elden Ring profile, then use borderless windows mode.

### Follow these steps on Nvidia:
1. Open Nvidia Control Panel
2. Navigate to `Display -> Change resolution`
3. **Make sure your monitor is set to the highest Refresh rate possible:**
4.  [![Make sure your monitor is set to the highest Refresh rate possible](https://camo.githubusercontent.com/331eb420bee67f4e57d7e46601bfd51f462de68f/68747470733a2f2f692e696d6775722e636f6d2f625667767155372e706e67)](#)
5. Navigate to `3D Settings -> Manage 3D settings -> Program Settings -> Elden Ring`
6. **Set `Preferred refresh rate` to `Highest available`**
7. **Set `Vertical sync` to `Off`**
8. Hit apply and close Nvidia Control Panel
9. Start `Elden Ring FPS Unlocker and more` and start the game through the first button
10. Use borderless window mode for now
11. Set your new refresh rate limit, tick the checkbox and click `Patch game`

### Follow these steps on AMD:
1. Right click on Desktop -> `Display settings`
2. Scroll down and click `Advanced Display Settings -> Display Adapter Properties`
3. **Switch to `Monitor` tab and make sure your monitor is set to the highest Refresh rate possible:**
4. Open Radeon Settings
5. Navigate to `Gaming -> Elden Ring` or add it manually if it's missing: `Add -> Browse -> Elden Ring`
6. **Set `Wait for Vertical Refresh` to `Enhanced Sync`**:
7. Apply and close Radeon Settings
8. Start `Elden Ring FPS Unlocker and more` and start the game through the first button
9. Use borderless window mode for now
10. Set your new refresh rate limit, tick the checkbox and click `Patch game`

### To play the game with GSYNC do these additional steps (Nvidia):
1. Under Nvidia Control Panel navigate to `3D Settings -> Manage 3D settings -> Program Settings -> Elden Ring`
2. Set `Monitor Technology` to `G-SYNC`
3. You can keep `Vertical sync` on `Use the 3D application setting` now to help remove frame time stutters ([see here](https://www.blurbusters.com/gsync/gsync101-input-lag-tests-and-settings/15/))
4. Make sure that `Preferred refresh rate` is still set to `Highest available`
5. Don't forget to Apply and close Nvidia Control Panel
6. Use a 3rd party frame rate limiter like [RTSS](https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html) and set a frame rate limit just a few fps below your monitor refresh rate, on a 144Hz Monitor use 138
7. Start `Elden Ring FPS Unlocker and more` and set FPS lock to your monitors refresh rate
8. Start the game and set it to borderless window

### To add a custom resolution:
*soon!*

### To use the FOV changer:
1. Set a new FOV value
2. Tick the checkbox and confirm with `Patch game`

### On 'Disable Runes loss on death':
Like 'Unseen Aid' in Sekiro you will not lose any Runes upon death with this option enabled.

### On 'Game speed':
Slow down the game to beat a boss like a game journalist or speed it up and become gud. Game speed acts as a global time scale and is used by the game itself to create a dramatic effect in a few cutscenes. All game physics (even opening the menu) will be affected equally: all time-critical windows like dodge and deflect will be proportionally prolonged or shortened while the amount of damage given and taken as well as all other damage physics will be unaltered. A hit from an enemy on 150% game speed will do the exact same damage as on 80%, the deflect window on 50% is exactly twice as long as on 100% and so on. Of course, your character will be affected by the speed too so even though a time window might be different now, the speed which you can react on it is different too. Be aware that the speed modifier can potentially crash the game in certain cutscenes and NPC interactions so use it with caution.

## Troubleshooting:
* Make sure you followed the appropriate steps and didn't skip any
* Try disabling `Fullscreen optimization` for Elden Ring: right mouse click on `eldenring.exe -> Compatibility-> tick 'Disable fullscreen optimizations'`
* If you are using ReShade make sure your preset doesn't enforce 60 Hz, try removing ReShade and see if it solves the problem
* Try adding the whole game folder and `Elden Ring FPS Unlocker and more` to your antivirus's exclusion list
* Try disabling `Steam Broadcast` (streaming via overlay)
* Try to force disable VSYNC even when you are using GSYNC
* Close and disable all screen recording and streaming applications
* Close and disable all overlays
* Close and disable all performance "booster" programs and alike
* Do a clean reinstall of your graphic driver:
  1. Download latest graphics driver for your GPU
  2. Download [DDU](https://www.guru3d.com/files-get/display-driver-uninstaller-download,1.html)
  3. Disconnect internet so windows update won't auto-install minimal driver as soon as you uninstall them
  4. Boot into safe mode
  5. Completely uninstall graphics driver and all of their utilities using DDU
  6. Reboot
  7. Install the latest driver you previously downloaded
  8. Reconnect internet

## Prerequisites

* .NET Framework 4.8
* administrative privileges (for patching)
* 64 bit OS

## Building

Use Visual Studio 2022 to build

## Contributing

Feel free to open an issue or create a pull request at any time

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## Credits
* [Darius Dan](http://www.dariusdan.com) for the icon

## Limitations

* the game has forced VSYNC so unlocking the frame rate when your monitor has 60Hz will do nothing. You'll have to disable VSYNC in Nvidia Control Panel or AMD Radeon Settings first, see Usage
* in fullscreen the game forces the monitor to 60 Hz and there is currently no known way to bypass this, user borderless window mode
* game speed modification can potentially crash the game in certain cutscenes and NPC interactions, use with caution

## Version History
* v0.0.0.4-beta (2022-02-26)
  * fixed issues with FOV changer
  * added game speed modifier
  * added option to disable Runes penalty upon death
  * fixed game exe selection if exe isn't called 'eldenring.exe'
  * improved stability
* v0.0.0.3-beta (2022-02-25)
  * added FOV changer
  * added handling of alternative version of EAC service (thanks to [DubbleClick](https://github.com/DubbleClick))
  * added handling of non-english characters in installation paths (thanks to [mrdellis](https://github.com/mrdellis))
* v0.0.0.2-beta (2022-02-25)
  * added game checks
  * fixed broken game start
  * added prompt to select game installation path
  * removed reference to external MS DLL
  * multiple fixes
  * added icon
* v0.0.0.1-beta (2022-02-25)
  * Initial release
