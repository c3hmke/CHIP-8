# CHIP-8 Emulator

This project was used to cut my teeth on implementing hardware emulators and run into the pitfalls of linking that to a UI before proceeding a something more substantial.
It's rough around the edges and was built and tested in Linux. 

There is currently no input remapping, most of the controls should be on the numeric keys of your keyboard,I mostly tested using BRIX since the ROM has a lot of outlying 
behaviours, if you want to play that one the controls are { 4: Left, 6: Right } for the other games you'll have to mash some buttons to figure it out for now.

I don't plan on updating this, instead I'm working on [Ostrich](https://github.com/c3hmke/Ostrich), a GameBoy emulator. I may include the CHIP-8 core there at some point.

CHIP-8 is an interpreted programming language, developed by Joseph Weisbecker on his 1802 microprocessor. It was initially used on the COSMAC VIP and Telmac 1800, which were 8-bit microcomputers made in the mid-1970s.

CHIP-8 was designed to be easy to program for and to use less memory than other programming languages like BASIC.

Interpreters have been made for many devices, such as home computers, microcomputers, graphing calculators, mobile phones, and video game consoles.

---
Some references:
- [Wikipedia](https://en.wikipedia.org/wiki/CHIP-8)
- [Cowgod's Technical Reference v1.0](http://devernay.free.fr/hacks/chip8/C8TECH10.HTM#3.0) 
- [Mastering CHIP-8](https://github.com/mattmikolay/chip-8/wiki/CHIP%E2%80%908-Technical-Reference#instruction-set)

---

## Notes

Opcodes 8XY6 and 8XYE use the Vy register instead of Vx. <br/>
Opcodes FX55 and FX65 will increment I, they shouldn't.  <br/>

Both behaviours are bugs in the original interpreters, they're fixed in Super-CHIP8 and most modern emulators will also include the fixes.<br/>
I've kept it here for sake of compatibility with older ROMs.

---

ROMS courtesy of [Zophar](https://www.zophar.net/pdroms/chip8/chip-8-games-pack.html)
