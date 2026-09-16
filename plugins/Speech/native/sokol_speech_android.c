/* sokol_speech_android.c -- android.speech.tts.TextToSpeech bridge for Sokol.NET.
   The engine work lives in the Java helper com.sokol.speech.SokolSpeech
   (platform/android/java/, wired via AndroidJavaSource_sokolspeechPath); this
   file forwards calls to it and receives events via nativeOnEvent.
   Requires:
     - sapp_android_get_native_activity() exported from sokol_app.h
     - the <queries> fragment in platform/android/manifest/Queries.xml (Android 11+
       package visibility: without it the app cannot bind the TTS engine).
*/
#include "sokol_speech.h"
#include <android/native_activity.h>
#include <jni.h>
#include <stddef.h>

/* Already declared in sokol_app.h; returns ANativeActivity* as const void*. */
extern const void* sapp_android_get_native_activity(void);

/* sokol_speech_queue.c */
extern void sokolspeech__emit(int type, int id, int code);

static jclass    _ss_class;        /* global ref to com.sokol.speech.SokolSpeech */
static jmethodID _ss_init;
static jmethodID _ss_available;
static jmethodID _ss_say;
static jmethodID _ss_stop;
static jmethodID _ss_open_settings;
static jmethodID _ss_shutdown;

static JNIEnv* _ss_env(void)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    if (!activity) return NULL;
    JNIEnv* env = NULL;
    (*activity->vm)->AttachCurrentThread(activity->vm, &env, NULL);
    /* Do NOT DetachCurrentThread -- Sokol reuses this thread across frames. */
    return env;
}

/* Load the helper class through the activity's ClassLoader (FindClass from a
   native thread uses the bootstrap loader and cannot see app classes). */
static bool _ss_resolve(JNIEnv* env, ANativeActivity* activity)
{
    if (_ss_class) return true;

    jclass    clsLdrCls = (*env)->FindClass(env, "java/lang/ClassLoader");
    jmethodID loadClass = (*env)->GetMethodID(env, clsLdrCls, "loadClass",
                              "(Ljava/lang/String;)Ljava/lang/Class;");
    jclass    actCls    = (*env)->GetObjectClass(env, activity->clazz);
    jmethodID getClsLdr = (*env)->GetMethodID(env, actCls, "getClassLoader",
                              "()Ljava/lang/ClassLoader;");
    jobject   loader    = (*env)->CallObjectMethod(env, activity->clazz, getClsLdr);
    jstring   name      = (*env)->NewStringUTF(env, "com.sokol.speech.SokolSpeech");
    jclass    cls       = (jclass)(*env)->CallObjectMethod(env, loader, loadClass, name);

    (*env)->DeleteLocalRef(env, name);
    (*env)->DeleteLocalRef(env, loader);
    (*env)->DeleteLocalRef(env, actCls);
    (*env)->DeleteLocalRef(env, clsLdrCls);

    if (!cls || (*env)->ExceptionCheck(env)) {
        (*env)->ExceptionClear(env);
        return false;
    }

    _ss_class         = (jclass)(*env)->NewGlobalRef(env, cls);
    _ss_init          = (*env)->GetStaticMethodID(env, _ss_class, "init", "(Landroid/app/Activity;)V");
    _ss_available     = (*env)->GetStaticMethodID(env, _ss_class, "available", "(Ljava/lang/String;)Z");
    _ss_say           = (*env)->GetStaticMethodID(env, _ss_class, "say", "(Ljava/lang/String;Ljava/lang/String;)I");
    _ss_stop          = (*env)->GetStaticMethodID(env, _ss_class, "stop", "()V");
    _ss_open_settings = (*env)->GetStaticMethodID(env, _ss_class, "openVoiceSettings", "()V");
    _ss_shutdown      = (*env)->GetStaticMethodID(env, _ss_class, "shutdown", "()V");
    (*env)->DeleteLocalRef(env, cls);
    return true;
}

void sokolspeech_init(void)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    JNIEnv* env = _ss_env();
    if (!activity || !env || !_ss_resolve(env, activity)) {
        sokolspeech__emit(SOKOLSPEECH_EVENT_READY, 0, -1);
        return;
    }
    (*env)->CallStaticVoidMethod(env, _ss_class, _ss_init, activity->clazz);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

bool sokolspeech_available(const char* lang)
{
    if (!_ss_class || !lang) return false;
    JNIEnv* env = _ss_env();
    if (!env) return false;
    jstring jlang = (*env)->NewStringUTF(env, lang);
    jboolean ok = (*env)->CallStaticBooleanMethod(env, _ss_class, _ss_available, jlang);
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); ok = JNI_FALSE; }
    (*env)->DeleteLocalRef(env, jlang);
    return ok == JNI_TRUE;
}

int sokolspeech_say(const char* text, const char* lang)
{
    if (!_ss_class || !text || !lang) return 0;
    JNIEnv* env = _ss_env();
    if (!env) return 0;
    jstring jtext = (*env)->NewStringUTF(env, text);
    jstring jlang = (*env)->NewStringUTF(env, lang);
    jint id = (*env)->CallStaticIntMethod(env, _ss_class, _ss_say, jtext, jlang);
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); id = 0; }
    (*env)->DeleteLocalRef(env, jtext);
    (*env)->DeleteLocalRef(env, jlang);
    return (int)id;
}

static void _ss_call_void(jmethodID method)
{
    if (!_ss_class || !method) return;
    JNIEnv* env = _ss_env();
    if (!env) return;
    (*env)->CallStaticVoidMethod(env, _ss_class, method);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

void sokolspeech_stop(void)                { _ss_call_void(_ss_stop); }
void sokolspeech_open_voice_settings(void) { _ss_call_void(_ss_open_settings); }
void sokolspeech_shutdown(void)            { _ss_call_void(_ss_shutdown); }

/* Upcall from com.sokol.speech.SokolSpeech (any Java thread). */
JNIEXPORT void JNICALL
Java_com_sokol_speech_SokolSpeech_nativeOnEvent(JNIEnv* env, jclass cls, jint type, jint id, jint code)
{
    (void)env; (void)cls;
    sokolspeech__emit((int)type, (int)id, (int)code);
}
