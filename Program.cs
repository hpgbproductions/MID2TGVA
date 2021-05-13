/*
    MIDI to Tone Generator Converter (MID2TGVA)
    Copyright (C) 2021 hpgbproductions

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

    hpgbproductions (SimplePlanes)
    https://www.simpleplanes.com/u/hpgbproductions

    hpgbproductions (Twitter)
    https://twitter.com/hpgbproductions
*/

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace MID2TGVA
{
    class Program
    {
        static void Main(string[] args)
        {
            // BEGIN user settings

            int DefaultTempoEvents = 16;
            int DefaultChVolEvents = 16;
            int DefaultChExpEvents = 16;
            int DefaultEventsPerNote = 16;
            
            double FrequencyMultiplier = 1;
            double AmplitudeMultiplier = 1;

            double NoteOnDelay = 0.01;
            double NoteOffDelay = -0.01;
            double SpeedMultiplier = 1;
            double VelocityNonlinearlty = 0.5;
            double VolumeNonlinearity = 2;
            double ExpressionNonlinearity = 1;

            // Used for Reset All Controllers
            byte DefaultVolume = 64;
            byte DefaultExpression = 64;

            bool IgnoreVolume = false;
            bool IgnoreExpression = false;

            // Tone Generator settings
            string TG_ActivationGroup = "0";
            string TG_AudioType = "Sine";
            string TG_BypassEffects = "false";
            string TG_BypassLEffects = "false";
            string TG_BypassReverb = "false";
            string TG_StereoPan = "0";
            string TG_SpatialBlend = "0";
            string TG_ReverbMix = "1";
            string TG_Doppler = "0";
            string TG_Spread = "0";
            string TG_DistMin = "500";
            string TG_DistMax = "500";

            // EQ profile (array size is changeable, at least 2)
            // Default size of 12 is roughly one per octave
            double[] EqProfile = new double[]
            {
                1, 1, 1, 1,
                1, 1, 1, 0.5,
                0.1, 0.3, 0.5, 0.5,
            };

            // END user settings

            // ----------------------------------------------------------------

            string fp;
            BinaryReader reader;
            Stream fs;

            string outfp;
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            Stream outfs;
            StreamWriter outfsw;

            uint format = 0;
            uint ntracks = 0;    // Number of MTrk chunks
            uint tickdiv;        // The source two-byte value that provides timing resolution information

            TimingModes TimingMode = TimingModes.Metrical;
            uint ppqn = 0;             // Pulses (ticks) per quarter note
            uint fps = 0;
            uint subfps = 0;

            byte PreviousAction = 0xFF;    // For Running Status use
            bool MultiPacketMsg = false;

            uint CurrentTick = 0;
            uint CurrentTrack = 0;

            double GlobalOffset = 0;

            int CurrentPartId = 1;

            // Amplitude/Time graphs for track, channel, note
            NoteData[,,] Notes = new NoteData[0, 0, 0];
            ChannelVolumeData[,] ChVol = new ChannelVolumeData[0, 0];
            ChannelExpressionData[,] ChExp = new ChannelExpressionData[0, 0];

            TempoData Tempos = new TempoData(DefaultTempoEvents);

            // ----------------------------------------------------------------
            // BEGIN Runtime Area

            Console.WriteLine("MIDI to Tone Generator Converter");
            Console.WriteLine("Copyright (C) 2021 hpgbproductions");
            Console.WriteLine();

            if (args.Length == 0)
            {
                Console.WriteLine("Enter MIDI file path>");
                fp = Console.ReadLine();
            }
            else
            {
                fp = args[0];
            }

            // Open the selected MIDI file for reading
            Console.WriteLine("Reading file...");
            Console.WriteLine();
            fs = File.OpenRead(fp);
            reader = new BinaryReader(fs);

            // Read the header chunk
            ReadChunk();

            // Set up note storage
            Notes = new NoteData[ntracks, 16, 128];
            ChVol = new ChannelVolumeData[ntracks, 16];
            ChExp = new ChannelExpressionData[ntracks, 16];
            for (uint t = 0; t < ntracks; t++)
            {
                for (uint c = 0; c < 16; c++)
                {
                    ChVol[t, c] = new ChannelVolumeData(DefaultChVolEvents);
                    ChExp[t, c] = new ChannelExpressionData(DefaultChExpEvents);
                    for (uint n = 0; n < 128; n++)
                    {
                        Notes[t, c, n] = new NoteData(DefaultEventsPerNote, EqAmplitude(n, EqProfile) * AmplitudeMultiplier, NoteFrequency(n) * FrequencyMultiplier);
                    }
                }
            }

            // Read the track chunk(s)
            switch (format)
            {
                case 0:
                    ReadChunk();
                    break;

                case 1:
                    bool more = true;
                    while (more)
                    {
                        more = ReadChunk();
                    }
                    break;

                case 2:
                    long pos_store = fs.Position;
                    bool more2 = true;
                    while (more2)
                    {
                        more2 = ReadChunk(true);
                    }

                    Console.WriteLine(string.Format("{0} track chunks detected. Enter a number from 0 to {1}, inclusive>", ntracks, ntracks - 1));
                    int TargetTrack = int.Parse(Console.ReadLine());
                    if (TargetTrack >= CurrentTrack)
                    {
                        Console.WriteLine("A track of index " + TargetTrack + " does not exist.");
                        break;
                    }

                    CurrentTrack = 0;
                    fs.Position = pos_store;

                    while (CurrentTrack < TargetTrack)
                    {
                        ReadChunk(true, true);
                    }
                    ReadChunk();
                    break;

                default:
                    Console.WriteLine("This MIDI file format is not supported");
                    return;
            }

            // Auto-calculate
            for (uint t = 0; t < ntracks; t++)
            {
                for (uint c = 0; c < 16; c++)
                {
                    ChVol[t, c].Calculate(Tempos, GlobalOffset, SpeedMultiplier, VolumeNonlinearity);
                    ChExp[t, c].Calculate(Tempos, GlobalOffset, SpeedMultiplier, ExpressionNonlinearity);
                    for (uint n = 0; n < 128; n++)
                        Notes[t, c, n].Calculate(Tempos, GlobalOffset, NoteOnDelay, NoteOffDelay, SpeedMultiplier, VelocityNonlinearlty);
                }
            }
            
            WriteAircraftFile();

            Console.WriteLine();
            Console.WriteLine("Process complete. Press Enter to exit>");
            Console.ReadLine();
            reader.Dispose();
            outfsw.Dispose();
            return;

            // END Runtime Area
            // ----------------------------------------------------------------

            double Lerp(double a, double b, double t)
            {
                return a + (b - a) * Math.Clamp(t, 0, 1);
            }

            double InverseLerp(double a, double b, double y)
            {
                return (y - a) / (b - a);
            }

            bool ByteArraysEqual(byte[] a, byte[] b)
            {
                if (a.Length != b.Length)
                {
                    return false;
                }

                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i] != b[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            uint ReadBigEndianUInt32()
            {
                byte[] data = reader.ReadBytes(4);
                Array.Reverse(data);
                return BitConverter.ToUInt32(data);
            }

            uint ReadBigEndianUInt24()
            {
                byte[] dnum = reader.ReadBytes(3);
                byte[] data = new byte[4] { dnum[2], dnum[1], dnum[0], (byte)0x00u };
                return BitConverter.ToUInt32(data);
            }

            ushort ReadBigEndianUInt16()
            {
                var data = reader.ReadBytes(2);
                Array.Reverse(data);
                return BitConverter.ToUInt16(data);
            }

            // Read variable length values used for delta times and event length
            uint ReadVariableLengthUInt()
            {
                uint number = 0;

                int bytecount = 0;
                bool readnext = true;

                while (readnext && bytecount < 4)
                {
                    byte b = reader.ReadByte();
                    bytecount++;
                    number = (number << 7) + (b & 0x7fu);
                    if (b >> 7 == 0)
                    {
                        readnext = false;
                    }
                }
                
                return number;
            }

            /*
            uint TestVariableLengthUInt(byte w, byte x, byte y, byte z)
            {
                uint number = 0;

                int bytecount = 0;
                bool readnext = true;

                byte[] bytes = new byte[4] { w, x, y, z };

                while (readnext && bytecount < 4)
                {
                    byte b = bytes[bytecount];
                    bytecount++;
                    number = (number << 7) + (b & 0x7fu);
                    if (b >> 7 == 0)
                    {
                        readnext = false;
                    }
                }

                return number;
            }
            */

            // Returns the frequency associated with a given MIDI note number
            double NoteFrequency(uint note)
            {
                if (note > 127)
                {
                    return 0f;
                }

                double[] MiddleNotes = new double[] {
                261.625580, 277.182617, 293.664764, 311.126984,
                329.627583, 349.228241, 369.994415, 391.995422,
                415.304688, 440.000000, 466.163757, 493.883301 };

                int RelativeOctave = Convert.ToInt32(note) / 12 - 5;
                int NoteIndex = Convert.ToInt32(note) % 12;

                return MiddleNotes[NoteIndex] * MathF.Pow(2, RelativeOctave);
            }

            // Returns a value along a given graph
            double EqAmplitude(uint note, double[] graph)
            {
                if (graph.Length < 2)
                {
                    Console.WriteLine("Invalid graph array!");
                    return 0;
                }
                else if (note > 127)
                {
                    Console.WriteLine("Note value is out of the range 0..127");
                    return 0;
                }

                double NotesPerSegment = 127 / (graph.Length - 2);
                int SegmentStartIndex = (int)Math.Floor(note / NotesPerSegment);
                int SegmentEndIndex = SegmentStartIndex + 1;
                double SegmentStartNote = SegmentStartIndex * NotesPerSegment;
                double SegmentEndNote = SegmentEndIndex * NotesPerSegment;

                double amp = Lerp(graph[SegmentStartIndex], graph[SegmentEndIndex], InverseLerp(SegmentStartNote, SegmentEndNote, note));

                return amp;
            }

            // Reads events and identifies them
            // Returns false if it hits End of Track, otherwise returns true
            bool ReadEvent()
            {
                byte bmain = reader.ReadByte();

                if (bmain >= 0x80u)
                {
                    PreviousAction = bmain;
                }
                else    // Running Status
                {
                    bmain = PreviousAction;
                    fs.Position--;
                }

                if (bmain >= 0x80u && bmain <= 0xEFu)    // MIDI Event
                {
                    byte bmainact = (byte)(bmain >> 4);
                    byte bmainch = (byte)(bmain & 0x0Fu);

                    if (bmainact == 0x8u || bmainact == 0x9u || bmainact == 0xAu)    // Key Actions
                    {
                        byte bkey = reader.ReadByte();
                        byte bprs = reader.ReadByte();

                        if (bmainact == 0x8u)
                        {
                            Notes[CurrentTrack, bmainch, bkey].AddEntry(CurrentTick, 0);
                            Console.WriteLine(string.Format("    {0}> Channel {1:X}: Turned OFF note 0x{2:X2} with pressure 0x{3:X2}", CurrentTick, bmainch, bkey, bprs));
                        }
                        else if (bmainact == 0x9u)
                        {
                            Notes[CurrentTrack, bmainch, bkey].AddEntry(CurrentTick, bprs);
                            Console.WriteLine(string.Format("    {0}> Channel {1:X}: Turned {4} note 0x{2:X2} with pressure 0x{3:X2}", CurrentTick, bmainch, bkey, bprs, bprs == 0 ? "OFF" : "ON"));
                        }
                        else if (bmainact == 0xAu)
                        {
                            Console.WriteLine(string.Format("    {0}> Channel {1:X}: Applied aftertouch on note 0x{2:X2} with pressure 0x{3:X2}", CurrentTick, bmainch, bkey, bprs));
                        }
                    }
                    else if (bmainact == 0xBu)
                    {
                        byte bcon = reader.ReadByte();
                        byte bval = reader.ReadByte();

                        if (bcon == 7u)    // Volume MSB
                        {
                            ChVol[CurrentTrack, bmainch].AddEntry(CurrentTick, bval);
                        }
                        else if (bcon == 11u)    // Expression MSB
                        {
                            ChExp[CurrentTrack, bmainch].AddEntry(CurrentTick, bval);
                        }
                        else if (bcon == 121u)    // Reset Controller Values
                        {
                            ChVol[CurrentTrack, bmainch].AddEntry(CurrentTick, DefaultVolume);
                            ChExp[CurrentTrack, bmainch].AddEntry(CurrentTick, DefaultExpression);
                        }
                        else if (bcon == 120u || bcon >= 123u)    // All Sound/Notes Off
                        {
                            for (int i = 0; i < 128; i++)
                            {
                                Notes[CurrentTrack, bmainch, i].AddEntry(CurrentTick, 0);
                            }
                        }

                        Console.WriteLine(string.Format("    {0}> Channel {1:X}: Set controller 0x{2:X2} to value 0x{3:X2}", CurrentTick, bmainch, bcon, bval));
                    }
                    else if (bmainact == 0xCu)
                    {
                        byte bprg = reader.ReadByte();
                        Console.WriteLine(string.Format("    {0}> Channel {1:X}: Changed to program {2:X2}", CurrentTick, bmainch, bprg));
                    }
                    else if (bmainact == 0xDu)
                    {
                        byte bprs = reader.ReadByte();
                        Console.WriteLine(string.Format("    {0}> Channel {1:X}: Applied aftertouch pressure {2:X2}", CurrentTick, bmainch, bprs));
                    }
                    else if (bmainact == 0xEu)
                    {
                        byte bmsb = reader.ReadByte();
                        byte blsb = reader.ReadByte();
                        Console.WriteLine(string.Format("    {0}> Channel {1:X}: Applied pitch bend {2:X2} {3:X2}", CurrentTick, bmainch, bmsb, blsb));
                    }

                    return true;
                }
                else if (bmain == 0xFFu)    // Meta Event
                {
                    byte bsec = reader.ReadByte();
                    uint blen = ReadVariableLengthUInt();

                    if (bsec == 0x00u)    // Sequence Number
                    {
                        ushort bsqn = ReadBigEndianUInt16();
                        Console.WriteLine(string.Format("    {0}> Sequence Number {1}", CurrentTick, bsqn));
                    }
                    else if (bsec <= 0x09u)    // Text Events
                    {
                        byte texttype = bsec;
                        
                        string text = Encoding.UTF8.GetString(reader.ReadBytes((int)blen));

                        string[] TextTypes = new string[] { "Sequence Name", "Text", "Copyright", "Track Name", "Instrument Name", "Lyric", "Marker", "Cue Point", "Program Name", "Device Name" };

                        if (texttype == 0x03u)    // Sequence | Track Name
                        {
                            if (CurrentTrack == 1 && format <= 1)    // Sequence Name
                            {
                                Console.WriteLine("    {0}> {1}: {2}", CurrentTick, TextTypes[0], text);
                            }
                            else    // Track Name
                            {
                                Console.WriteLine("    {0}> {1}: {2}", CurrentTick, TextTypes[3], text);
                            }
                        }
                        else
                        {
                            Console.WriteLine("    {0}> {1}: {2}", CurrentTick, TextTypes[texttype], text);
                        }
                    }
                    else if (bsec == 0x20u)
                    {
                        byte c = reader.ReadByte();
                        Console.WriteLine("    {0}> Selected Channel: {1:X}", CurrentTick, c);
                    }
                    else if (bsec == 0x21u)
                    {
                        byte p = reader.ReadByte();
                        Console.WriteLine("    {0}> Selected Port: {1:X}", CurrentTick, p);
                    }
                    else if (bsec == 0x2Fu)    // End of Track
                    {
                        Console.WriteLine("    {0}> End of Track", CurrentTick);
                        return false;
                    }
                    else if (bsec == 0x51u)    // Tempo
                    {
                        uint mpqn = ReadBigEndianUInt24();
                        uint bpm = 60000000u / mpqn;
                        Console.WriteLine(string.Format("    {0}> Tempo: {1} BPM ({2} us/beat)", CurrentTick, bpm, mpqn));
                        if (TimingMode == TimingModes.Metrical)
                        {
                            Tempos.AddEntry(CurrentTick, mpqn, ppqn);
                        }
                    }
                    else if (bsec == 0x54u)    // SMPTE Offset Time
                    {
                        byte hr = reader.ReadByte();
                        byte r = (byte)(hr >> 5);
                        byte h = (byte)(hr & 0x1Fu);

                        byte m = reader.ReadByte();
                        byte s = reader.ReadByte();
                        byte fr = reader.ReadByte();
                        byte ff = reader.ReadByte();

                        char sep = ':';

                        double fps = 0;
                        switch (r)
                        {
                            case 0b00:
                                fps = 24;
                                break;
                            case 0b01:
                                fps = 25;
                                break;
                            case 0b10:
                                fps = 29.97;
                                sep = ';';
                                break;
                            case 0b11:
                                fps = 30;
                                break;
                            default:
                                fps = 0;
                                break;
                        }

                        GlobalOffset = h * 3600 + m * 60 + s + (fr + 0.01 * ff) / fps;
                        Console.WriteLine(string.Format("    {0}> SMPTE Offset: {1:D2}{6}{2:D2}{6}{3:D2}{6}{4:D2}.{5:D2} ({7} seconds)", CurrentTick, h, m, s, fr, ff, sep, GlobalOffset));
                    }
                    else if (bsec == 0x58u)    // Time Signature
                    {
                        byte n = reader.ReadByte();
                        byte dd = reader.ReadByte();
                        byte d = 0x02;
                        byte c = reader.ReadByte();
                        byte b = reader.ReadByte();

                        for (int i = 1; i < dd; i++)
                        {
                            d *= d;
                        }

                        Console.WriteLine(string.Format("    {0}> Time Signature: {1}/{2}; metronome interval {3}; {4}x 32nd notes per quarter note", CurrentTick, n, d, c, b));
                    }
                    else if (bsec == 0x59u)
                    {
                        /*
                        byte sf = reader.ReadByte();
                        byte s = (byte)(sf >> 4);
                        byte f = (byte)(sf & 0x0Fu);
                        */

                        sbyte sf = (sbyte)reader.ReadByte();
                        byte mi = reader.ReadByte();

                        string[,] key = new string[2, 15] { { "C", "G", "D", "A", "E", "B", "F-sharp", "C-sharp", "C-flat", "G-flat", "D-flat", "A-flat", "E-flat", "B-flat", "F" }, { "A", "E", "B", "F-sharp", "C-sharp", "G-sharp", "D-sharp", "A-sharp", "A-flat", "E-flat", "B-flat", "F", "C", "G", "D" } };
                        string[] mst = new string[] { "major", "minor" };

                        Console.WriteLine(string.Format("sf={0} mi={1}", sf, mi));
                        Console.WriteLine(string.Format("    {0}> Key Signature: {1} {2}", CurrentTick, key[mi, sf >= 0 ? sf : sf + 15], mst[mi]));
                        Console.WriteLine("");
                    }
                    else if (bsec == 0x7Fu)
                    {
                        byte[] bmsg = reader.ReadBytes((int)blen);
                        string bmss = BitConverter.ToString(bmsg);
                        Console.WriteLine("    {0}> Sequencer Specific Event: {1}", CurrentTick, bmss);
                    }

                    return true;
                }
                else if (bmain == 0xF0u || bmain == 0xF7u)    // SysEx Event | Escape Sequence
                {
                    byte blen = reader.ReadByte();
                    byte[] bmsg = reader.ReadBytes(blen);
                    string bmss = BitConverter.ToString(bmsg);

                    if (bmain == 0xF0u)    // SysEx Event
                    {
                        if (bmsg[bmsg.Length - 1] == 0xF7u)    // SysEx Termination Byte
                        {
                            MultiPacketMsg = false;
                        }
                        else
                        {
                            MultiPacketMsg = true;
                        }

                        Console.WriteLine("    {0}> System Exclusive Event: {1}", CurrentTick, bmss);
                    }
                    else    // Escape Sequence
                    {
                        if (MultiPacketMsg)
                        {
                            if (bmsg[bmsg.Length - 1] == 0xF7u)    // SysEx Termination Byte
                            {
                                MultiPacketMsg = false;
                            }

                            Console.WriteLine("    {0}> System Exclusive Event: {1}", CurrentTick, bmss);
                        }
                        else
                        {
                            Console.WriteLine("    {0}> Escape Sequence: {1}", CurrentTick, bmss);
                        }
                    }

                    return true;
                }

                // This should not occur
                Console.WriteLine(string.Format("Error: Unexpected first byte 0x{0:X2}", bmain));
                return false;
            }

            // Reads the next chunk
            // Returns true on a successful read, otherwise returns false
            bool ReadChunk(bool tryskip = false, bool hide = false)
            {
                // Check if EOF
                if (reader.PeekChar() == -1)
                {
                    Console.WriteLine("No more chunks are available.");
                    return false;
                }

                byte[] ChunkId = new byte[4];    // Type of chunk
                uint ChunkLen = 0;               // Length of chunk
                uint Remaining = 0;              // Bytes remaining in the chunk (only used for header)

                ChunkId = reader.ReadBytes(4);
                ChunkLen = ReadBigEndianUInt32();
                Remaining = ChunkLen;

                if (ByteArraysEqual(ChunkId, new byte[] { 0x4D, 0x54, 0x68, 0x64 }))    // MThd
                {
                    Console.WriteLine(string.Format("Detected header chunk with length 0x{0:X8} ({1} bytes)", ChunkLen, ChunkLen));

                    format = ReadBigEndianUInt16();
                    Console.WriteLine("    Format: " + format);

                    ntracks = ReadBigEndianUInt16();
                    Console.WriteLine("    Tracks: " + ntracks);

                    tickdiv = ReadBigEndianUInt16();
                    if (tickdiv < 0x8000)    // Metrical
                    {
                        TimingMode = TimingModes.Metrical;
                        ppqn = tickdiv;
                        Tempos.AddEntry(0u, 500000u, ppqn);
                        Console.WriteLine(string.Format("    Timing Mode: Metrical ({0} PPQN)", ppqn));
                    }
                    else    // Timecode
                    {
                        TimingMode = TimingModes.Timecode;
                        fps = (~(tickdiv >> 8) + 1) & 0x7f;
                        subfps = tickdiv & 0x7f;
                        Tempos.AddEntry(0u, 1000000u / (fps * subfps), 1);
                        Console.WriteLine(string.Format("    Timing Mode: Timecode ({0} FPS, {1} sub-frames)", fps, subfps));
                    }

                    Remaining -= 6;

                    // Advance to the end of the chunk, if not already there
                    if (Remaining > 0)
                    {
                        fs.Position += Remaining;
                    }

                    return true;
                }
                else if (ByteArraysEqual(ChunkId, new byte[] { 0x4D, 0x54, 0x72, 0x6B }))    // MTrk
                {
                    CurrentTick = 0;
                    MultiPacketMsg = false;

                    if (tryskip)
                    {
                        fs.Position += ChunkLen;

                        if (!hide)
                            Console.WriteLine(string.Format("Detected track chunk {0} with length 0x{1:X8} ({1} bytes)", CurrentTrack, ChunkLen));
                        
                        CurrentTrack++;
                        return true;
                    }

                    Console.WriteLine(string.Format("Detected track chunk {0} with length 0x{1:X8} ({1} bytes)", CurrentTrack, ChunkLen));

                    bool cont = true;
                    while (cont)
                    {
                        CurrentTick += ReadVariableLengthUInt();
                        cont = ReadEvent();
                    }

                    CurrentTrack++;
                    return true;
                }
                else
                {
                    Console.WriteLine("Error: This chunk type is not supported!");
                    return false;
                }
            }

            void WriteAircraftFile()
            {
                // Used to write connections
                List<int> BasePartIds = new List<int>(1);
                List<List<int>> TonePartIds = new List<List<int>>(1);    // Track, Parts

                outfp = Path.Combine(desktop, Path.GetFileNameWithoutExtension(fp) + ".xml");
                outfs = File.Open(outfp, FileMode.Create);
                outfsw = new StreamWriter(outfs, Encoding.UTF8);

                Console.WriteLine("Generating Aircraft: " + Path.GetFileNameWithoutExtension(fp));
                outfsw.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                outfsw.WriteLine(string.Format("<Aircraft name=\"{0}\" url=\"\" theme=\"Default\" size=\"0,0,0\" boundsMin=\"0,0,0\" xmlVersion=\"6\" legacyJointIdentification=\"false\">", Path.GetFileNameWithoutExtension(fp)));
                outfsw.WriteLine("  <Assembly>");
                outfsw.WriteLine("    <Parts>");
                outfsw.WriteLine("      <Part id=\"0\" partType=\"Cockpit-1\" position=\"0,0,0\" rotation=\"0,0,0\" drag=\"0,0,0,0,0,0\" materials=\"7,0\" scale=\"1,1,1\" partCollisionResponse=\"Default\">");
                outfsw.WriteLine("        <Cockpit.State primaryCockpit=\"True\" lookBackTranslation=\"0,0\" />");
                outfsw.WriteLine("      </Part>");
                Console.WriteLine("    0> Cockpit");

                // Write track module
                for (int t = 0; t < ntracks; t++)
                {
                    // Write base fuselage block
                    BasePartIds.Add(CurrentPartId);
                    double BasePartZ = BasePartIds.Count * -8;
                    outfsw.WriteLine(string.Format("      <Part id=\"{0}\" partType=\"Fuselage-Body-1\" position=\"0,0,{1}\" rotation=\"0,0,0\" drag=\"0,0,0,0,0,0\" materials=\"0\" scale=\"1,1,1\" partCollisionResponse=\"Default\">", CurrentPartId, BasePartZ));
                    outfsw.WriteLine("        <FuelTank.State fuel = \"0\" capacity = \"0\" />");
                    outfsw.WriteLine("        <Fuselage.State version=\"2\" frontScale=\"16, 1\" rearScale=\"16, 1\" offset=\"0, 0, 16\" deadWeight=\"0\" buoyancy=\"0\" fuelPercentage=\"0\" smoothFront=\"False\" smoothBack=\"False\" autoSizeOnConnected=\"false\" cornerTypes=\"0,0,0,0,0,0,0,0\" />");
                    outfsw.WriteLine("      </Part>");
                    Console.WriteLine(string.Format("    {0}> Track {1}: Base Fuselage Block", CurrentPartId, t));
                    CurrentPartId++;

                    TonePartIds.Add(new List<int>());
                    // Write Tone Generators
                    for (int c = 0; c < 16; c++)
                    {
                        // Vertical offsets +0.5 ~ +8 (channels)
                        double PosY = 0.5 + c * 0.5;
                        for (int n = 0; n < 128; n++)
                        {
                            string input = GenerateInput(Notes[t, c, n], ChVol[t, c], ChExp[t, c]);
                            if (input != "0")
                            {
                                TonePartIds[t].Add(CurrentPartId);

                                // Horizontal offsets -3.75 ~ +3.75 (notes)
                                double PosX = -3.75 + n % 16 * 0.5;
                                double PosZ = -3.75 + n / 16 * 0.5 + BasePartZ;

                                outfsw.WriteLine(string.Format("      <Part id=\"{0}\" partType=\"Mod_ToneGenerator_tonegen_a\" position=\"{1},{2},{3}\" rotation=\"0,0,0\" drag=\"0,0,0,0,0,0\" materials=\"0,0,0,0\" scale=\"1,1,1\" partCollisionResponse=\"Default\">", CurrentPartId, PosX, PosY, PosZ));
                                outfsw.WriteLine(string.Format("        <InputController.State activationGroup=\"{0}\" invert=\"false\" min=\"0\" max=\"{1}\" input=\"{2}\" />", TG_ActivationGroup, Notes[t, c, n].amulti, input));
                                outfsw.WriteLine(string.Format("        <ToneGenerator_ToneGenerator2.State _audiotype=\"{0}\" _bypass_effects=\"{1}\" _bypass_leffects=\"{2}\" _bypass_reverb=\"{3}\" _frequency=\"{4}\" _stereo_pan=\"{5}\" _spatialblend=\"{6}\" _reverb_mix=\"{7}\" _doppler=\"{8}\" _spread=\"{9}\" _dist_min=\"{10}\" _dist_max=\"{11}\" />",
                                    TG_AudioType, TG_BypassEffects, TG_BypassLEffects, TG_BypassReverb, Notes[t, c, n].frequency, TG_StereoPan, TG_SpatialBlend, TG_ReverbMix, TG_Doppler, TG_Spread, TG_DistMin, TG_DistMax));
                                outfsw.WriteLine("      </Part>");
                                Console.WriteLine(string.Format("    {0}> Track {1}: Channel {2} Note {3}", CurrentPartId, t, c, n));

                                CurrentPartId++;
                            }
                        }
                    }
                }

                outfsw.WriteLine("    </Parts>");
                outfsw.WriteLine("    <Connections>");
                Console.WriteLine("Writing connections...");

                // Write fuselage-cockpit connections
                for (int i = 0; i < BasePartIds.Count; i++)
                {
                    int partA = BasePartIds[i];
                    int partB = 0;
                    if (i > 0)
                    {
                        partB = BasePartIds[i - 1];
                    }
                    outfsw.WriteLine(string.Format("      <Connection partA=\"{0}\" partB=\"{1}\" attachPointsA=\"0\" attachPointsB=\"0\" />", partA, partB));
                }

                // Write TG-fuselage connections
                for (int t = 0; t < TonePartIds.Count; t++)
                {
                    for (int c = 0; c < TonePartIds[t].Count; c++)
                    {
                        outfsw.WriteLine(string.Format("      <Connection partA=\"{0}\" partB=\"{1}\" attachPointsA=\"2\" attachPointsB=\"3\" />", TonePartIds[t][c], BasePartIds[t]));
                    }
                }

                outfsw.WriteLine("    </Connections>");
                outfsw.WriteLine("    <Bodies>");
                Console.WriteLine("Writing bodies...");

                // Write fuselage-cockpit body
                string BasePartIdString = "0";
                foreach (int id in BasePartIds)
                {
                    BasePartIdString += "," + id;
                }
                outfsw.WriteLine(string.Format("      <Body partIds=\"{0}\" position=\"0,0,0\" rotation=\"0,0,0\" velocity=\"0,0,0\" angularVelocity=\"0,0,0\" />", BasePartIdString));

                // Write body for each Tone Generator
                for (int t = 0; t < TonePartIds.Count; t++)
                {
                    foreach (int p in TonePartIds[t])
                    {
                        outfsw.WriteLine(string.Format("      <Body partIds=\"{0}\" position=\"0,0,0\" rotation=\"0,0,0\" velocity=\"0,0,0\" angularVelocity=\"0,0,0\" />", p));
                    }
                }

                outfsw.WriteLine("    </Bodies>");
                outfsw.WriteLine("  </Assembly>");
                outfsw.WriteLine("  <Theme name=\"Custom\">");
                for (int i = 0; i < 19; i++)
                {
                    outfsw.WriteLine("    <Material color=\"FFFFFF\" r=\"0\" m=\"0.65\" s=\"0.08\" />");
                }
                outfsw.WriteLine("  </Theme>");
                outfsw.WriteLine("</Aircraft>");

                Console.WriteLine("Finished aircraft export");
                outfsw.Close();
            }

            string GenerateInput(NoteData note, ChannelVolumeData vol, ChannelExpressionData exp)
            {
                string inputstr = string.Empty;

                // No note data
                if (!note.ContainsData())
                {
                    // The part generator should skip this part
                    return "0";
                }

                // Time < T0 ? 0 : A0
                // Time < T0 ? 0 : (Time < T1 ? A0)
                // Time < T0 ? 0 : (Time < T1 ? A0 : (Time < T2 ? A1 : A2))

                string nstr = string.Format("(Time&lt;{0:F3}?0:", note.time[0]);
                for (int ni = 0; ni < note.time.Count; ni++)
                {
                    // Last item
                    if (ni == note.time.Count - 1)
                    {
                        nstr += note.amplitude[ni] + new string(')', ni + 1);
                    }
                    else
                    {
                        nstr += string.Format("(Time&lt;{0:F3}?{1:F3}:", note.time[ni + 1], note.amplitude[ni]);
                    }
                }
                inputstr += nstr;

                if (vol.time.Count > 0 && !IgnoreVolume)
                {
                    string vstr = string.Format("(Time&lt;{0:F3}?0:", vol.time[0]);
                    for (int v = 0; v < vol.time.Count; v++)
                    {
                        // Last item
                        if (v == vol.time.Count - 1)
                        {
                            vstr += vol.amplitude[v] + new string(')', v + 1);
                        }
                        else
                        {
                            vstr += string.Format("(Time&lt;{0:F3}?{1:F3}:", vol.time[v + 1], vol.amplitude[v]);
                        }
                    }
                    inputstr += "*" + vstr;
                }

                if (exp.time.Count > 0 && !IgnoreExpression)
                {
                    string estr = string.Format("(Time&lt;{0:F3}?0:", exp.time[0]);
                    for (int e = 0; e < exp.time.Count; e++)
                    {
                        // Last item
                        if (e == exp.time.Count - 1)
                        {
                            estr += exp.amplitude[e] + new string(')', e + 1);
                        }
                        else
                        {
                            estr += string.Format("(Time&lt;{0:F3}?{1:F3}:", exp.time[e + 1], exp.amplitude[e]);
                        }
                    }
                    inputstr += "*" + estr;
                }

                return inputstr;
            }
        }

        enum TimingModes { Metrical, Timecode }

        struct NoteData
        {
            // Raw data from file
            public List<uint> tick;
            public List<byte> velocity;

            // Processed information
            public List<double> time;
            public List<double> amplitude;

            // Musical part parameters
            public double amulti;
            public double frequency;

            // Cache data that determines how entries are added
            private uint maxtick;

            public NoteData(int defaultsize, double a, double f)
            {
                tick = new List<uint>(defaultsize);
                velocity = new List<byte>(defaultsize);
                time = new List<double>(defaultsize);
                amplitude = new List<double>(defaultsize);
                amulti = a;
                frequency = f;
                maxtick = 0;
            }

            public bool AddEntry(uint ctick, byte vel)
            {
                int index = 0;
                if (ctick >= maxtick)    // Append
                {
                    if (tick.Count > 0)
                    {
                        if (vel == velocity[velocity.Count - 1])
                            return false;
                    }
                    tick.Add(ctick);
                    velocity.Add(vel);
                    maxtick = ctick;
                }
                else    // Insert
                {
                    foreach (uint t in tick)    // Locate a target index
                    {
                        if (ctick >= t)
                            index++;
                        else
                            break;
                    }
                    if (index > 0)
                    {
                        if (vel == velocity[index - 1])
                            return false;
                    }
                    tick.Insert(index, ctick);
                    tick.Insert(index, vel);
                }

                return true;
            }

            public void Calculate(TempoData tempo, double offset = 0, double ond = 0, double offd = 0, double speed = 1, double nonlin = 0.5)
            {
                time.Clear();
                amplitude.Clear();
                for (int i = 0; i < tick.Count; i++)
                {
                    time.Add((tempo.TimeAtTick(tick[i]) + (velocity[i] == 0 ? offd : ond)) / speed + offset);
                    amplitude.Add(Math.Pow(Convert.ToDouble(velocity[i]) / 127, nonlin));
                }
            }

            public bool ContainsData()
            {
                if (tick.Count == 0)
                    return false;

                // else
                foreach (byte v in velocity)
                {
                    if (v != 0)
                        return true;
                }
                return false;
            }

            public void Debug(string id, bool fast = false)
            {
                Console.WriteLine(string.Format("NoteData Array ({0}): {1}/{2} entries used", id, tick.Count, tick.Capacity));
                if (fast)
                    return;
                for (int i = 0; i < tick.Capacity; i++)
                {
                    if (i < tick.Count)
                        Console.WriteLine("    {0}: {1}> {2:X2}", i, tick[i], velocity[i]);
                    else
                        Console.WriteLine("    {0}: Unused", i);
                }
            }
        }

        struct ChannelVolumeData
        {
            public List<uint> tick;
            public List<byte> vol;

            public List<double> time;
            public List<double> amplitude;

            private uint maxtick;

            public ChannelVolumeData(int defaultsize)
            {
                tick = new List<uint>(defaultsize);
                vol = new List<byte>(defaultsize);
                time = new List<double>(defaultsize);
                amplitude = new List<double>(defaultsize);
                maxtick = 0;
            }

            public bool AddEntry(uint ctick, byte v)
            {
                int index = 0;
                if (ctick >= maxtick)    // Append
                {
                    if (tick.Count > 0)
                    {
                        if (v == vol[vol.Count - 1])
                            return false;
                    }
                    tick.Add(ctick);
                    vol.Add(v);
                    maxtick = ctick;
                }
                else    // Insert
                {
                    foreach (uint t in tick)    // Locate a target index
                    {
                        if (ctick >= t)
                            index++;
                        else
                            break;
                    }
                    if (index > 0)
                    {
                        if (v == vol[index - 1])
                            return false;
                    }
                    tick.Insert(index, ctick);
                    tick.Insert(index, v);
                }

                return true;
            }

            public void Calculate(TempoData tempo, double offset = 0, double speed = 1, double nonlin = 1)
            {
                time.Clear();
                amplitude.Clear();
                for (int i = 0; i < tick.Count; i++)
                {
                    time.Add(tempo.TimeAtTick(tick[i]) / speed + offset);
                    amplitude.Add(Math.Pow(Convert.ToDouble(vol[i]) / 127, nonlin));
                }
            }
        }

        struct ChannelExpressionData
        {
            public List<uint> tick;
            public List<byte> exp;

            public List<double> time;
            public List<double> amplitude;

            private uint maxtick;

            public ChannelExpressionData(int defaultsize)
            {
                tick = new List<uint>(defaultsize);
                exp = new List<byte>(defaultsize);
                time = new List<double>(defaultsize);
                amplitude = new List<double>(defaultsize);
                maxtick = 0;
            }

            public bool AddEntry(uint ctick, byte v)
            {
                int index = 0;
                if (ctick >= maxtick)    // Append
                {
                    if (tick.Count > 0)
                    {
                        if (v == exp[exp.Count - 1])
                            return false;
                    }
                    tick.Add(ctick);
                    exp.Add(v);
                    maxtick = ctick;
                }
                else    // Insert
                {
                    foreach (uint t in tick)    // Locate a target index
                    {
                        if (ctick >= t)
                            index++;
                        else
                            break;
                    }
                    if (index > 0)
                    {
                        if (v == exp[index - 1])
                            return false;
                    }
                    tick.Insert(index, ctick);
                    tick.Insert(index, v);
                }

                return true;
            }

            public void Calculate(TempoData tempo, double offset = 0, double speed = 1, double nonlin = 1)
            {
                time.Clear();
                amplitude.Clear();
                for (int i = 0; i < tick.Count; i++)
                {
                    time.Add(tempo.TimeAtTick(tick[i]) / speed + offset);
                    amplitude.Add(Math.Pow(Convert.ToDouble(exp[i]) / 127, nonlin));
                }
            }
        }

        struct TempoData
        {
            public List<uint> tick;
            public List<double> tlen;    // Store tempo at a point as the tick interval in seconds

            // Cache data that determines how entries are added
            private uint maxtick;

            public TempoData(int defaultsize)
            {
                tick = new List<uint>(defaultsize);
                tlen = new List<double>(defaultsize);
                maxtick = 0;
            }

            public bool AddEntry(uint ctick, uint usqn, uint ppqn)
            {
                double slen = usqn / Convert.ToDouble(1000000) / ppqn;

                if (ctick >= maxtick)    // Append
                {
                    tick.Add(ctick);
                    tlen.Add(slen);
                    maxtick = ctick;
                }
                else    // Insert
                {
                    int index = 0;
                    foreach (uint t in tick)    // Locate a target index
                    {
                        if (ctick >= t)
                            index++;
                        else
                            break;
                    }
                    tick.Insert(index, ctick);
                    tlen.Insert(index, slen);
                }
                
                return true;
            }

            public double TimeAtTick(uint ctick)
            {
                uint remain = ctick;
                double ctime = 0;

                for (int i = 1; i < tick.Count; i++)
                {
                    if (remain > tick[i] - tick[i - 1] && i + 1 < tick.Count)
                    {
                        remain -= tick[i] - tick[i - 1];
                        ctime += (tick[i] - tick[i - 1]) * tlen[i - 1];
                    }
                    else    // Final segment
                    {
                        ctime += tlen[i] * remain;
                        break;
                    }
                }

                return ctime;
            }

            public void Debug(bool fast = false)
            {
                Console.WriteLine(string.Format("TempoData Array: {0}/{1} entries used", tick.Count, tick.Capacity));
                if (fast)
                    return;
                for (int i = 0; i < tick.Capacity; i++)
                {
                    if (i < tick.Count)
                        Console.WriteLine("    {0}: {1} ({2:F6})> {3:F9} seconds/tick", i, tick[i], TimeAtTick(tick[i]),  tlen[i]);
                    else
                        Console.WriteLine("    {0}: Unused", i);
                }
            }
        }
    }
}
