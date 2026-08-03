#ifndef SOKOL_SAFEAREA_INCLUDED
/*
    sokol_safearea.h -- query the display's safe-area insets (notch / Dynamic Island /
                        punch-hole camera / home indicator / curved & waterfall edges)

    Project URL: https://github.com/elix22/Sokol.NET

    Do this:
        #define SOKOL_IMPL or #define SOKOL_SAFEAREA_IMPL
    before you include this file in *one* C or ObjC file to create the
    implementation.

    sokol_app.h must be included before sokol_safearea.h.

    WHY THIS EXISTS
    ===============
    sokol_app.h hands you a framebuffer that covers the WHOLE display, including
    the strip behind a notch/Dynamic Island and the rounded corners. Anything an
    app draws there is clipped by the hardware -- on a modern phone in landscape
    that is text and buttons along the leading edge, silently cut off.

    Both mobile OSes already compute the usable rect per device, per orientation
    and per fold state, so there is nothing to derive and no device table to
    maintain (a hardcoded one is wrong the moment the user rotates or unfolds).
    This module wraps the two platform queries behind one function.

        iOS      UIWindow.safeAreaInsets -- notch/Dynamic Island, home indicator,
                 AND the rounded-corner allowance.
        Android  WindowInsets.getDisplayCutout() safe insets (API 28+), widened by
                 DisplayCutout.getWaterfallInsets() (API 31+) for curved screens.
        others   zero (desktop and web have no cutouts).

    HOW TO USE
    ==========
        float ltrb[4];
        ssafe_get(ltrb);            // left, top, right, bottom
        // inset your UI/content rect by ltrb; the background may still bleed
        // to the full framebuffer.

    UNITS
    =====
    PHYSICAL PIXELS, i.e. the same space as sapp_width()/sapp_height(), so a
    caller working in logical units divides by the same sapp_dpi_scale() it uses
    for the framebuffer size. Zeros are returned on every unsupported platform
    and on any query failure, so a caller never needs a platform #if.

    CALL ORDER
    ==========
    Any time after sapp has a window. The values change on rotation and on fold,
    so re-read them when the framebuffer size changes rather than caching once.
    Cheap on iOS (a property read); on Android it is a handful of JNI calls, so
    prefer re-reading on resize over every frame.

    ANDROID CAVEAT
    ==============
    View.getRootWindowInsets() is documented for the UI thread, and sokol runs the
    app on its own thread. The query is therefore best-effort: it attaches to the
    JVM if needed, clears any pending exception, and reports zeros if anything
    fails -- never a crash. Note also that an app which does not opt into
    LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES/ALWAYS is letterboxed away from the
    cutout on long edges by the system, in which case zero is the correct answer.

    zlib/libpng license -- same terms as the sokol headers.
*/
#define SOKOL_SAFEAREA_INCLUDED (1)

#if defined(SOKOL_IMPL) && !defined(SOKOL_SAFEAREA_IMPL)
#define SOKOL_SAFEAREA_IMPL
#endif

#include <stdint.h>
#include <stdbool.h>

#if !defined(SOKOL_APP_INCLUDED)
#error "Please include sokol_app.h before sokol_safearea.h"
#endif

#if defined(SOKOL_API_DECL) && !defined(SOKOL_SAFEAREA_API_DECL)
#define SOKOL_SAFEAREA_API_DECL SOKOL_API_DECL
#endif
#ifndef SOKOL_SAFEAREA_API_DECL
#if defined(_WIN32) && defined(SOKOL_DLL) && defined(SOKOL_SAFEAREA_IMPL)
#define SOKOL_SAFEAREA_API_DECL __declspec(dllexport)
#elif defined(_WIN32) && defined(SOKOL_DLL)
#define SOKOL_SAFEAREA_API_DECL __declspec(dllimport)
#else
#define SOKOL_SAFEAREA_API_DECL extern
#endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* true if this platform can report safe-area insets at all (iOS/Android) */
SOKOL_SAFEAREA_API_DECL bool ssafe_supported(void);

/* Write the safe-area insets, in PHYSICAL PIXELS, into out_ltrb as
   { left, top, right, bottom }. Always writes 4 floats; zeros where the
   platform has no cutout or the query failed. Does nothing if out_ltrb is null. */
SOKOL_SAFEAREA_API_DECL void ssafe_get(float* out_ltrb);

#ifdef __cplusplus
} /* extern "C" */
#endif
#endif /* SOKOL_SAFEAREA_INCLUDED */

/*=== IMPLEMENTATION =========================================================*/
#ifdef SOKOL_SAFEAREA_IMPL
#define SOKOL_SAFEAREA_IMPL_INCLUDED (1)

/*--- iOS -------------------------------------------------------------------*/
#if defined(__APPLE__)
#include <TargetConditionals.h>
#endif

#if defined(__APPLE__) && defined(TARGET_OS_IOS) && TARGET_OS_IOS

#import <UIKit/UIKit.h>

static void _ssafe_get(float* o) {
    UIWindow* win = (__bridge UIWindow*) sapp_ios_get_window();
    if (nil == win) {
        return;
    }
    /* safeAreaInsets is in POINTS and already accounts for the rounded corners,
       so a notchless-but-round phone still reports a non-zero landscape inset. */
    const UIEdgeInsets ins = win.safeAreaInsets;
    const float s = sapp_dpi_scale();
    o[0] = (float)ins.left   * s;
    o[1] = (float)ins.top    * s;
    o[2] = (float)ins.right  * s;
    o[3] = (float)ins.bottom * s;
}

#define _SSAFE_SUPPORTED (1)

/*--- Android ---------------------------------------------------------------*/
#elif defined(__ANDROID__)

#include <jni.h>
#include <android/native_activity.h>

/* Best-effort: any failure leaves the caller's zeros in place. */
static void _ssafe_get(float* o) {
    const ANativeActivity* act = (const ANativeActivity*) sapp_android_get_native_activity();
    if ((NULL == act) || (NULL == act->vm) || (NULL == act->clazz)) {
        return;
    }
    JavaVM* vm = act->vm;
    JNIEnv* env = NULL;
    bool attached = false;
    if ((*vm)->GetEnv(vm, (void**)&env, JNI_VERSION_1_6) != JNI_OK) {
        /* sokol's app thread is normally attached already; attach if it is not.
           Deliberately NOT detached again: the thread lives for the whole app,
           and detach/attach churn per query is both slow and error-prone. */
        if ((*vm)->AttachCurrentThread(vm, &env, NULL) != JNI_OK) {
            return;
        }
        attached = true;
    }
    (void)attached;
    if (NULL == env) {
        return;
    }

    jobject activity = act->clazz;
    jclass  cls_act  = (*env)->GetObjectClass(env, activity);
    jmethodID m_getWindow = (*env)->GetMethodID(env, cls_act, "getWindow", "()Landroid/view/Window;");
    if (NULL == m_getWindow) { goto cleanup; }
    jobject window = (*env)->CallObjectMethod(env, activity, m_getWindow);
    if (NULL == window) { goto cleanup; }

    jclass cls_win = (*env)->GetObjectClass(env, window);
    jmethodID m_getDecor = (*env)->GetMethodID(env, cls_win, "getDecorView", "()Landroid/view/View;");
    if (NULL == m_getDecor) { goto cleanup; }
    jobject decor = (*env)->CallObjectMethod(env, window, m_getDecor);
    if (NULL == decor) { goto cleanup; }

    jclass cls_view = (*env)->GetObjectClass(env, decor);
    /* getRootWindowInsets(): API 23+ */
    jmethodID m_getInsets = (*env)->GetMethodID(env, cls_view, "getRootWindowInsets", "()Landroid/view/WindowInsets;");
    if (NULL == m_getInsets) { goto cleanup; }
    jobject insets = (*env)->CallObjectMethod(env, decor, m_getInsets);
    if (NULL == insets) { goto cleanup; }

    jclass cls_ins = (*env)->GetObjectClass(env, insets);
    /* getDisplayCutout(): API 28+ -- absent (or null) means "no cutout", i.e. zeros. */
    jmethodID m_getCutout = (*env)->GetMethodID(env, cls_ins, "getDisplayCutout", "()Landroid/view/DisplayCutout;");
    if (NULL == m_getCutout) { goto cleanup; }
    jobject cutout = (*env)->CallObjectMethod(env, insets, m_getCutout);
    if (NULL == cutout) { goto cleanup; }

    jclass cls_cut = (*env)->GetObjectClass(env, cutout);
    jmethodID m_l = (*env)->GetMethodID(env, cls_cut, "getSafeInsetLeft",   "()I");
    jmethodID m_t = (*env)->GetMethodID(env, cls_cut, "getSafeInsetTop",    "()I");
    jmethodID m_r = (*env)->GetMethodID(env, cls_cut, "getSafeInsetRight",  "()I");
    jmethodID m_b = (*env)->GetMethodID(env, cls_cut, "getSafeInsetBottom", "()I");
    if (m_l && m_t && m_r && m_b) {
        o[0] = (float)(*env)->CallIntMethod(env, cutout, m_l);
        o[1] = (float)(*env)->CallIntMethod(env, cutout, m_t);
        o[2] = (float)(*env)->CallIntMethod(env, cutout, m_r);
        o[3] = (float)(*env)->CallIntMethod(env, cutout, m_b);
    }

    /* Curved / waterfall screens (API 31+): widen by the waterfall insets, which
       are reported separately from the cutout. android.graphics.Insets exposes
       left/top/right/bottom as public int fields. */
    jmethodID m_wf = (*env)->GetMethodID(env, cls_cut, "getWaterfallInsets", "()Landroid/graphics/Insets;");
    if (NULL != m_wf) {
        jobject wf = (*env)->CallObjectMethod(env, cutout, m_wf);
        if (NULL != wf) {
            jclass cls_wf = (*env)->GetObjectClass(env, wf);
            const char* names[4] = { "left", "top", "right", "bottom" };
            for (int i = 0; i < 4; i++) {
                jfieldID f = (*env)->GetFieldID(env, cls_wf, names[i], "I");
                if (NULL == f) { continue; }
                const float v = (float)(*env)->GetIntField(env, wf, f);
                if (v > o[i]) { o[i] = v; }
            }
        }
    }

cleanup:
    /* A missing method on an older API level raises NoSuchMethodError; that is an
       expected outcome here, not a failure worth propagating into the app. */
    if ((*env)->ExceptionCheck(env)) {
        (*env)->ExceptionClear(env);
    }
}

#define _SSAFE_SUPPORTED (1)

/*--- desktop / web: no cutouts --------------------------------------------*/
#else

static void _ssafe_get(float* o) {
    (void)o;
}

#define _SSAFE_SUPPORTED (0)

#endif

SOKOL_API_IMPL bool ssafe_supported(void) {
    return _SSAFE_SUPPORTED ? true : false;
}

SOKOL_API_IMPL void ssafe_get(float* out_ltrb) {
    if (NULL == out_ltrb) {
        return;
    }
    out_ltrb[0] = 0.0f; out_ltrb[1] = 0.0f; out_ltrb[2] = 0.0f; out_ltrb[3] = 0.0f;
    _ssafe_get(out_ltrb);
}

#endif /* SOKOL_SAFEAREA_IMPL */
