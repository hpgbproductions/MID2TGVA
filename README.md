# MID2TGVA
Convert MIDI files to a SimplePlanes aircraft with parts from the Tone Generator mod.

Each note of each channel in each track is assigned to a block. Despite this, a generated aircraft with a few hundred parts runs smoothly. The placement of each block is calculated from its track, channel, and note number. Unused notes are skipped.

The script may also be useful for studying MIDI files. Try using [MidiEditor](http://www.midieditor.org/index.php?category=intro) to perform simple modifications to MIDI files before exporting.

Many settings, including speed, pitch, and an equalizer, are available by modifying the variables in the "User Settings" section of the program file.

![](https://jundroo.blob.core.windows.net/simpleplanes/GameData/aircraft/915/yd62Vc-UserView-0.png)

[ Example ] Dichromatic Lotus Butterfly ~ Ancients (composed by ZUN)
[Aircraft](https://www.simpleplanes.com/a/yd62Vc/ssg_18) [Source](http://www16.big.or.jp/~zun/html/music_old.html)

[ Example ] Tomboyish Girl in Love (composed by ZUN)
[Aircraft](https://www.simpleplanes.com/a/y2cEvK/th06_05)

#####NOTES

- Use of the software is subject to the GNU General Public License 3.
- Copyright holders of MIDI files used retain their rights.
- You may need Visual Studio to use the software. You will need Visual Studio to change the settings.
- Loud sounds play when loading an aircraft with Tone Generator parts. Always turn down the volume to a safe level before use.
