package com.sokol.speech;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.os.Bundle;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;
import android.speech.tts.Voice;

import java.util.Locale;
import java.util.Set;

/**
 * TextToSpeech helper for the sokol_speech plugin. Called from native code
 * (sokol_speech_android.c); progress flows back through nativeOnEvent into the
 * plugin's C event queue, drained by the app's game thread.
 *
 * "Available" means an OFFLINE voice for the language is installed now: the
 * engine's voices are enumerated and filtered on
 * {@code !isNetworkConnectionRequired()} and the absence of the
 * {@code notInstalled} feature, per the Voice API documentation. Speaking
 * selects that voice explicitly, so synthesis never touches the network.
 */
public final class SokolSpeech {

    /** Upcall into sokol_speech_android.c -- safe from any thread. */
    static native void nativeOnEvent(int type, int id, int code);

    /* Mirror of sokolspeech_event_type in sokol_speech.h. */
    static final int EV_READY   = 1;
    static final int EV_STARTED = 2;
    static final int EV_DONE    = 3;
    static final int EV_ERROR   = 4;

    static Activity activity;
    /** Created on the UI thread by init(); read from the game thread once ready. */
    static volatile TextToSpeech tts;
    static volatile boolean ready;
    static int nextId = 1;

    private SokolSpeech() {}

    public static void init(final Activity act) {
        activity = act;
        act.runOnUiThread(() -> {
            if (tts != null) return;
            tts = new TextToSpeech(act.getApplicationContext(), status -> {
                ready = status == TextToSpeech.SUCCESS;
                nativeOnEvent(EV_READY, 0, ready ? 0 : status);
            });
            tts.setOnUtteranceProgressListener(new UtteranceProgressListener() {
                @Override public void onStart(String utteranceId) { nativeOnEvent(EV_STARTED, parse(utteranceId), 0); }
                @Override public void onDone(String utteranceId)  { nativeOnEvent(EV_DONE, parse(utteranceId), 0); }
                @Override public void onStop(String utteranceId, boolean interrupted) { nativeOnEvent(EV_DONE, parse(utteranceId), 1); }
                @Override public void onError(String utteranceId, int errorCode) { nativeOnEvent(EV_ERROR, parse(utteranceId), errorCode); }
                @Override @SuppressWarnings("deprecation")
                public void onError(String utteranceId) { nativeOnEvent(EV_ERROR, parse(utteranceId), TextToSpeech.ERROR); }
            });
        });
    }

    static int parse(String utteranceId) {
        try { return Integer.parseInt(utteranceId); } catch (Exception e) { return 0; }
    }

    /** The best installed OFFLINE voice for {@code lang} (BCP-47; a bare language matches
        any region, a full tag prefers its own region), or null. */
    static Voice offlineVoice(String lang) {
        TextToSpeech t = tts;
        if (t == null || !ready || lang == null) return null;
        Locale want = Locale.forLanguageTag(lang.replace('_', '-'));
        String wantLang = want.getLanguage();
        String wantCountry = want.getCountry();
        Set<Voice> voices;
        try { voices = t.getVoices(); } catch (Exception e) { voices = null; }   // some engines throw here
        if (voices == null) return null;
        Voice best = null;
        int bestScore = -1;
        for (Voice v : voices) {
            if (v == null || v.getLocale() == null) continue;
            if (v.isNetworkConnectionRequired()) continue;
            Set<String> features = v.getFeatures();
            if (features != null && features.contains(TextToSpeech.Engine.KEY_FEATURE_NOT_INSTALLED)) continue;
            if (!wantLang.equals(v.getLocale().getLanguage())) continue;
            int score = 0;
            if (!wantCountry.isEmpty() && wantCountry.equals(v.getLocale().getCountry())) score += 100;
            score += v.getQuality();          // QUALITY_VERY_LOW (100) .. QUALITY_VERY_HIGH (500)
            if (score > bestScore) { bestScore = score; best = v; }
        }
        return best;
    }

    public static boolean available(String lang) {
        return offlineVoice(lang) != null;
    }

    public static int say(String text, String lang) {
        TextToSpeech t = tts;
        Voice v = offlineVoice(lang);
        if (t == null || v == null || text == null) return 0;
        int id = nextId++;
        try {
            t.setVoice(v);
            Bundle params = new Bundle();
            params.putString(TextToSpeech.Engine.KEY_PARAM_UTTERANCE_ID, String.valueOf(id));
            int r = t.speak(text, TextToSpeech.QUEUE_FLUSH, params, String.valueOf(id));
            if (r != TextToSpeech.SUCCESS) { nativeOnEvent(EV_ERROR, id, r); }
        } catch (Exception e) {
            nativeOnEvent(EV_ERROR, id, TextToSpeech.ERROR);
        }
        return id;
    }

    public static void stop() {
        TextToSpeech t = tts;
        if (t != null) { try { t.stop(); } catch (Exception ignored) {} }
    }

    /** The engine's voice-data installer (ACTION_INSTALL_TTS_DATA); falls back to the
        system TTS settings page when no engine handles the installer intent. */
    public static void openVoiceSettings() {
        Activity a = activity;
        if (a == null) return;
        a.runOnUiThread(() -> {
            Intent install = new Intent(TextToSpeech.Engine.ACTION_INSTALL_TTS_DATA);
            install.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            try { a.startActivity(install); return; } catch (ActivityNotFoundException ignored) {}
            Intent settings = new Intent("com.android.settings.TTS_SETTINGS");
            settings.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            try { a.startActivity(settings); } catch (ActivityNotFoundException ignored) {}
        });
    }

    public static void shutdown() {
        TextToSpeech t = tts;
        tts = null;
        ready = false;
        if (t != null) { try { t.stop(); t.shutdown(); } catch (Exception ignored) {} }
    }
}
