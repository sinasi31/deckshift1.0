using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Design-time helper: bakes the procedural SHIFT SFX variants to .wav files you can click to
// audition in the Project window — no Play mode, no MCP bridge needed. Run via the menu:
//   Deckshift → Bake Shift SFX Previews
// The .wav files land in Assets/ProcSfxPreview/ and are throwaway (delete the folder once the
// sound is locked). The LIVE game sound is always ProcSfx.Jump (= BuildShift(ProcSfx.Default)).
public static class JumpSfxPreviewBaker
{
    private const string OutDir = "Assets/ProcSfxPreview";

    [MenuItem("Deckshift/Bake Shift SFX Previews")]
    public static void Bake()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        var names = new[] { "ShiftA_Glass", "ShiftB_Breath", "ShiftC_Bloom" };
        var variants = new[] { ProcSfx.ShiftStyle.Glass, ProcSfx.ShiftStyle.Breath, ProcSfx.ShiftStyle.Bloom };

        for (int i = 0; i < names.Length; i++)
        {
            AudioClip clip = ProcSfx.BuildShift(variants[i]);
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            WriteWav(OutDir + "/" + names[i] + ".wav", samples, clip.frequency, clip.channels);
        }

        AssetDatabase.Refresh();
        Debug.Log("[ProcSfx] Baked " + names.Length + " shift previews to " + OutDir
                  + " — click each .wav in the Project window to audition.");
    }

    // Minimal 16-bit PCM WAV writer (BinaryWriter is always little-endian, which WAV wants).
    private static void WriteWav(string path, float[] samples, int sampleRate, int channels)
    {
        const int bits = 16;
        int blockAlign = channels * bits / 8;
        int byteRate = sampleRate * blockAlign;
        int dataSize = samples.Length * (bits / 8);

        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);               // PCM fmt chunk size
            bw.Write((short)1);         // PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)blockAlign);
            bw.Write((short)bits);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);

            foreach (float s in samples)
                bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), short.MinValue, short.MaxValue));
        }
    }
}
