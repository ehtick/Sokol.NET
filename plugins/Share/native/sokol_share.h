/* sokol_share.h -- cross-platform share sheet for Sokol.NET
   Part of the Share plugin (plugins/Share/).

   USAGE
     #define SOKOL_SHARE_IMPL before including this header in exactly one .c / .m file.
     On Android link with sokol_share_android.c; on iOS with sokol_share_ios.m.

   THREAD SAFETY
     Android: AttachCurrentThread is called internally; safe from any thread.
     iOS:     dispatch_async(main_queue) is used internally; safe from any thread.
*/
#pragma once
#ifndef SOKOL_SHARE_H
#define SOKOL_SHARE_H

#ifdef __cplusplus
extern "C" {
#endif

/* Share an image file and a short text string via the platform share sheet.
   image_path: absolute path to a PNG produced by sfs_get_pref_path (may be NULL).
   text:       UTF-8 message string.                                              */
void sokolshare_image_text(const char* image_path, const char* text);

#ifdef __cplusplus
}
#endif
#endif /* SOKOL_SHARE_H */
