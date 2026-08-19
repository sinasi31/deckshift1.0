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

    private static AudioClip meteorImpact;

    // Meteor Greaves landing. A heavy stone impact, and deliberately a THIRD sound family from the
    // two already here: magic is harmonic (bell partials), metal is inharmonic-but-pitched (bar
    // modes), and this is barely pitched at all — mostly noise and sub. Stone shatters; it does not
    // ring. Keeping the families distinct is what stops a boulder landing and a scrap pickup
    // reading as the same event.
    public static AudioClip MeteorImpact
    {
        get { if (meteorImpact == null) meteorImpact = BuildMeteorImpact(); return meteorImpact; }
    }

    // Four layers, because a big impact is a sequence and not a single hit:
    //   CRACK   ~8 ms of bright noise — the fracture. Without this the hit has no attack and
    //           reads as distant rather than underfoot.
    //   SUB     a sine swept 110 -> 34 Hz. The downward sweep is what makes it feel like MASS
    //           arriving; a static low sine just sounds like a hum.
    //   BODY    three inharmonic low partials for the stone slab itself.
    //   DEBRIS  band-passed noise with a slow decay and a little flutter — rubble settling after,
    //           which is what sells the scale and stops the sound ending abruptly.
    private static AudioClip BuildMeteorImpact()
    {
        const float dur = 1.15f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(90210);

        // Body: inharmonic, low, and fast-decaying — a struck slab, not a bell.
        float b0 = 132f;
        float[] ratio = { 1f, 1.71f, 2.43f };
        float[] pAmp = { 1f, 0.42f, 0.20f };
        float[] pDec = { 11f, 17f, 26f };

        float crackLp = 0f;
        float crackCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 7000f / SampleRate);

        // Two one-pole stages in series make a crude band-pass for the debris.
        float debLp = 0f, debHp = 0f;
        float debLpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 1800f / SampleRate);
        float debHpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 260f / SampleRate);

        float subPhase = 0f;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            // CRACK
            crackLp += crackCoef * (noise - crackLp);
            float crack = crackLp * Mathf.Exp(-320f * ts);

            // SUB — sweep the frequency, integrating phase so there are no discontinuities.
            float subF = Mathf.Lerp(110f, 34f, Mathf.Clamp01(ts / 0.30f));
            subPhase += 2f * Mathf.PI * subF / SampleRate;
            float sub = Mathf.Sin(subPhase) * Mathf.Exp(-5.5f * ts);

            // BODY
            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * b0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            // DEBRIS — rubble skittering, gated so it starts just after the hit.
            debLp += debLpCoef * (noise - debLp);
            debHp += debHpCoef * (debLp - debHp);
            float band = debLp - debHp;
            float flutter = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 27f * ts);
            float debrisEnv = Mathf.Exp(-4.2f * ts) * Mathf.Clamp01(ts / 0.02f);
            float debris = band * debrisEnv * flutter;

            dry[i] = crack * 0.30f + sub * 0.52f + body * 0.16f + debris * 0.22f;
        }

        return Finalize(dry, 0.26f, 5200f);   // wet and dark — a big room, heard from inside it
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
    private static AudioClip pauseHalt, pauseRelease, pauseTick;

    // Opening the pause screen: the moment everything stops.
    public static AudioClip PauseHalt
    {
        get { if (pauseHalt == null) pauseHalt = BuildPauseHalt(); return pauseHalt; }
    }

    // Closing it: the clock let go.
    public static AudioClip PauseRelease
    {
        get { if (pauseRelease == null) pauseRelease = BuildPauseRelease(); return pauseRelease; }
    }

    // Moving the selection. Tiny, dry, and quiet enough to hold a key down through.
    public static AudioClip PauseTick
    {
        get { if (pauseTick == null) pauseTick = BuildPauseTick(); return pauseTick; }
    }

    // A FOURTH sound family, kept distinct from the three already here on purpose: magic is
    // harmonic (bell partials), metal is inharmonic-but-pitched (bar modes), stone is barely
    // pitched at all. This one is defined by what happens to its ENVELOPE rather than its
    // spectrum — it is the only sound in the game that gets CHOKED.
    //
    // Three beats, and the middle one is the whole idea:
    //   INHALE  ~130ms of noise whose band-pass climbs. Reads as something winding up.
    //   STOP    a struck glass cluster plus a low sub, on the beat the inhale cuts dead.
    //   CHOKE   the ring is damped away over ~180ms instead of being allowed to decay naturally.
    //           A sound that fades out says "ending"; a sound that is cut short says "held".
    private static AudioClip BuildPauseHalt()
    {
        const float dur = 1.10f;
        const float hit = 0.13f;                 // when the inhale stops and the strike lands
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(9042);

        float svfLow = 0f, svfBand = 0f;

        // Cold glass, not a warm bell: a stretched, slightly inharmonic cluster.
        float f0 = 494f;                                    // B4
        float[] ratio = { 1f, 2.02f, 3.09f, 4.21f, 6.05f };
        float[] pAmp = { 1f, 0.46f, 0.26f, 0.14f, 0.07f };

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            // INHALE — band-passed noise climbing 300 -> 3400 Hz, silenced the instant the hit lands.
            float inhale = 0f;
            if (ts < hit)
            {
                float t01 = ts / hit;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float cutoff = Mathf.Lerp(300f, 3400f, t01 * t01);
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(cutoff, SampleRate * 0.45f) / SampleRate);
                svfLow += f * svfBand;
                float high = noise - svfLow - 0.30f * svfBand;
                svfBand += f * high;
                inhale = svfBand * Mathf.Pow(t01, 1.8f) * 0.16f;
            }

            float st = ts - hit;
            if (st < 0f) { dry[i] = inhale; continue; }

            // THE CHOKE. Two envelopes multiplied: the partials' own decay, and a damper that
            // clamps down over 180ms and holds everything at a trickle afterwards. Without the
            // damper this is just a chime and reads as a menu ping.
            float damp = Mathf.Lerp(1f, 0.06f, Mathf.Clamp01(st / 0.18f));

            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * f0 * ratio[p] * st) * pAmp[p] * Mathf.Exp(-6f * st);

            // Sub thump under the strike — the weight of the thing coming to rest.
            float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(96f, 58f, Mathf.Clamp01(st / 0.25f)) * st)
                      * Mathf.Exp(-9f * st);

            dry[i] = inhale + body * damp * 0.105f + sub * 0.075f;
        }

        return Finalize(dry, 0.20f, 7000f);
    }

    // The inverse: short, and it OPENS. The halt's ring is choked; this one is released and allowed
    // to run out on its own, which is what makes the pair read as a lid closing and lifting.
    private static AudioClip BuildPauseRelease()
    {
        const float dur = 0.55f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(3311);

        float lp = 0f;
        double ph = 0.0;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float t01 = ts / dur;

            // A breath opening outward: noise whose low-pass sweeps UP, brief.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(700f, 5200f, Mathf.Clamp01(t01 / 0.35f));
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate)) * (noise - lp);
            float air = lp * Mathf.Exp(-11f * ts) * 0.13f;

            // A glass partial rising a fourth. Phase is integrated so the glide doesn't tear.
            float g = Mathf.Lerp(494f, 659f, Mathf.Clamp01(t01 / 0.4f));
            ph += 2.0 * Mathf.PI * g / SampleRate;
            float tone = (float)System.Math.Sin(ph) * Mathf.Exp(-7f * ts) * 0.075f;

            dry[i] = air + tone;
        }

        return Finalize(dry, 0.14f, 8000f);
    }

    // Selection tick. 35ms, no reverb, no pitch to speak of — a fingernail on glass. Anything with
    // a discernible NOTE turns a held arrow key into a melody.
    private static AudioClip BuildPauseTick()
    {
        const float dur = 0.035f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(1187);

        float hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            // One-pole high-pass, so the click has no body at all.
            hp = 0.92f * (hp + noise - prev);
            prev = noise;

            dry[i] = (hp * 0.5f + Mathf.Sin(2f * Mathf.PI * 2300f * ts) * 0.35f) * Mathf.Exp(-180f * ts) * 0.22f;
        }

        return Finalize(dry, 0f, 11000f);
    }

    private static AudioClip paperRustle, waxStamp;

    // Opening the quest board: a sheaf of pinned paper disturbed by the door.
    public static AudioClip PaperRustle
    {
        get { if (paperRustle == null) paperRustle = BuildPaperRustle(); return paperRustle; }
    }

    // Accepting a contract: a seal pressed into wax.
    public static AudioClip WaxStamp
    {
        get { if (waxStamp == null) waxStamp = BuildWaxStamp(); return waxStamp; }
    }

    // These two are the only sounds in the game with NO PITCHED COMPONENT AT ALL, and that is what
    // makes them a distinct family rather than a variation on the stone hits. Magic is harmonic,
    // metal is inharmonic-but-pitched, stone is barely pitched, the pause pair is defined by its
    // envelope — paper simply has no note in it. Give either of these a tone and it stops being
    // paper immediately.
    //
    // A rustle is a cluster of short noise bursts, not one long one: a continuous shaped hiss reads
    // as wind, and only the granularity says "many separate sheets".
    private static AudioClip BuildPaperRustle()
    {
        const float dur = 0.62f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5531);

        const int bursts = 14;
        var at = new float[bursts];
        var decay = new float[bursts];
        var amp = new float[bursts];
        var hz = new float[bursts];
        for (int k = 0; k < bursts; k++)
        {
            // Front-loaded: the disturbance settles rather than building.
            float u = (float)rng.NextDouble();
            at[k] = u * u * dur * 0.85f;
            decay[k] = 34f + (float)rng.NextDouble() * 46f;
            amp[k] = 0.5f + (float)rng.NextDouble() * 0.5f;
            hz[k] = 2200f + (float)rng.NextDouble() * 4200f;
        }

        float svfLow = 0f, svfBand = 0f;
        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            float env = 0f;
            float centre = 0f, wsum = 0f;
            for (int k = 0; k < bursts; k++)
            {
                float bt = ts - at[k];
                if (bt <= 0f) continue;
                float e = Mathf.Exp(-decay[k] * bt) * amp[k];
                env += e;
                centre += hz[k] * e; wsum += e;
            }
            if (env <= 0.0001f) { dry[i] = 0f; continue; }
            centre = wsum > 0f ? centre / wsum : 3600f;

            // Resonant band-pass following whichever burst is loudest right now.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(centre, SampleRate * 0.45f) / SampleRate);
            svfLow += f * svfBand;
            float high = noise - svfLow - 0.55f * svfBand;   // low Q: paper is broad, not whistly
            svfBand += f * high;

            // Global taper so the last bursts don't end abruptly.
            dry[i] = svfBand * Mathf.Min(env, 1.4f) * (1f - Mathf.Clamp01(ts / dur)) * 0.085f;
        }

        return Finalize(dry, 0.08f, 12000f);   // nearly dry — a board is against a wall, not in a hall
    }

    // The press. Three layers, and the ORDER of their decays is what sells it as one physical
    // action rather than three sounds: the wax gives way first (a fast dull squash), the seal
    // bottoms out on the paper underneath (a low thock), and the sheet itself creases last.
    private static AudioClip BuildWaxStamp()
    {
        const float dur = 0.42f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(8264);

        // Crease ticks: a handful of tiny snaps in the first 140ms.
        const int ticks = 6;
        var tickAt = new float[ticks];
        var tickHz = new float[ticks];
        for (int k = 0; k < ticks; k++)
        {
            tickAt[k] = 0.012f + (float)rng.NextDouble() * 0.13f;
            tickHz[k] = 3400f + (float)rng.NextDouble() * 3800f;
        }

        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            // SQUASH — noise through a low-pass that closes fast. Soft material displacing.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(2600f, 380f, Mathf.Clamp01(ts / 0.05f));
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate)) * (noise - lp);
            float squash = lp * Mathf.Exp(-28f * ts) * 0.30f;

            // THOCK — the seal reaching the desk through the paper. Pitch drops as it settles, but
            // it stays under 90 Hz where it reads as weight rather than as a note.
            float thock = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(88f, 52f, Mathf.Clamp01(ts / 0.12f)) * ts)
                        * Mathf.Exp(-19f * ts) * 0.16f;

            // CREASE — paper snapping under the pressure.
            float crease = 0f;
            for (int k = 0; k < ticks; k++)
            {
                float t2 = ts - tickAt[k];
                if (t2 <= 0f) continue;
                crease += Mathf.Sin(2f * Mathf.PI * tickHz[k] * t2) * Mathf.Exp(-150f * t2);
            }

            dry[i] = squash + thock + crease * 0.022f;
        }

        return Finalize(dry, 0.12f, 9000f);
    }

    // =============================================================================================
    // GATE — heavy banded doors hung in a stone arch.
    //
    // A FIFTH family, and deliberately the only one built from TWO materials at once. Every family
    // above commits to one (magic = harmonic bell partials, metal = inharmonic bar modes, stone =
    // noise + sub, paper = no pitched component at all, UI = pitch motion). A barred door in a stone
    // opening is iron working against masonry, so these layer bar modes OVER grit — which is what
    // stops a hinge creak reading as a scrap pickup and the stop reading as a Meteor Greaves landing.
    //
    // FOUR clips, because a gate opening is a SEQUENCE and not a hit. The old gate played nothing at
    // all, and that silence was most of why it felt like nothing was happening.
    //
    // ⚠️ These were written for a PORTCULLIS (strain → catch → ratchet down → seat) and are now
    // sequenced by Gate.cs as a DOUBLE DOOR (bolt → strain → swing → stop, and a slam on the way
    // back). The clips still fit — the materials did not change, only the choreography — so the
    // names below describe the sound rather than the beat it happens to serve. Do not assume
    // "Ratchet" means the gate ratchets; it is the dry repeatable tick, used as a hinge creak.

    private static AudioClip gateGroan, gateRelease, gateRatchet, gateSeat;

    // The mechanism taking the weight before anything moves. Deliberately has NO attack transient —
    // it swells. A sound that arrives gradually is what makes the release that follows land.
    public static AudioClip GateGroan
    { get { if (gateGroan == null) gateGroan = BuildGateGroan(); return gateGroan; } }

    // The catch letting go: the one sharp event in the whole sequence, so nothing else may compete.
    public static AudioClip GateRelease
    { get { if (gateRelease == null) gateRelease = BuildGateRelease(); return gateRelease; } }

    // One pawl catch during the descent. Played a dozen times per open, so it is deliberately the
    // quietest and driest clip in the game — give this a tail and the drop turns to mush.
    public static AudioClip GateRatchet
    { get { if (gateRatchet == null) gateRatchet = BuildGateRatchet(); return gateRatchet; } }

    // The slab arriving. Lower and longer than MeteorImpact on purpose: that is a body hitting the
    // floor, this is the floor taking a tonne of rock.
    public static AudioClip GateSeat
    { get { if (gateSeat == null) gateSeat = BuildGateSeat(); return gateSeat; } }

    private static AudioClip BuildGateGroan()
    {
        const float dur = 0.55f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(20819);

        float lp = 0f, hp = 0f;
        float lpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 520f / SampleRate);
        float hpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 150f / SampleRate);
        float subPhase = 0f;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            // Swell in and ease out. No transient anywhere — this is load ARRIVING, not an impact.
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(ts / dur));
            env *= env;

            // Stone bearing on stone: a narrow noise band with a slow wobble, as the faces bind.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += lpCoef * (noise - lp);
            hp += hpCoef * (lp - hp);
            float grit = (lp - hp) * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 11f * ts));

            subPhase += 2f * Mathf.PI * 52f / SampleRate;   // the weight itself
            float sub = Mathf.Sin(subPhase);

            dry[i] = (grit * 0.42f + sub * 0.30f) * env;
        }
        return Finalize(dry, 0.20f, 3200f);   // dark: heard through rock, not through air
    }

    private static AudioClip BuildGateRelease()
    {
        const float dur = 0.34f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(60313);

        float f0 = 168f;                                   // heavy iron => LOW fundamental
        float[] ratio = { 1f, 2.76f, 5.40f, 8.93f };       // ideal free-bar modes = metal
        float[] pAmp  = { 1f, 0.48f, 0.22f, 0.09f };
        float[] pDec  = { 15f, 24f, 36f, 52f };

        float lp = 0f;
        float lpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 4200f / SampleRate);
        float subPhase = 0f;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * f0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            lp += lpCoef * (noise - lp);
            float grit = lp * Mathf.Exp(-70f * ts);

            subPhase += 2f * Mathf.PI * 64f / SampleRate;
            float sub = Mathf.Sin(subPhase) * Mathf.Exp(-13f * ts);

            dry[i] = body * 0.20f + grit * 0.26f + sub * 0.30f;
        }
        return Finalize(dry, 0.18f, 6000f);
    }

    private static AudioClip BuildGateRatchet()
    {
        const float dur = 0.13f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(11279);

        float f0 = 315f;
        float[] ratio = { 1f, 2.76f, 5.40f };
        float[] pAmp  = { 1f, 0.40f, 0.16f };
        float[] pDec  = { 46f, 62f, 84f };                 // very fast: a tick, never a ring

        float lp = 0f;
        float lpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 3400f / SampleRate);

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * f0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            lp += lpCoef * (noise - lp);
            float grit = lp * Mathf.Exp(-150f * ts);

            dry[i] = body * 0.11f + grit * 0.16f;
        }
        return Finalize(dry, 0.05f, 7000f);   // almost dry — a dozen of these must not smear
    }

    private static AudioClip BuildGateSeat()
    {
        const float dur = 1.30f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(77404);

        float b0 = 96f;                                    // below MeteorImpact 132 = more mass
        float[] ratio = { 1f, 1.71f, 2.43f };
        float[] pAmp  = { 1f, 0.38f, 0.17f };
        float[] pDec  = { 8f, 13f, 20f };

        float crackLp = 0f;
        float crackCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 6200f / SampleRate);
        float debLp = 0f, debHp = 0f;
        float debLpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 1500f / SampleRate);
        float debHpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 220f / SampleRate);
        float subPhase = 0f;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            crackLp += crackCoef * (noise - crackLp);
            float crack = crackLp * Mathf.Exp(-260f * ts);

            float subF = Mathf.Lerp(92f, 30f, Mathf.Clamp01(ts / 0.36f));
            subPhase += 2f * Mathf.PI * subF / SampleRate;
            float sub = Mathf.Sin(subPhase) * Mathf.Exp(-4.2f * ts);

            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * b0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            debLp += debLpCoef * (noise - debLp);
            debHp += debHpCoef * (debLp - debHp);
            float band = debLp - debHp;
            float debris = band * Mathf.Exp(-3.4f * ts) * Mathf.Clamp01(ts / 0.025f)
                         * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 19f * ts));

            dry[i] = crack * 0.22f + sub * 0.58f + body * 0.20f + debris * 0.18f;
        }
        return Finalize(dry, 0.30f, 4600f);   // wet and dark — a big room, heard from inside it
    }

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
    // =============================================================================================
    // UI — the interface's own voice.
    //
    // THE FAMILY RULE, and it is a different KIND of rule from the others. Every family above is
    // defined by a MATERIAL: magic by harmonic bell partials, metal by inharmonic bar modes, stone
    // by noise and sub, paper by having no pitched component at all, the pause pair by a choked
    // envelope. A UI sound has no material — it is not a thing in the world, it is the interface.
    //
    // So this family is defined by PITCH MOTION instead. All six share ONE voice, literally the
    // same `WoodTap` call, and differ only in which way the pitch moves and by how much. That is
    // what makes them a learnable language rather than six noises — and it is the right mechanism
    // for the job, because these are the only sounds in the game that must be told apart FROM EACH
    // OTHER. A world sound only has to be distinguishable from other materials.
    //
    // ⚠️ THE VOICE IS SOFT STRUCK WOOD, and that is a deliberate claim on the one material the
    // world does not already use. Metal is the forge, glass and bells are magic, stone is the
    // rooms, paper is the quest board. Wood is unclaimed, warm, and belongs in a candlelit
    // dungeon — where a clean synth blip would sound like it came from a different game.
    //
    // ⚠️ THEY MUST BE SMALL AND DRY. These play hundreds of times a session. Anything with shimmer
    // or a long tail becomes torture by minute ten, which is why the reverb here is the driest in
    // the file and the decays are the shortest.
    //
    // ⚠️ CANCEL AND REFUSE ARE NOT THE SAME SOUND, and conflating them is the usual mistake.
    // CANCEL is the player choosing to back out — consonant, unremarkable, no fault implied.
    // REFUSE is the game saying no — the only DISSONANT sound in the family. And refuse must not
    // read as damage or failure either; it means "you can't do that", not "you got hurt".
    //
    // ⚠️ OPEN AND CLOSE ARE THE SAME FIGURE INVERTED — the identical three notes, backwards. Two
    // unrelated sounds would not read as a pair, and the pairing is what tells the player that the
    // thing that arrived is the thing that just left.

    private static AudioClip uiMove, uiConfirm, uiCancel, uiRefuse, uiOpen, uiClose;

    /// <summary>Moving a selection. Neutral, no pitch motion, quiet enough to hold a key through.</summary>
    public static AudioClip UIMove
    {
        get { if (uiMove == null) uiMove = BuildUIMove(); return uiMove; }
    }

    /// <summary>Committing. Rising perfect fifth — the most consonant way up, so it reads as resolution.</summary>
    public static AudioClip UIConfirm
    {
        get { if (uiConfirm == null) uiConfirm = BuildUIConfirm(); return uiConfirm; }
    }

    /// <summary>Backing out by choice. Falling fourth: downward, but consonant — this is not an error.</summary>
    public static AudioClip UICancel
    {
        get { if (uiCancel == null) uiCancel = BuildUICancel(); return uiCancel; }
    }

    /// <summary>The game saying no. A minor second sounded together, damped hard. The only dissonance here.</summary>
    public static AudioClip UIRefuse
    {
        get { if (uiRefuse == null) uiRefuse = BuildUIRefuse(); return uiRefuse; }
    }

    /// <summary>A panel arriving. Three notes up.</summary>
    public static AudioClip UIOpen
    {
        get { if (uiOpen == null) uiOpen = BuildUIOpen(); return uiOpen; }
    }

    /// <summary>The same three notes, backwards.</summary>
    public static AudioClip UIClose
    {
        get { if (uiClose == null) uiClose = BuildUIClose(); return uiClose; }
    }

    // The one voice. Every UI sound in the family is this function and nothing else, so the family
    // physically cannot drift apart the way a set of hand-tuned one-offs would.
    //
    // Struck wood is INHARMONIC but only mildly so — far less than metal's bar modes, which is what
    // keeps it reading as a soft tap rather than a clank. The tiny noise transient is the mallet
    // contact; without it the tone starts from nothing and sounds synthesised rather than struck.
    private static void WoodTap(float[] buf, float atSeconds, float hz, float amp, float decay,
                                System.Random rng)
    {
        int start = Mathf.RoundToInt(atSeconds * SampleRate);
        if (start >= buf.Length) return;

        // Mild inharmonic partials — a struck wooden bar, not a metal one.
        float[] ratio = { 1f, 2.83f, 4.94f };
        float[] gain  = { 1f, 0.26f, 0.09f };

        for (int i = start; i < buf.Length; i++)
        {
            float t = (float)(i - start) / SampleRate;
            float env = Mathf.Exp(-decay * t);
            if (env < 0.0008f) break;

            float body = 0f;
            for (int k = 0; k < ratio.Length; k++)
            {
                // Higher partials die faster, which is most of what makes a tap sound wooden.
                body += Mathf.Sin(2f * Mathf.PI * hz * ratio[k] * t) * gain[k] * Mathf.Exp(-decay * 1.9f * k * t);
            }

            // Mallet contact: a couple of milliseconds of noise, gone almost immediately.
            float tap = 0f;
            if (t < 0.004f)
                tap = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.35f * (1f - t / 0.004f);

            buf[i] += (body * 0.42f + tap) * env * amp;
        }
    }

    private static AudioClip BuildUIMove()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.13f)];
        var rng = new System.Random(9101);
        // Deliberately the quietest sound in the game. It fires on every arrow key.
        WoodTap(dry, 0f, 700f, 0.085f, 58f, rng);
        return Finalize(dry, 0.055f, 6200f);
    }

    private static AudioClip BuildUIConfirm()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.30f)];
        var rng = new System.Random(9102);
        WoodTap(dry, 0f,     620f, 0.155f, 34f, rng);
        WoodTap(dry, 0.062f, 930f, 0.150f, 30f, rng);   // x1.5 — perfect fifth up
        return Finalize(dry, 0.085f, 6800f);
    }

    private static AudioClip BuildUICancel()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.30f)];
        var rng = new System.Random(9103);
        WoodTap(dry, 0f,     780f, 0.145f, 34f, rng);
        WoodTap(dry, 0.062f, 585f, 0.140f, 30f, rng);   // x0.75 — perfect fourth down
        return Finalize(dry, 0.085f, 6200f);
    }

    private static AudioClip BuildUIRefuse()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.24f)];
        var rng = new System.Random(9104);
        // Sounded TOGETHER, not in sequence: a beating minor second is the dissonance, and playing
        // the two notes one after the other would just read as another little melody.
        WoodTap(dry, 0f,      600f, 0.135f, 46f, rng);
        WoodTap(dry, 0.006f,  636f, 0.130f, 46f, rng);  // ~x1.06 — minor second
        return Finalize(dry, 0.05f, 5200f);             // driest and dullest: it should not ring
    }

    private static AudioClip BuildUIOpen()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.42f)];
        var rng = new System.Random(9105);
        WoodTap(dry, 0f,     520f, 0.120f, 30f, rng);
        WoodTap(dry, 0.055f, 693f, 0.125f, 28f, rng);
        WoodTap(dry, 0.110f, 780f, 0.130f, 24f, rng);
        return Finalize(dry, 0.10f, 7000f);
    }

    private static AudioClip BuildUIClose()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.42f)];
        var rng = new System.Random(9106);
        // The identical three pitches of UIOpen, in reverse.
        WoodTap(dry, 0f,     780f, 0.125f, 30f, rng);
        WoodTap(dry, 0.055f, 693f, 0.120f, 28f, rng);
        WoodTap(dry, 0.110f, 520f, 0.118f, 24f, rng);
        return Finalize(dry, 0.10f, 6400f);
    }

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
