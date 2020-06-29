using CSCore;
using CSCore.SoundOut;
using NAudio.Wave;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using static AGI.Resource;
using static AGI.Resource.Sound;
using WaveFileReader = CSCore.Codecs.WAV.WaveFileReader;

namespace AGILE
{
    /// <summary>
    /// A class for playing AGI Sounds.
    /// </summary>
    class SoundPlayer
    {
        private const int SAMPLE_RATE = 44100;

        /// <summary>
        /// The SN76489 PSG that will play the musical notes.
        /// </summary>
        private SN76489 psg;

        /// <summary>
        /// The GameState class holds all of the data and state for the Game currently 
        /// being run by the interpreter.
        /// </summary>
        private GameState state;

        /// <summary>
        /// The Thread that is waiting for the sound to finish.
        /// </summary>
        private Thread playerThread;

        /// <summary>
        /// The number of the Sound resource currently playing, or -1 if none should be playing.
        /// </summary>
        private int soundNumPlaying;

        /// <summary>
        /// Constructor for SoundPlayer.
        /// </summary>
        /// <param name="state"></param>
        public SoundPlayer(GameState state)
        {
            psg = new SN76489();
            this.state = state;
            this.soundNumPlaying = -1;
        }

        /// <summary>
        /// Plays the given AGI Sound.
        /// </summary>
        /// <param name="sound">The AGI Sound to play.</param>
        /// <param name="endFlag">The flag to set when the sound ends.</param>
        public void PlaySound(Sound sound, int endFlag)
        {
            bool voice1Playing = true;
            bool voice2Playing = true;
            bool voice3Playing = true;
            bool voice4Playing = true;
            int voice1SampleCount = 0;
            int voice2SampleCount = 0;
            int voice3SampleCount = 0;
            int voice4SampleCount = 0;
            List<Note> voice1Notes = sound.Notes[0];
            List<Note> voice2Notes = sound.Notes[1];
            List<Note> voice3Notes = sound.Notes[2];
            List<Note> voice4Notes = sound.Notes[3];
            int voice1NoteNum = 0;
            int voice2NoteNum = 0;
            int voice3NoteNum = 0;
            int voice4NoteNum = 0;
            int samplesPerDurationUnit = SAMPLE_RATE / 60;
            int[] bufferData = new int[2];
            MemoryStream sampleStream = new MemoryStream();

            // If the same sound is already playing, exit immediately.
            if (soundNumPlaying == sound.Index) return;

            // Stop any currently playing sound.
            StopSound();

            // Start by converting the Notes into samples.
            while (voice1Playing || voice2Playing || voice3Playing || voice4Playing)
            {
                if (voice1SampleCount-- <= 0)
                {
                    if (voice1NoteNum < voice1Notes.Count)
                    {
                        Note note = voice1Notes[voice1NoteNum++];
                        int[] psgBytes = note.Encode();
                        psg.write(psgBytes[3]);
                        psg.write(psgBytes[2]);
                        psg.write(psgBytes[4]);
                        voice1SampleCount = note.duration * samplesPerDurationUnit;
                    }
                    else
                    {
                        voice1Playing = false;
                        psg.setVolByNumber(0, 0x0F);
                    }
                }

                if (voice2SampleCount-- <= 0)
                {
                    if (voice2NoteNum < voice2Notes.Count)
                    {
                        Note note = voice2Notes[voice2NoteNum++];
                        int[] psgBytes = note.Encode();
                        psg.write(psgBytes[3]);
                        psg.write(psgBytes[2]);
                        psg.write(psgBytes[4]);
                        voice2SampleCount = note.duration * samplesPerDurationUnit;
                    }
                    else
                    {
                        voice2Playing = false;
                        psg.setVolByNumber(1, 0x0F);
                    }
                }

                if (voice3SampleCount-- <= 0)
                {
                    if (voice3NoteNum < voice3Notes.Count)
                    {
                        Note note = voice3Notes[voice3NoteNum++];
                        int[] psgBytes = note.Encode();
                        psg.write(psgBytes[3]);
                        psg.write(psgBytes[2]);
                        psg.write(psgBytes[4]);
                        voice3SampleCount = note.duration * samplesPerDurationUnit;
                    }
                    else
                    {
                        voice3Playing = false;
                        psg.setVolByNumber(2, 0x0F);
                    }
                }

                if (voice4SampleCount-- <= 0)
                {
                    if (voice4NoteNum < voice4Notes.Count)
                    {
                        Note note = voice4Notes[voice4NoteNum++];
                        int[] psgBytes = note.Encode();
                        psg.write(psgBytes[3]);
                        psg.write(psgBytes[2]);
                        psg.write(psgBytes[4]);
                        voice4SampleCount = note.duration * samplesPerDurationUnit;
                    }
                    else
                    {
                        voice4Playing = false;
                        psg.setVolByNumber(3, 0x0F);
                    }
                }

                // Use the SN76489 PSG emulation to generate the sample data.
                short sample = (short)(psg.render() * 8000);
                sampleStream.WriteByte((byte)(sample & 0xFF));
                sampleStream.WriteByte((byte)((sample >> 8) & 0xFF));
                sampleStream.WriteByte((byte)(sample & 0xFF));
                sampleStream.WriteByte((byte)((sample >> 8) & 0xFF));
            }

            // Use the samples to create a Wave file.
            // TODO: We could cache the created Wave data for the duration of the room. Sometimes rooms play the same sound over and over, e.g. The Ruby Cast :-)
            byte[] waveData = CreateWave(sampleStream.ToArray());
            MemoryStream waveStream = new MemoryStream(waveData);

            // Now play the Wave file.
            playerThread = new Thread(() => PlayWaveStreamAndWait(waveStream, sound.Index, endFlag));
            playerThread.Start();
        }

        /// <summary>
        /// Plays the Wave file data from the given MemoryStream.
        /// </summary>
        /// <param name="waveStream">The MemoryStream containing the Wave file data to play.</param>
        private void PlayWaveStreamAndWait(MemoryStream waveStream, int soundNum, int endFlag)
        {
            soundNumPlaying = soundNum;
            //PlayWithSoundPlayer(waveStream);
            PlayWithCSCore(waveStream);
            //PlayWithNAudio(waveStream);
            soundNumPlaying = -1;
            state.Flags[endFlag] = true;
        }

        /// <summary>
        /// Creates a WAVE file from the given sample data.
        /// </summary>
        /// <param name="sampleData">The sample data to create the WAVE file from.</param>
        private byte[] CreateWave(byte[] sampleData)
        {
            // Create WAVE header
            int headerLen = 44;
            int l1 = (sampleData.Length + headerLen) - 8;   // Total size of file minus 8.
            int l2 = sampleData.Length;
            byte[] wave = new byte[headerLen + sampleData.Length];
            byte[] header = new byte[] {
                82, 73, 70, 70,   // RIFF
                (byte)(l1 & 255), (byte)((l1 >> 8) & 255), (byte)((l1 >> 16) & 255), (byte)((l1 >> 24) & 255),
                87, 65, 86, 69,   // WAVE
                102, 109, 116, 32,// fmt  (chunk ID)
                16, 0, 0, 0,      // size (chunk size)
                1, 0,             // audio format (PCM = 1, i.e. Linear quantization)
                2, 0,             // number of channels
                68, 172, 0, 0,    // sample rate (samples per second), i.e. 44100
                16, 177, 2, 0,    // byte rate (average bytes per second, == SampleRate * NumChannels * BitsPerSample/8)
                4, 0,             // block align (== NumChannels * BitsPerSample/8)
                16, 0,            // bits per sample (i.e 16 bits per sample)
                100, 97, 116, 97, // data (chunk ID)
                (byte)(l2 & 255), (byte)((l2 >> 8) & 255), (byte)((l2 >> 16) & 255), (byte)((l2 >> 24) & 255)
            };
            header.CopyTo(wave, 0);

            // Append sample data
            for (int i = 0, idx = headerLen; i < sampleData.Length; ++i)
            {
                wave[idx++] = sampleData[i];
            }

            // Return the WAVE formatted typed array
            return wave;
        }

        /// <summary>
        /// Plays the WAVE file data contained in the given MemoryStream using the NAudio library.
        /// </summary>
        /// <param name="memoryStream">The MemoryStream containing the WAVE file data.</param>
        public void PlayWithNAudio(MemoryStream memoryStream)
        {
            RawSourceWaveStream rs = new RawSourceWaveStream(memoryStream, new NAudio.Wave.WaveFormat(44100, 16, 2));
            WaveOutEvent wo = new WaveOutEvent();
            wo.Init(rs);
            wo.Play();
            while ((wo.PlaybackState == NAudio.Wave.PlaybackState.Playing) && (soundNumPlaying >= 0))
            {
                Thread.Sleep(50);
            }
            wo.Dispose();
        }

        /// <summary>
        /// Plays the WAVE file data contained in the given MemoryStream using the CSCore library.
        /// </summary>
        /// <param name="memoryStream">The MemoryStream containing the WAVE file data.</param>
        public void PlayWithCSCore(MemoryStream memoryStream)
        {
            using (IWaveSource soundSource = new WaveFileReader(memoryStream))
            {
                //SoundOut implementation which plays the sound
                // WaveOut works. DirectSoundOut works. WasapiOut  works.
                using (ISoundOut soundOut = new CSCore.SoundOut.WasapiOut())
                {
                    //Tell the SoundOut which sound it has to play
                    soundOut.Initialize(soundSource);
                    //Play the sound
                    soundOut.Play();

                    while ((soundOut.PlaybackState == CSCore.SoundOut.PlaybackState.Playing) && (soundNumPlaying >= 0))
                    {
                        Thread.Sleep(10);
                    }

                    //Stop the playback
                    soundOut.Stop();
                }
            }
        }

        /// <summary>
        /// Plays the WAVE file data contained in the given MemoryStream using the System.Media.SoundPlayer.
        /// </summary>
        /// <param name="memoryStream">The MemoryStream containing the WAVE file data.</param>
        public void PlayWithSoundPlayer(MemoryStream memoryStream)
        {
            System.Media.SoundPlayer player = new System.Media.SoundPlayer(memoryStream);
            player.Stream.Seek(0, SeekOrigin.Begin);
            player.PlaySync();
            // TODO: Doesn't support stopping the sound part way through.
            player.Dispose();
            memoryStream.Dispose();
        }

        /// <summary>
        /// Stops the currently playing sound.
        /// </summary>
        public void StopSound()
        {
            if ((playerThread != null) && (soundNumPlaying >= 0))
            {
                soundNumPlaying = -1;

                while (playerThread.ThreadState != ThreadState.Stopped)
                {
                    soundNumPlaying = -1;
                    Thread.Sleep(10);
                }
            }
        }

        /// <summary>
        /// SN76489 is the audio chip used in the IBM PC Jr and therefore what the original AGI sound format was designed for.
        /// 
        /// This code is ported from Shiru's AS3 VGM player http://shiru.untergrund.net/. Ported to C# by superjoebob.
        /// </summary>
        public sealed class SN76489
        {
            private static float[] volumeTable = new float[] {
                0.25f,0.2442f,0.1940f,0.1541f,0.1224f,0.0972f,0.0772f,0.0613f,0.0487f,0.0386f,0.0307f,0.0244f,0.0193f,0.0154f,0.0122f,0,
                -0.25f,-0.2442f,-0.1940f,-0.1541f,-0.1224f,-0.0972f,-0.0772f,-0.0613f,-0.0487f,-0.0386f,-0.0307f,-0.0244f,-0.0193f,-0.0154f,-0.0122f,0,
                0.25f,0.2442f,0.1940f,0.1541f,0.1224f,0.0972f,0.0772f,0.0613f,0.0487f,0.0386f,0.0307f,0.0244f,0.0193f,0.0154f,0.0122f,0,
                0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0 };

            private uint volA;
            private uint volB;
            private uint volC;
            private uint volD;
            private int divA;
            private int divB;
            private int divC;
            private int divD;
            private int cntA;
            private int cntB;
            private int cntC;
            private int cntD;
            private float outA;
            private float outB;
            private float outC;
            private float outD;
            private uint noiseLFSR;
            private uint noiseTap;
            private uint latchedChan;
            private bool latchedVolume;

            private float ticksPerSample;
            private float ticksCount;

            public SN76489()
            {
                clock(3500000);
                reset();
            }

            public void clock(float f)
            {
                ticksPerSample = f / 16 / 44100;
            }

            public void reset()
            {
                volA = 15;
                volB = 15;
                volC = 15;
                volD = 15;
                outA = 0;
                outB = 0;
                outC = 0;
                outD = 0;
                latchedChan = 0;
                latchedVolume = false;
                noiseLFSR = 0x8000;
                ticksCount = ticksPerSample;
            }

            public uint getDivByNumber(uint chan)
            {
                switch (chan)
                {
                    case 0: return (uint)divA;
                    case 1: return (uint)divB;
                    case 2: return (uint)divC;
                    case 3: return (uint)divD;
                }
                return 0;
            }

            public void setDivByNumber(uint chan, uint div)
            {
                switch (chan)
                {
                    case 0: divA = (int)div; break;
                    case 1: divB = (int)div; break;
                    case 2: divC = (int)div; break;
                    case 3: divD = (int)div; break;
                }
            }

            public uint getVolByNumber(uint chan)
            {
                switch (chan)
                {
                    case 0: return volA;
                    case 1: return volB;
                    case 2: return volC;
                    case 3: return volD;
                }
                return 0;
            }

            public void setVolByNumber(uint chan, uint vol)
            {
                switch (chan)
                {
                    case 0: volA = vol; break;
                    case 1: volB = vol; break;
                    case 2: volC = vol; break;
                    case 3: volD = vol; break;
                }
            }

            public void write(int val)
            {
                /*
                 * A tone is produced on a voice by passing the sound chip a 3-bit register address 
                 * and then a 10-bit frequency divisor. The register address specifies which voice 
                 * the tone will be produced on. 
                 * 
                 * The actual frequency produced is the 10-bit frequency divisor given by F0 to F9
                 * divided into 1/32 of the system clock frequency (3.579 MHz) which turns out to be 
                 * 111,860 Hz. Keeping all this in mind, the following is the formula for calculating
                 * the frequency:
                 * 
                 *  f = 111860 / (((Byte2 & 0x3F) << 4) + (Byte1 & 0x0F))
                 */
                int chan;
                int cdiv;
                if ((val & 128) != 0)
                {
                    // First Byte
                    // 7  6  5  4  3  2  1  0
                    // 1  .  .  .  .  .  .  .      Identifies first byte (command byte)
                    // .  R0 R1 .  .  .  .  .      Voice number
                    // .  .  .  R2 .  .  .  .      1 = Update attenuation, 0 = Frequency count
                    // .  .  .  .  A0 A1 A2 A3     
                    // .  .  .  .  F6 F7 F8 F9     4 of 10 - bits in frequency count.
                    chan = (val >> 5) & 0x03;
                    cdiv = (int)((getDivByNumber((uint)chan) & 0xfff0) | ((uint)val & 0x0F));
                    latchedChan = (uint)chan;
                    latchedVolume = ((val & 0x10) != 0) ? true : false;
                }
                else
                {
                    // Second Byte - Frequency count only
                    // 7  6  5  4  3  2  1  0
                    // 0  .  .  .  .  .  .  .      Identifies second byte(completing byte)
                    // .  X  .  .  .  .  .  .      Unused, ignored.
                    // .  .  F0 F1 F2 F3 F4 F5     6 of 10 - bits in frequency count.
                    chan = (int)latchedChan;
                    cdiv = (int)((getDivByNumber((uint)chan) & 0x000F) | (((uint)val & 0x3F) << 4));
                }

                if (latchedVolume)
                {
                    // Update attenuation.
                    setVolByNumber((uint)chan, (uint)((getVolByNumber((uint)chan) & 16) | ((uint)val & 15)));
                }
                else
                {
                    setDivByNumber((uint)chan, (uint)cdiv);
                    if (chan == 3)
                    {
                        noiseTap = (uint)((((cdiv >> 2) & 1) != 0) ? 9 : 1);
                        noiseLFSR = 0x8000;
                    }
                }
            }

            public float render()
            {
                uint cdiv, tap;
                float outVal;

                while (ticksCount > 0)
                {
                    cntA -= 1;
                    if (cntA < 0)
                    {
                        if (divA > 1)
                        {
                            volA ^= 16;
                            outA = volumeTable[volA];
                        }
                        cntA = divA;
                    }

                    cntB -= 1;
                    if (cntB < 0)
                    {
                        if (divB > 1)
                        {
                            volB ^= 16;
                            outB = volumeTable[volB];
                        }
                        cntB = divB;
                    }

                    cntC -= 1;
                    if (cntC < 0)
                    {
                        if (divC > 1)
                        {
                            volC ^= 16;
                            outC = volumeTable[volC];
                        }
                        cntC = divC;
                    }

                    cntD -= 1;
                    if (cntD < 0)
                    {
                        cdiv = (uint)(divD & 3);
                        if (cdiv < 3) cntD = (int)(0x10 << (int)cdiv); else cntD = divC << 1;

                        if (noiseTap == 9)
                        {
                            tap = noiseLFSR & noiseTap;
                            tap ^= tap >> 8;
                            tap ^= tap >> 4;
                            tap ^= tap >> 2;
                            tap ^= tap >> 1;
                            tap &= 1;
                        }
                        else
                        {
                            tap = noiseLFSR & 1;
                        }

                        noiseLFSR = (noiseLFSR >> 1) | (tap << 15);
                        volD = (volD & 15) | ((noiseLFSR & 1 ^ 1) << 4);
                        outD = volumeTable[volD];
                    }

                    ticksCount -= 1;
                }

                ticksCount += ticksPerSample;
                outVal = outA + outB + outC + outD;

                return outVal;
            }
        }
    }
}
