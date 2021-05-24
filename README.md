# MID2TGVA
Convert MIDI files to a SimplePlanes aircraft with parts from the Tone Generator mod.

Each note of each channel in each track is assigned to a block. Despite this, a generated aircraft with a few hundred parts runs smoothly. The placement of each block is calculated from its track, channel, and note number. Unused notes are skipped.

The script may also be useful for studying MIDI files. Almost all op-codes are displayed when the MIDI file is being read.

Many settings, including speed, pitch, and an equalizer, are available by modifying the variables in the "User Settings" section of the program file.

The output XML file is created in the desktop folder. At the time of writing, the aircraft is compatible with all Tone Generator versions (latest: V1.3).

[ Example ] Dichromatic Lotus Butterfly ~ Ancients (composed by ZUN)

- [Aircraft](https://www.simpleplanes.com/a/yd62Vc/ssg_18)
- [Source: ZUN's old works](http://www16.big.or.jp/~zun/html/music_old.html)

[ Example ] Tomboyish Girl in Love (composed by ZUN)

- [Aircraft](https://www.simpleplanes.com/a/y2cEvK/th06_05)
- Source: Embodiment of Scarlet Devil

### Features

- Heavily optimized aircraft generation
- Supports MIDI Volume and Expression
- Many settings to adjust MIDI file export
- MIDI information debugging display

### Tips

- Try using a MIDI file sequencer or digital audio workstation to perform simple modifications to MIDI files before exporting.

### Notes

- Make sure that the desktop does not have any files with the same name as your MIDI file, or information may be lost.
- Tested with SimplePlanes V1.10.106.0. Generated aircraft may not be loadable in older versions of the game.
- Tested with Tone Generator V1.0 to V1.1b.
- You may need Visual Studio to use the software or change the settings.
- Loud sounds play when loading an aircraft with Tone Generator parts. Always turn down the volume to a safe level before use.

### Copyright

- Use of the software is subject to the GNU General Public License 3.
- Copyright holders of the MIDI files used to generate aircraft files will retain their rights.

### SP Website Terms

- Users of this software (except the developers) may not perform the following actions:
  - Upload unchanged export contents as a public aircraft.
- Users of this software may however upload derivative works of the exported aircraft file. These include, but are not limited to:
  - Aircraft and other machines
  - Artwork / Graphics
  - Funky Trees / XML Modding
  - Videos and similar media
  - Other content where the generated music is not the most significant feature of the work.
- The terms listed in this section may be changed without notice.

- - -

Last Updated: 2021 May 24
