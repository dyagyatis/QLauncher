using System;
using System.IO;
using System.Media;

namespace MinecraftLauncher
{
    public static class SoundManager
    {
        private static byte[]? _cachedClickWav;
        private static byte[]? _cachedLaunchWav;

        public static void PlayClickSound()
        {
            try
            {
                var settings = SettingsManager.Load();
                if (!settings.EnableUiSounds) return;

                _cachedClickWav ??= GenerateClickSoundWav();
                using var ms = new MemoryStream(_cachedClickWav);
                using var player = new SoundPlayer(ms);
                player.Play();
            }
            catch { }
        }

        public static void PlayLaunchSound()
        {
            try
            {
                var settings = SettingsManager.Load();
                if (!settings.EnableUiSounds) return;

                _cachedLaunchWav ??= GenerateLaunchSoundWav();
                using var ms = new MemoryStream(_cachedLaunchWav);
                using var player = new SoundPlayer(ms);
                player.Play();
            }
            catch { }
        }

        private static byte[] GenerateClickSoundWav()
        {
            int sampleRate = 44100;
            double duration = 0.035; // 35 ms
            int numSamples = (int)(sampleRate * duration);
            short[] samples = new short[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                // Exponential decay envelope
                double envelope = Math.Exp(-t * 150);
                // Frequency sweep from 1400Hz to 700Hz
                double freq = 1400 - (t / duration) * 700;
                double wave = Math.Sin(2 * Math.PI * freq * t);

                samples[i] = (short)(wave * envelope * 14000);
            }

            return CreateWavHeaderAndData(samples, sampleRate);
        }

        private static byte[] GenerateLaunchSoundWav()
        {
            int sampleRate = 44100;
            double duration = 0.28; // 280 ms
            int numSamples = (int)(sampleRate * duration);
            short[] samples = new short[numSamples];

            // Ascending major chord (C5=523Hz, E5=659Hz, G5=784Hz)
            double[] freqs = { 523.25, 659.25, 783.99 };

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                double envelope = Math.Sin(Math.PI * (t / duration)) * Math.Exp(-t * 6);

                double wave = 0;
                for (int f = 0; f < freqs.Length; f++)
                {
                    double delay = f * 0.04;
                    if (t >= delay)
                    {
                        wave += Math.Sin(2 * Math.PI * freqs[f] * (t - delay));
                    }
                }

                samples[i] = (short)(wave * envelope * 6000);
            }

            return CreateWavHeaderAndData(samples, sampleRate);
        }

        private static byte[] CreateWavHeaderAndData(short[] samples, int sampleRate)
        {
            int dataSize = samples.Length * 2;
            int totalSize = 36 + dataSize;

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // RIFF header
            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(totalSize);
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });

            // fmt sub-chunk
            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16); // Subchunk1Size (16 for PCM)
            writer.Write((short)1); // AudioFormat (1 for PCM)
            writer.Write((short)1); // NumChannels (1 mono)
            writer.Write(sampleRate); // SampleRate
            writer.Write(sampleRate * 2); // ByteRate (SampleRate * NumChannels * BitsPerSample/8)
            writer.Write((short)2); // BlockAlign (NumChannels * BitsPerSample/8)
            writer.Write((short)16); // BitsPerSample

            // data sub-chunk
            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);

            for (int i = 0; i < samples.Length; i++)
            {
                writer.Write(samples[i]);
            }

            return ms.ToArray();
        }
    }
}
