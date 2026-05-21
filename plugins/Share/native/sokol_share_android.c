/* sokol_share_android.c -- Android share sheet implementation for Sokol.NET.
   Uses JNI + androidx.core.content.FileProvider for content:// URI (API 24+).
   Requires:
     - sapp_android_get_native_activity() exported from sokol_app.h
     - androidx.core:core:1.12.0 in app/build.gradle
     - FileProvider <provider> in AndroidManifest.xml
     - file_provider_paths.xml in res/xml/
*/
#include "sokol_share.h"
#include <android/native_activity.h>
#include <jni.h>
#include <stdio.h>
#include <string.h>

/* Already declared in sokol_app.h; returns ANativeActivity* as const void*. */
extern const void* sapp_android_get_native_activity(void);

void sokolshare_image_text(const char* image_path, const char* text)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    if (!activity) return;

    JavaVM* vm  = activity->vm;
    JNIEnv* env = NULL;
    (*vm)->AttachCurrentThread(vm, &env, NULL);
    if (!env) return;

    jstring jtext = (*env)->NewStringUTF(env, text       ? text       : "");
    jstring jpath = (*env)->NewStringUTF(env, image_path ? image_path : "");

    /* ── Resolve package name → FileProvider authority ─────────────────── */
    jclass    ctxClass = (*env)->FindClass(env, "android/content/Context");
    jmethodID getPkg   = (*env)->GetMethodID(env, ctxClass,
                             "getPackageName", "()Ljava/lang/String;");
    jstring   jpkg     = (jstring)(*env)->CallObjectMethod(env, activity->clazz, getPkg);

    const char* pkgStr = (*env)->GetStringUTFChars(env, jpkg, NULL);
    char authority[256];
    snprintf(authority, sizeof(authority), "%s.fileprovider", pkgStr);
    (*env)->ReleaseStringUTFChars(env, jpkg, pkgStr);
    jstring jauthority = (*env)->NewStringUTF(env, authority);

    /* ── Build content:// URI via FileProvider ──────────────────────────── */
    /* NOTE: FindClass() from a native thread uses the bootstrap class loader
       and cannot see app classes (e.g. androidx.*). We must load them through
       the activity's own ClassLoader, which has the full APK class path. */
    jobject uri = NULL;
    if (image_path && image_path[0])
    {
        jclass    fileClass  = (*env)->FindClass(env, "java/io/File");
        jmethodID fileInit   = (*env)->GetMethodID(env, fileClass,
                                   "<init>", "(Ljava/lang/String;)V");
        jobject   fileObj    = (*env)->NewObject(env, fileClass, fileInit, jpath);

        /* Load FileProvider via app ClassLoader (dots, not slashes). */
        jclass    clsLdrCls  = (*env)->FindClass(env, "java/lang/ClassLoader");
        jmethodID loadClass  = (*env)->GetMethodID(env, clsLdrCls, "loadClass",
                                   "(Ljava/lang/String;)Ljava/lang/Class;");
        jclass    actCls2    = (*env)->GetObjectClass(env, activity->clazz);
        jmethodID getClsLdr  = (*env)->GetMethodID(env, actCls2, "getClassLoader",
                                   "()Ljava/lang/ClassLoader;");
        jobject   loader     = (*env)->CallObjectMethod(env, activity->clazz, getClsLdr);
        jstring   fpName     = (*env)->NewStringUTF(env,
                                   "androidx.core.content.FileProvider");
        jclass    fpClass    = (jclass)(*env)->CallObjectMethod(env, loader,
                                   loadClass, fpName);

        jmethodID getUri     = (*env)->GetStaticMethodID(env, fpClass, "getUriForFile",
            "(Landroid/content/Context;Ljava/lang/String;Ljava/io/File;)"
            "Landroid/net/Uri;");
        uri = (*env)->CallStaticObjectMethod(env, fpClass, getUri,
                  activity->clazz, jauthority, fileObj);

        (*env)->DeleteLocalRef(env, fpName);
        (*env)->DeleteLocalRef(env, loader);
        (*env)->DeleteLocalRef(env, actCls2);
        (*env)->DeleteLocalRef(env, clsLdrCls);
        (*env)->DeleteLocalRef(env, fileObj);
        (*env)->DeleteLocalRef(env, fileClass);
        (*env)->DeleteLocalRef(env, fpClass);
    }

    /* ── Build ACTION_SEND intent ───────────────────────────────────────── */
    jclass    intentClass = (*env)->FindClass(env, "android/content/Intent");
    jmethodID intentInit  = (*env)->GetMethodID(env, intentClass,
                                "<init>", "(Ljava/lang/String;)V");
    jstring   action      = (*env)->NewStringUTF(env, "android.intent.action.SEND");
    jobject   intent      = (*env)->NewObject(env, intentClass, intentInit, action);

    jmethodID setType      = (*env)->GetMethodID(env, intentClass, "setType",
                                 "(Ljava/lang/String;)Landroid/content/Intent;");
    jmethodID putExtraStr  = (*env)->GetMethodID(env, intentClass, "putExtra",
                                 "(Ljava/lang/String;Ljava/lang/String;)"
                                 "Landroid/content/Intent;");
    jmethodID putExtraParc = (*env)->GetMethodID(env, intentClass, "putExtra",
                                 "(Ljava/lang/String;Landroid/os/Parcelable;)"
                                 "Landroid/content/Intent;");
    jmethodID addFlags     = (*env)->GetMethodID(env, intentClass, "addFlags",
                                 "(I)Landroid/content/Intent;");

    jstring mime      = (*env)->NewStringUTF(env, uri ? "image/png" : "text/plain");
    jstring keyText   = (*env)->NewStringUTF(env, "android.intent.extra.TEXT");
    jstring keyStream = (*env)->NewStringUTF(env, "android.intent.extra.STREAM");

    (*env)->CallObjectMethod(env, intent, setType, mime);
    (*env)->CallObjectMethod(env, intent, putExtraStr, keyText, jtext);
    if (uri)
    {
        (*env)->CallObjectMethod(env, intent, putExtraParc, keyStream, uri);
        /* FLAG_GRANT_READ_URI_PERMISSION = 1 */
        (*env)->CallObjectMethod(env, intent, addFlags, (jint)1);
    }

    /* ── Wrap in chooser and present ────────────────────────────────────── */
    jstring   title   = (*env)->NewStringUTF(env, "Share your score");
    jmethodID chooser = (*env)->GetStaticMethodID(env, intentClass, "createChooser",
        "(Landroid/content/Intent;Ljava/lang/CharSequence;)"
        "Landroid/content/Intent;");
    jobject chooserIntent = (*env)->CallStaticObjectMethod(env, intentClass,
                                chooser, intent, title);

    jclass    actClass = (*env)->GetObjectClass(env, activity->clazz);
    jmethodID startAct = (*env)->GetMethodID(env, actClass, "startActivity",
                             "(Landroid/content/Intent;)V");
    (*env)->CallVoidMethod(env, activity->clazz, startAct, chooserIntent);

    /* ── Cleanup ─────────────────────────────────────────────────────────── */
    (*env)->DeleteLocalRef(env, chooserIntent);
    (*env)->DeleteLocalRef(env, intent);
    if (uri) (*env)->DeleteLocalRef(env, uri);
    (*env)->DeleteLocalRef(env, jtext);
    (*env)->DeleteLocalRef(env, jpath);
    (*env)->DeleteLocalRef(env, jauthority);
    (*env)->DeleteLocalRef(env, jpkg);
    (*env)->DeleteLocalRef(env, title);
    (*env)->DeleteLocalRef(env, action);
    (*env)->DeleteLocalRef(env, mime);
    (*env)->DeleteLocalRef(env, keyText);
    (*env)->DeleteLocalRef(env, keyStream);
    (*env)->DeleteLocalRef(env, intentClass);
    (*env)->DeleteLocalRef(env, ctxClass);
    (*env)->DeleteLocalRef(env, actClass);
    /* Do NOT DetachCurrentThread — Sokol reuses this thread across frames. */
}
