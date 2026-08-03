using UnityEngine;

// House-style PROCEDURAL sound effects (no audio files — clips are synthesized in code from
// oscillators, filtered noise + a tiny reverb, the same "generate the data ourselves" idea as the
// procedural sprites in CardAimIndicator / SpitGlob). Clips are baked once on first access and
// cached, so playback is a normal AudioClip through SfxManager.PlayOn.
//
// Usage:  SfxManager.PlayOn(audioSource, ProcSfx.Jump);
//
// DESIGN NOTE (2026-07-17): the "jump" action SPENDS Shift — the arcane kinetic resource — so it
// is a SHIFT (arcane displacement), NOT a physical jump (no thump). Rejected directions, in order:
// a jump/thump (too physical), a high shimmer (annoying), a warm dark thump (still a jump), and a
// flanger+doppler teleport (too ELECTRIC/sci-fi for a candlelit alchemist's den). This pass is
// ORGANIC + ARCANE: filtered air displacement ("fwsh") + a warm struck-glass/resonant bloom (a
// nod to the potion bottles in the room) — sine-based and noise-based only, no synthy flanger or
// laser sweep. Three styles are provided to A/B (JumpSfxPreviewBaker). Live sound = DefaultStyle.
public static class ProcSfx
{
    private const int SampleRate = 44100;

    public enum ShiftStyle { Glass, Breath, Bloom }
    // Bloom was the closest of the three to the designer's target (2026-07-17) — the resume point
    // when the "shift" SFX hunt continues. (Currently the live jump uses the original mp3, not this.)
    public static readonly ShiftStyle DefaultStyle = ShiftStyle.Bloom;

    private static AudioClip jump;
    public static AudioClip Jump
    {
        get
        {
            if (jump == null) jump = BuildShift(DefaultStyle);
            return jump;
        }
    }

    public static AudioClip BuildShift(ShiftStyle style)
    {
        switch (style)
        {
            case ShiftStyle.Breath: return BuildBreath();
            case ShiftStyle.Bloom:  return BuildBloom();
            default:                return BuildGlass();
        }
    }

    // A — GLASS: a soft air "fwsh" of displacement + a warm struck-glass resonance (potion-bottle
    // nod). Organic and magical, a little bright but warm — "displace + soft enchanted ring".
    private static AudioClip BuildGlass()
    {
        const float dur = 0.45f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(9021);

        float svfLow = 0f, svfBand = 0f;   // band-pass on the air
        float f0 = 430f;                   // glass fundamental
        float[] ratio = { 1f, 2.38f, 3.94f };   // struck-glass-ish partials (kept low/warm)
        float[] pAmp  = { 1f, 0.42f, 0.18f };
        float[] pDec  = { 6f, 10f, 15f };        // higher partials die faster (natural)

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            // air displacement: band-pass noise whose center falls (a settling "fwsh").
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(1700f, 620f, ts / dur);
            float f = 2f * Mathf.Sin(Mathf.PI * cutoff / SampleRate);
            svfLow += f * svfBand;
            float high = noise - svfLow - 0.42f * svfBand;
            svfBand += f * high;
            float airEnv = Mathf.Clamp01(ts / 0.006f) * Mathf.Exp(-16f * ts);

            // struck-glass bloom.
            float bell = 0f;
            for (int k = 0; k < ratio.Length; k++)
                bell += pAmp[k] * Mathf.Sin(2f * Mathf.PI * f0 * ratio[k] * ts) * Mathf.Exp(-pDec[k] * ts);
            float bellAtk = Mathf.Clamp01(ts / 0.003f);

            float sub = Mathf.Sin(2f * Mathf.PI * 92f * ts) * Mathf.Exp(-28f * ts);

            dry[i] = svfBand * airEnv * 0.16f
                   + bell * bellAtk * 0.13f
                   + sub * 0.08f;
        }

        return Finalize(dry, 0.10f, 2800f);
    }

    // B — BREATH: mostly warm air — a soft robe/air swish of displacement with only a whisper of
    // magical tone. The most organic/ethereal option, darker and breathier.
    private static AudioClip BuildBreath()
    {
        const float dur = 0.42f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5540);

        float lowLp = 0f, airLp = 0f;
        float lowCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 650f / SampleRate);
        float airCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 1500f / SampleRate);

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lowLp += lowCoef * (noise - lowLp);   // body of the breath
            airLp += airCoef * (noise - airLp);   // airy top

            // Soft swell-in then fade (a passing breath).
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(ts / dur), 0.45f));

            // Whisper of warm magic underneath.
            float tone = Mathf.Sin(2f * Mathf.PI * 320f * ts) * (1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 6f * ts));
            float toneEnv = Mathf.Clamp01(ts / 0.02f) * Mathf.Exp(-7f * ts);

            dry[i] = lowLp * env * 0.20f
                   + airLp * env * 0.12f
                   + tone * toneEnv * 0.06f;
        }

        return Finalize(dry, 0.13f, 2400f);
    }

    // C — BLOOM: a warm resonant magical swell — high-resonance band-pass noise "whooom" that
    // blooms in and fades, dreamy and enchanted, with a little air. More "spell energy" than air.
    private static AudioClip BuildBloom()
    {
        const float dur = 0.5f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(3312);

        float svfLow = 0f, svfBand = 0f;   // resonant band-pass = a pitched airy bloom
        float airLp = 0f;
        float airCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 1600f / SampleRate);

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            // Pitched resonant bloom around ~440 Hz (high resonance = low q).
            float cutoff = Mathf.Lerp(360f, 520f, Mathf.Clamp01(ts / dur));
            float f = 2f * Mathf.Sin(Mathf.PI * cutoff / SampleRate);
            svfLow += f * svfBand;
            float high = noise - svfLow - 0.10f * svfBand;
            svfBand += f * high;
            // Swell IN over ~90ms, then a gentle fall — the magic gathering and releasing.
            float bloomEnv = Mathf.Clamp01(ts / 0.09f) * Mathf.Exp(-4.5f * ts);

            airLp += airCoef * (noise - airLp);
            float airEnv = Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(ts / dur), 0.5f));

            dry[i] = svfBand * bloomEnv * 0.16f
                   + airLp * airEnv * 0.09f;
        }

        return Finalize(dry, 0.14f, 2600f);
    }

    private static AudioClip scrapPickup;
    // Collecting a scrap shard. Procedural because ScrapPickup builds its GameObject entirely in
    // code (no prefab), so there is nowhere to hang an AudioClip in the Inspector.
    public static AudioClip ScrapPickup
    {
        get
        {
            if (scrapPickup == null) scrapPickup = BuildScrapPickup();
            return scrapPickup;
        }
    }

    // A small bright metal "tink" — a struck iron offcut, not a coin. Built from INHARMONIC
    // partials (the 1 : 2.76 : 5.40 : 8.93 ideal-bar mode ratios), which is what makes metal read
    // as metal rather than as a pitched chime; harmonic ratios here would sound like a bell and
    // collide with the gold pickup. Short and dry so a five-shard burst layers into a satisfying
    // rattle instead of a wash.
    private static AudioClip BuildScrapPickup()
    {
        const float dur = 0.22f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(4471);

        float f0 = 920f;                                    // small shard = high fundamental
        float[] ratio = { 1f, 2.76f, 5.40f, 8.93f };        // ideal free-bar modes
        float[] pAmp = { 1f, 0.55f, 0.28f, 0.12f };
        float[] pDec = { 22f, 30f, 42f, 58f };              // brighter modes die faster

        float clickLp = 0f;
        float clickCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 5200f / SampleRate);

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            // Struck partials.
            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * f0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            // Contact transient: a very short filtered noise tick that sells the "hit".
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            clickLp += clickCoef * (noise - clickLp);
            float clickEnv = Mathf.Exp(-190f * ts);

            dry[i] = body * 0.13f + clickLp * clickEnv * 0.20f;
        }

        return Finalize(dry, 0.07f, 9000f);   // dry and bright — metal, close-miked
    }

    private static AudioClip arcaneGather, arcaneBind;

    // Blompo's blessing, part one: power gathering. A rising shimmer under the ring-and-motes
    // sequence, ending just as the blessing sets.
    public static AudioClip ArcaneGather
    {
        get { if (arcaneGather == null) arcaneGather = BuildArcaneGather(); return arcaneGather; }
    }

    // Blompo's blessing, part two: the bind. A struck chime that blooms.
    public static AudioClip ArcaneBind
    {
        get { if (arcaneBind == null) arcaneBind = BuildArcaneBind(); return arcaneBind; }
    }

    // Rising shimmer. Band-passed noise whose cutoff GLIDES upward, plus two sine partials sliding
    // up a fifth — the pitch rise is what makes it read as "something is building" rather than as
    // ambience. Swells to ~85% of its length so it peaks into the bind rather than after it.
    private static AudioClip BuildArcaneGather()
    {
        const float dur = 1.5f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(7717);

        float svfLow = 0f, svfBand = 0f;
        double p1 = 0.0, p2 = 0.0;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float t01 = ts / dur;

            // Resonant band-pass climbing 400 -> 2600 Hz.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(400f, 2600f, t01 * t01);
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(cutoff, SampleRate * 0.45f) / SampleRate);
            svfLow += f * svfBand;
            float high = noise - svfLow - 0.22f * svfBand;
            svfBand += f * high;

            // Two partials gliding up a fifth. Phase is integrated (not sin(2*pi*f*t)) so the
            // frequency can change without the waveform tearing.
            float g1 = Mathf.Lerp(392f, 587f, t01);
            float g2 = Mathf.Lerp(587f, 880f, t01);
            p1 += 2.0 * Mathf.PI * g1 / SampleRate;
            p2 += 2.0 * Mathf.PI * g2 / SampleRate;

            float swell = Mathf.Pow(Mathf.Clamp01(t01 / 0.85f), 1.6f);
            float tail = 1f - Mathf.Clamp01((t01 - 0.85f) / 0.15f) * 0.4f;
            float env = swell * tail;

            dry[i] = svfBand * env * 0.13f
                   + (float)System.Math.Sin(p1) * env * 0.045f
                   + (float)System.Math.Sin(p2) * env * 0.030f;
        }

        return Finalize(dry, 0.22f, 6000f);
    }

    // The bind: a struck chime that blooms open.
    //
    // HARMONIC partials on purpose (1, 2, 3, 4, 5.1) — that's what makes it read as a bell, i.e.
    // as magic. The scrap pickup deliberately uses INHARMONIC bar modes so it reads as metal; the
    // two sounds should never be confusable, and the partial ratios are the whole difference.
    private static AudioClip BuildArcaneBind()
    {
        const float dur = 1.8f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(2286);

        float f0 = 587.33f;                                    // D5
        float[] ratio = { 1f, 2f, 3f, 4f, 5.1f };              // 5.1 is stretched for shimmer
        float[] pAmp = { 1f, 0.50f, 0.30f, 0.16f, 0.09f };
        float[] pDec = { 2.6f, 3.6f, 5.0f, 7.0f, 9.0f };

        // A few high sparkles scattered through the first 250ms.
        int sparkCount = 7;
        float[] sparkAt = new float[sparkCount];
        float[] sparkHz = new float[sparkCount];
        for (int k = 0; k < sparkCount; k++)
        {
            sparkAt[k] = (float)rng.NextDouble() * 0.25f;
            sparkHz[k] = 1800f + (float)rng.NextDouble() * 2600f;
        }

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * f0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            // Sub bloom an octave down, swelling in rather than striking — the "opening" feeling.
            float sub = Mathf.Sin(2f * Mathf.PI * (f0 * 0.5f) * ts)
                      * Mathf.Clamp01(ts / 0.12f) * Mathf.Exp(-2.2f * ts);

            float spark = 0f;
            for (int k = 0; k < sparkCount; k++)
            {
                float st = ts - sparkAt[k];
                if (st <= 0f) continue;
                spark += Mathf.Sin(2f * Mathf.PI * sparkHz[k] * st) * Mathf.Exp(-26f * st);
            }

            dry[i] = body * 0.115f + sub * 0.055f + spark * 0.020f;
        }

        return Finalize(dry, 0.30f, 9000f);   // wetter than the forge sounds — this one has space
    }

    // Shared tail: small warm reverb + master low-pass + anti-click fades -> AudioClip.
    private static AudioClip Finalize(float[] dry, float reverbWet, float masterLpHz)
    {
        float[] s = ApplyReverbAndWarmth(dry, reverbWet, masterLpHz);

        int n = s.Length;
        int fade = Mathf.Min(n / 2, SampleRate / 400);   // ~2.5ms
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            s[i] *= k;
            s[n - 1 - i] *= k;
        }

        var clip = AudioClip.Create("ProcSfx_Shift", n, 1, SampleRate, false);
        clip.SetData(s, 0);
        return clip;
    }

    // Small Schroeder reverb (3 damped combs -> 1 allpass) for a tight, warm stone room, then a
    // one-pole master low-pass. Damping keeps the tail warm/candlelit.
    private static float[] ApplyReverbAndWarmth(float[] dry, float wet, float masterLpHz)
    {
        int n = dry.Length;

        int[] combLen = { 811, 971, 1123 };
        const float combFb = 0.66f;
        const float damp = 0.34f;
        var combBuf = new float[combLen.Length][];
        var combPos = new int[combLen.Length];
        var combStore = new float[combLen.Length];
        for (int k = 0; k < combLen.Length; k++) combBuf[k] = new float[combLen[k]];

        const int apLen = 421;
        const float apG = 0.5f;
        var apBuf = new float[apLen];
        int apPos = 0;

        float masterLp = 0f;
        float masterCoef = 1f - Mathf.Exp(-2f * Mathf.PI * masterLpHz / SampleRate);

        var outp = new float[n];
        for (int i = 0; i < n; i++)
        {
            float input = dry[i];

            float combSum = 0f;
            for (int k = 0; k < combLen.Length; k++)
            {
                float y = combBuf[k][combPos[k]];
                combStore[k] += damp * (y - combStore[k]);
                combBuf[k][combPos[k]] = input + combStore[k] * combFb;
                combPos[k] = (combPos[k] + 1) % combLen[k];
                combSum += y;
            }
            combSum /= combLen.Length;

            float bufout = apBuf[apPos];
            float apOut = -combSum + bufout;
            apBuf[apPos] = combSum + bufout * apG;
            apPos = (apPos + 1) % apLen;

            float mixed = dry[i] + apOut * wet;

            masterLp += masterCoef * (mixed - masterLp);
            outp[i] = Mathf.Clamp(masterLp, -0.95f, 0.95f);
        }
        return outp;
    }
}
