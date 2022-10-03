using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using static AGI.Resource;
using static AGI.Resource.Sound;

namespace AGILE
{
    /// <summary>
    /// A class for playing AGI Sounds.
    /// </summary>
    class SoundPlayer
    {
        private const int SAMPLE_RATE = 44100;

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
        /// A cache of the generated WAVE data for loaded sounds.
        /// </summary>
        public Dictionary<int, byte[]> SoundCache { get; }

        /// <summary>
        /// The number of the Sound resource currently playing, or -1 if none should be playing.
        /// </summary>
        private int soundNumPlaying;

        /// <summary>
        /// NAudio output device that we play the generated WAVE data with.
        /// </summary>
        private readonly IWavePlayer outputDevice;

        /// <summary>
        /// NAudio ISampleProvider that mixes multiple sounds together.
        /// </summary>
        private readonly MixingSampleProvider mixer;

        /// <summary>
        /// Constructor for SoundPlayer.
        /// </summary>
        /// <param name="state"></param>
        public SoundPlayer(GameState state)
        {
            this.state = state;
            this.SoundCache = new Dictionary<int, byte[]>();
            this.soundNumPlaying = -1;

            // Set up the NAudio mixer. Using a single WaveOutEvent instance, and associated mixer eliminates
            // delays caused by creation of a WaveOutEvent per sound.
            this.outputDevice = new WaveOutEvent();
            this.mixer = new MixingSampleProvider(NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(SAMPLE_RATE, 2));
            this.mixer.ReadFully = true;
            this.outputDevice.Init(mixer);
            this.outputDevice.Play();
        }

        /// <summary>
        /// Loads and generates an AGI Sound, caching it in a ready to play state.
        /// </summary>
        /// <param name="sound">The AGI Sound to load.</param>
        public void LoadSound(Sound sound)
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
            MemoryStream sampleStream = new MemoryStream();

            // Create a new PSG for each sound, to guarantee a clean state.
            SN76496 psg = new SN76496();

            // Start by converting the Notes into samples.
            while (voice1Playing || voice2Playing || voice3Playing || voice4Playing)
            {
                if (voice1SampleCount-- <= 0)
                {
                    if (voice1NoteNum < voice1Notes.Count)
                    {
                        Note note = voice1Notes[voice1NoteNum++];
                        byte[] psgBytes = note.rawData;
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
                        byte[] psgBytes = note.rawData;
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
                        byte[] psgBytes = note.rawData;
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
                        byte[] psgBytes = note.Encode();
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

                // Use the SN76496 PSG emulation to generate the sample data.
                short sample = (short)(psg.render());

                sampleStream.WriteByte((byte)(sample & 0xFF));
                sampleStream.WriteByte((byte)((sample >> 8) & 0xFF));
                sampleStream.WriteByte((byte)(sample & 0xFF));
                sampleStream.WriteByte((byte)((sample >> 8) & 0xFF));
            }

            // Use the samples to create a Wave file. These can be several MB in size (e.g. 5MB, 8MB, 10MB)
            byte[] waveData = CreateWave(sampleStream.ToArray());

            // Cache for use when the sound is played. This reduces overhead of generating WAV on every play.
            this.SoundCache.Add(sound.Index, waveData);
        }

        /// <summary>
        /// Plays the given AGI Sound.
        /// </summary>
        /// <param name="sound">The AGI Sound to play.</param>
        /// <param name="endFlag">The flag to set when the sound ends.</param>
        public void PlaySound(Sound sound, int endFlag)
        {
            // Stop any currently playing sound. Will set the end flag for the previous sound.
            StopSound(false);

            // Set the starting state of the sound end flag to false.
            state.Flags[endFlag] = false;

            // Get WAV data from the cache.
            byte[] waveData = null;
            if (this.SoundCache.TryGetValue(sound.Index, out waveData))
            {
                // Now play the Wave file.
                MemoryStream memoryStream = new MemoryStream(waveData);
                playerThread = new Thread(() => PlayWaveStreamAndWait(memoryStream, sound.Index, endFlag));
                playerThread.Start();
            }
        }

        /// <summary>
        /// Plays the Wave file data from the given MemoryStream.
        /// </summary>
        /// <param name="waveStream">The MemoryStream containing the Wave file data to play.</param>
        private void PlayWaveStreamAndWait(MemoryStream waveStream, int soundNum, int endFlag)
        {
            if (this.state.Flags[Defines.SOUNDON])
            {
                soundNumPlaying = soundNum;
                PlayWithNAudioMix(waveStream);
            }

            // The above call blocks until the sound has finished playing. If sound is not on, then it happens immediately.
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
        public void PlayWithNAudioMix(MemoryStream memoryStream)
        {
            // Add the new sound as an input to the NAudio mixer.
            RawSourceWaveStream rs = new RawSourceWaveStream(memoryStream, new NAudio.Wave.WaveFormat(44100, 16, 2));
            ISampleProvider soundMixerInput = rs.ToSampleProvider();
            mixer.AddMixerInput(soundMixerInput);

            // Register a handler for when this specific sound ends.
            bool playbackEnded = false;
            void handlePlaybackEnded(object sender, SampleProviderEventArgs args)
            {
                // It is possible that we get sound overlaps, so we check that this is the same sound.
                if (System.Object.ReferenceEquals(args.SampleProvider, soundMixerInput))
                {
                    mixer.MixerInputEnded -= handlePlaybackEnded;
                    playbackEnded = true;
                }
            };
            mixer.MixerInputEnded += handlePlaybackEnded;

            // Wait until either the sound has ended, or we have been told to stop.
            while (!playbackEnded && (soundNumPlaying >= 0))
            {
                Thread.Sleep(50);
            }

            // If we didn't stop due to the playback ending, then tell it to stop playing.
            if (!playbackEnded)
            {
                mixer.RemoveMixerInput(soundMixerInput);
            }
        }

        /// <summary>
        /// Resets the internal state of the SoundPlayer.
        /// </summary>
        public void Reset()
        {
            StopSound();
            SoundCache.Clear();
            mixer.RemoveAllMixerInputs();
        }

        /// <summary>
        /// Fully shuts down the SoundPlayer. Only intended for when AGILE is closing down.
        /// </summary>
        public void Shutdown()
        {
            Reset();
            outputDevice.Stop();
            outputDevice.Dispose();
        }

        /// <summary>
        /// Stops the currently playing sound
        /// </summary>
        /// <param name="wait">true to wait for the player thread to stop; otherwise false to not wait.</param>
        public void StopSound(bool wait = true)
        {
            if (soundNumPlaying >= 0)
            {
                // This tells the thread to stop.
                soundNumPlaying = -1;

                if (playerThread != null)
                {
                    // We wait for the thread to stop only if instructed to do so.
                    if (wait)
                    {
                        while (playerThread.ThreadState != ThreadState.Stopped)
                        {
                            soundNumPlaying = -1;
                            Thread.Sleep(10);
                        }
                    }

                    playerThread = null;
                }
            }
        }

        /// <summary>
        /// SN76496 is the audio chip used in the IBM PC JR and therefore what the original AGI sound format was designed for.
        /// </summary>
        public sealed class SN76496
        {
            private const float IBM_PCJR_CLOCK = 3579545f;

            private static float[] volumeTable = new float[] {
                8191.5f,
                6506.73973474395f,
                5168.4870873095f,
                4105.4752242578f,
                3261.09488758897f,
                2590.37974532693f,
                2057.61177037107f,
                1634.41912530676f,
                1298.26525860452f,
                1031.24875107119f,
                819.15f,
                650.673973474395f,
                516.84870873095f,
                410.54752242578f,
                326.109488758897f,
                0.0f
            };

            private int[] channelVolume = new int[4] { 15, 15, 15, 15 };
            private int[] channelCounterReload = new int[4];
            private int[] channelCounter = new int[4];
            private int[] channelOutput = new int[4];
            private uint lfsr;
            private uint latchedChannel;
            private bool updateVolume;
            private float ticksPerSample;
            private float ticksCount;

            public SN76496()
            {
                ticksPerSample = IBM_PCJR_CLOCK / 16 / SAMPLE_RATE;
                ticksCount = ticksPerSample;
                latchedChannel = 0;
                updateVolume = false;
                lfsr = 0x4000;
            }

            public void setVolByNumber(uint channel, uint volume)
            {
                switch (channel)
                {
                    case 0: channelVolume[0] = (int)(volume & 0x0F); break;
                    case 1: channelVolume[1] = (int)(volume & 0x0F); break;
                    case 2: channelVolume[2] = (int)(volume & 0x0F); break;
                    case 3: channelVolume[3] = (int)(volume & 0x0F); break;
                }
            }

            public void write(int data)
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
                int counterReloadValue;

                if ((data & 0x80) != 0)
                {
                    // First Byte
                    // 7  6  5  4  3  2  1  0
                    // 1  .  .  .  .  .  .  .      Identifies first byte (command byte)
                    // .  R0 R1 .  .  .  .  .      Voice number (i.e. channel)
                    // .  .  .  R2 .  .  .  .      1 = Update attenuation, 0 = Frequency count
                    // .  .  .  .  A0 A1 A2 A3     4-bit attenuation value.
                    // .  .  .  .  F6 F7 F8 F9     4 of 10 - bits in frequency count.
                    latchedChannel = (uint)(data >> 5) & 0x03;
                    counterReloadValue = (int)(((uint)channelCounterReload[latchedChannel] & 0xfff0) | ((uint)data & 0x0F));
                    updateVolume = ((data & 0x10) != 0) ? true : false;
                }
                else
                {
                    // Second Byte - Frequency count only
                    // 7  6  5  4  3  2  1  0
                    // 0  .  .  .  .  .  .  .      Identifies second byte (completing byte for frequency count)
                    // .  X  .  .  .  .  .  .      Unused, ignored.
                    // .  .  F0 F1 F2 F3 F4 F5     6 of 10 - bits in frequency count.
                    counterReloadValue = (int)(((uint)channelCounterReload[latchedChannel] & 0x000F) | (((uint)data & 0x3F) << 4));
                }

                if (updateVolume)
                {
                    // Volume latched. Update attenuation for latched channel.
                    channelVolume[latchedChannel] = (data & 0x0F);
                }
                else
                {
                    // Data latched. Update counter reload register for channel.
                    channelCounterReload[latchedChannel] = counterReloadValue;

                    // If it is for the noise control register, then set LFSR back to starting value.
                    if (latchedChannel == 3) lfsr = 0x4000;
                }
            }

            private void UpdateToneChannel(int channel)
            {
                // If the tone counter reload register is 0, then skip update.
                if (channelCounterReload[channel] == 0) return;

                // Note: For some reason SQ2 intro, in docking scene, is quite sensitive to how this is decremented and tested.

                // Decrement channel counter. If zero, then toggle output and reload from
                // the tone counter reload register.
                if (--channelCounter[channel] <= 0)
                {
                    channelCounter[channel] = channelCounterReload[channel];
                    channelOutput[channel] ^= 1;
                }
            }

            public float render()
            {
                while (ticksCount > 0)
                {
                    UpdateToneChannel(0);
                    UpdateToneChannel(1);
                    UpdateToneChannel(2);

                    channelCounter[3] -= 1;
                    if (channelCounter[3] < 0)
                    {
                        // Reload noise counter.
                        if ((channelCounterReload[3] & 0x03) < 3)
                        {
                            channelCounter[3] = (0x20 << (channelCounterReload[3] & 3));
                        }
                        else
                        {
                            // In this mode, the counter reload value comes from tone register 2.
                            channelCounter[3] = channelCounterReload[2];
                        }

                        uint feedback = ((channelCounterReload[3] & 0x04) == 0x04) ?
                            // White noise. Taps bit 0 and bit 1 of the LFSR as feedback, with XOR.
                            ((lfsr & 0x0001) ^ ((lfsr & 0x0002) >> 1)) :
                            // Periodic. Taps bit 0 for the feedback.
                            (lfsr & 0x0001);

                        // LFSR is shifted every time the counter times out. SR is 15-bit. Feedback added to top bit.
                        lfsr = (lfsr >> 1) | (feedback << 14);
                        channelOutput[3] = (int)(lfsr & 1);
                    }

                    ticksCount -= 1;
                }

                ticksCount += ticksPerSample;

                return (float)((volumeTable[channelVolume[0] & 0x0F] * ((channelOutput[0] - 0.5) * 2)) +
                               (volumeTable[channelVolume[1] & 0x0F] * ((channelOutput[1] - 0.5) * 2)) +
                               (volumeTable[channelVolume[2] & 0x0F] * ((channelOutput[2] - 0.5) * 2)) +
                               (volumeTable[channelVolume[3] & 0x0F] * ((channelOutput[3] - 0.5) * 2)));
            }
        }
    }
}
