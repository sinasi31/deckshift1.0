using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes the UI sound family to .wav files you can click to audition in the Project window — no
/// Play mode needed. Menu: **Deckshift → Bake UI SFX Previews**.
///
/// Same throwaway pattern as `JumpSfxPreviewBaker`; the folder is safe to delete once the sounds
/// are signed off. The live sounds are always the `ProcSfx.UI*` clips, never these files.
///
/// The numeric prefixes are so the Project window lists them in the order they should be judged:
/// Move first because it fires most often and is the one that becomes annoying, then the
/// Confirm/Cancel/Refuse trio (which have to be told apart from each other), then the Open/Close
/// pair (which have to read as the same figure inverted).
/// </summary>
public static class UISfxPreviewBaker
{
    private const string OutDir = "Assets/ProcSfxPreview";

    [MenuItem("Deckshift/Bake UI SFX Previews")]
    public static void Bake()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        var names = new[] { "UI_1_Move", "UI_2_Confirm", "UI_3_Cancel", "UI_4_Refuse", "UI_5_Open", "UI_6_Close" };
        var clips = new[] { ProcSfx.UIMove, ProcSfx.UIConfirm, ProcSfx.UICancel,
                            ProcSfx.UIRefuse, ProcSfx.UIOpen, ProcSfx.UIClose };

        for (int i = 0; i < names.Length; i++)
        {
            AudioClip c = clips[i];
            if (c == null) { Debug.LogWarning("[ProcSfx] " + names[i] + " built null"); continue; }

            var samples = new float[c.samples * c.channels];
            c.GetData(samples, 0);
            WriteWav(OutDir + "/" + names[i] + ".wav", samples, c.frequency, c.channels);
        }

        AssetDatabase.Refresh();
        Debug.Log("[ProcSfx] Baked " + names.Length + " UI previews to " + OutDir +
                  " — click each .wav in the Project window to audition.\n" +
                  "Judge in this order: Move (fires constantly — does it annoy?), then " +
                  "Confirm/Cancel/Refuse (can you tell them apart with your eyes shut?), then " +
                  "Open/Close (do they read as the same figure inverted?).");
    }

    // Minimal 16-bit PCM WAV writer (BinaryWriter is little-endian, which is what WAV wants).
    private static void WriteWav(string path, float[] samples, int sampleRate, int channels)
    {
        const int bits = 16;
        int blockAlign = channels * bits / 8;
        int byteRate = sampleRate * blockAlign;
        int dataSize = samples.Length * (bits / 8);

        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataSize);
            bw.Write(new[] { 'W', 'A', 'V', 'E' });

            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);            // PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)blockAlign);
            bw.Write((short)bits);

            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(dataSize);
            foreach (float s in samples)
                bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }
}
