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

        private readonly short[] dissolveDataV2 = new short[]
        {
              -2,   -3,   -2,   -1, 0x00, 0x00, 0x01, 0x01, 
            0x01, 0x01, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 
            0x02, 0x02, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03,
            0x03, 0x04, 0x04, 0x04, 0x04, 0x05, 0x05, 0x05, 
            0x05, 0x06, 0x06, 0x06, 0x06, 0x06, 0x07, 0x07, 
            0x07, 0x07, 0x08, 0x08, 0x08, 0x08, 0x09, 0x09, 
            0x09, 0x09, 0x0A, 0x0A, 0x0A, 0x0A, 0x0B, 0x0B, 
            0x0B, 0x0B, 0x0B, 0x0B, 0x0C, 0x0C, 0x0C, 0x0C, 
            0x0C, 0x0C, 0x0D, -100
        };

        private readonly short[] dissolveDataV3 = new short[]
        {
              -2,   -3,   -2,   -1, 0x00, 0x00, 0x00, 0x00, 
            0x00, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x02, 
            0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02,
            0x02, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03,
            0x03, 0x04, 0x04, 0x04, 0x04, 0x04, 0x05, 0x05, 
            0x05, 0x05, 0x05, 0x06, 0x06, 0x06, 0x06, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x08, 0x08, 0x08, 0x08,
            0x09, 0x09, 0x09, 0x09, 0x0A, 0x0A, 0x0A, 0x0A,
            0x0B, 0x0B, 0x0B, 0x0B, 0x0B, 0x0B, 0x0C, 0x0C, 
            0x0C, 0x0C, 0x0C, 0x0C, 0x0D, -100
        };

        private short[] dissolveData;

        /// <summary>
        /// Constructor for SoundPlayer.
        /// </summary>
        /// <param name="state"></param>
        public SoundPlayer(GameState state)
        {
            this.state = state;
            this.SoundCache = new Dictionary<int, byte[]>();
            this.soundNumPlaying = -1;
            this.dissolveData = (state.IsAGIV3? dissolveDataV3 : dissolveDataV2);

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
            Note[] voiceCurrentNote = new Note[4];
            bool[] voicePlaying = new bool[4] { true, true, true, true };
            int[] voiceSampleCount = new int[4];
            int[] voiceNoteNum = new int[4];
            int[] voiceDissolveCount = new int[4];
            int durationUnitCount = 0;

            // A single note duration unit is 1/60th of a second
            int samplesPerDurationUnit = SAMPLE_RATE / 60;
            MemoryStream sampleStream = new MemoryStream();

            // Create a new PSG for each sound, to guarantee a clean state.
            SN76496 psg = new SN76496();

            // Start by converting the Notes into samples.
            while (voicePlaying[0] || voicePlaying[1] || voicePlaying[2] || voicePlaying[3])
            {
                for (int voiceNum = 0; voiceNum < 4; voiceNum++)
                {
                    if (voicePlaying[voiceNum])
                    {
                        if (voiceSampleCount[voiceNum]-- <= 0)
                        {
                            if (voiceNoteNum[voiceNum] < sound.Notes[voiceNum].Count)
                            {
                                voiceCurrentNote[voiceNum] = sound.Notes[voiceNum][voiceNoteNum[voiceNum]++];
                                byte[] psgBytes = voiceCurrentNote[voiceNum].rawData;
                                psg.Write(psgBytes[3]);
                                psg.Write(psgBytes[2]);
                                psg.Write(psgBytes[4]);
                                voiceSampleCount[voiceNum] = voiceCurrentNote[voiceNum].duration * samplesPerDurationUnit;
                                voiceDissolveCount[voiceNum] = 0;
                            }
                            else
                            {
                                voicePlaying[voiceNum] = false;
                                psg.SetVolByNumber(voiceNum, 0x0F);
                            }
                        }
                        if ((durationUnitCount == 0) && (voicePlaying[voiceNum]))
                        {
                            voiceDissolveCount[voiceNum] = UpdateVolume(psg, voiceCurrentNote[voiceNum].origVolume, voiceNum, voiceDissolveCount[voiceNum]);
                        }
                    }
                }

                // This count hits zero 60 times a second. It counts samples from 0 to 734 (i.e. (44100 / 60) - 1).
                durationUnitCount = ((durationUnitCount + 1) % samplesPerDurationUnit);

                // Use the SN76496 PSG emulation to generate the sample data.
                short sample = (short)(psg.Render());
                sampleStream.WriteByte((byte)(sample & 0xFF));
                sampleStream.WriteByte((byte)((sample >> 8) & 0xFF));
                sampleStream.WriteByte((byte)(sample & 0xFF));
                sampleStream.WriteByte((byte)((sample >> 8) & 0xFF));
            }

            // Cache for use when the sound is played. This reduces overhead of generating WAV on every play.
            this.SoundCache.Add(sound.Index, sampleStream.ToArray());
        }

        /// <summary>
        /// Updates the volume of the given channel, by applying the dissolve data and master volume to the 
        /// given base volume and then sets that in the SN76496 PSG. The noise channel does not apply the
        /// dissolve data, so skips that bit.
        /// </summary>
        /// <param name="psg">The SN76496 PSG to set the calculated volume in.</param>
        /// <param name="baseVolume">The base volume to apply the dissolve data and master volume to.</param>
        /// <param name="channel">The channel to update the volume for.</param>
        /// <param name="dissolveCount">The current dissolve count value for the note being played by the given channel.</param>
        /// <returns>The new dissolve count value for the channel.</returns>
        private int UpdateVolume(SN76496 psg, int baseVolume, int channel, int dissolveCount)
        {
            int volume = baseVolume;

            if (volume != 0x0F)
            {
                int dissolveValue = (dissolveData[dissolveCount] == -100 ? dissolveData[dissolveCount - 1] : dissolveData[dissolveCount++]);

                // Add master volume and dissolve value to current channel volume. Noise channel doesn't dissolve.
                if (channel < 3) volume += dissolveValue;
                
                volume += this.state.Vars[Defines.ATTENUATION];

                if (volume < 0) volume = 0;
                if (volume > 0x0F) volume = 0x0F;
                if (volume < 8) volume += 2;

                // Apply calculated volume to PSG channel.
                psg.SetVolByNumber(channel, volume);
            }

            return dissolveCount;
        }

        /// <summary>
        /// Plays the given AGI Sound.
        /// </summary>
        /// <param name="sound">The AGI Sound to play.</param>
        /// <param name="endFlag">The flag to set when the sound ends.</param>
        public void PlaySound(Sound sound, int endFlag)
        {
            // Stop any currently playing sound. Will set the end flag for the previous sound.
            StopSound();

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
                Thread.Sleep(10);
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

            public void SetVolByNumber(int channel, int volume)
            {
                channelVolume[channel] = (int)(volume & 0x0F);
            }

            public int GetVolByNumber(int channel)
            {
                return (channelVolume[channel] & 0x0F);
            }

            public void Write(int data)
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

            public float Render()
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
