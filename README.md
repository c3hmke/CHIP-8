# CHIP-8 Emulator

This project was used to cut my teeth on implementing hardware emulators and run into the pitfalls of linking that to a UI before proceeding a something more substantial.
It's rough around the edges and I don't plan on updating this, instead I'm working on [Ostrich](https://github.com/c3hmke/Ostrich), a GameBoy emulator.

There is currently no input remapping, most of the controls should be on the numeric keys of your keyboard,I mostly tested using BRIX since the ROM has a lot of outlying 
behaviours, if you want to play that one the controls are { 4: Left, 6: Right } for the other games you'll have to mash some buttons to figure it out ¯\_(ツ)_/¯

---

CHIP-8 is an interpreted programming language, developed by Joseph Weisbecker on his 1802 microprocessor. It was initially used on the COSMAC VIP and Telmac 1800, which were 8-bit microcomputers made in the mid-1970s.

CHIP-8 was designed to be easy to program for and to use less memory than other programming languages like BASIC.

Interpreters have been made for many devices, such as home computers, microcomputers, graphing calculators, mobile phones, and video game consoles.

## Requirements

### Linux Build & Runtime
- .NET SDK 8+ to build
- SDL2 runtime library ( libSDL2.so if you're **not** bundling SDL2  )
- OpenGL + X11/Wayland libraries ( usually provided by Mesa packages )

Build a self-contained release ( output: bin/Release/net8.0/linux-x64/publish )
```sh
dotnet restore && dotnet publish -c Release -r linux-x64 --self-contained true
```

Distro Packages:
 - Ubuntu/Debian: `sudo apt install dotnet-sdk-8.0 libsdl2-dev libgl1`
 - Fedora: `sudo dnf dotnet-sdk-8.0 SDL2-devel mesa-libGL`
 - Arch: `sudo pacman -S dotnet-sdk sdl2 mesa`

### Windows Build & Runtime
- .NET SDK 8+ to build
- SDL2 runtime library ( SDL2.dll )
- OpenGL drivers ( provided by GPU vendor )

Build a self-contained release ( output: bin/Release/net8.0/win-x64/publish )
```sh
dotnet restore && dotnet publish -c Release -r win-x64 --self-contained true
```

Manual SDL2 setup:
 - Download SDL2 Development Libraries for Windows (x64) from https://www.libsdl.org/
 - Extract `SDL2.dll` from the `lib\x64` folder
 - Copy `SDL2.dll` next to the published executable ( `bin/Release/net8.0/win-x64/publish` )

## Notes

Opcodes 8XY6 and 8XYE use the Vy register instead of Vx. <br/>
Opcodes FX55 and FX65 will increment I, they shouldn't.  <br/>

Both behaviours are bugs in the original interpreters, they're fixed in Super-CHIP8 and most modern emulators will also include the fixes.<br/>
I've kept it here for sake of compatibility with older ROMs.

## Refrences
- [Wikipedia](https://en.wikipedia.org/wiki/CHIP-8)
- [Cowgod's Technical Reference v1.0](http://devernay.free.fr/hacks/chip8/C8TECH10.HTM#3.0) 
- [Mastering CHIP-8](https://github.com/mattmikolay/chip-8/wiki/CHIP%E2%80%908-Technical-Reference#instruction-set)
- ROMS courtesy of [Zophar](https://www.zophar.net/pdroms/chip8/chip-8-games-pack.html)
