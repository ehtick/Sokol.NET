/* sokol_speech_queue.c -- lock-protected event ring shared by the native
   backends. Platform code emits with sokolspeech__emit() (any thread); the game
   thread drains with sokolspeech_poll_event(). Events carry no strings, so
   nothing is allocated. */
#include "sokol_speech.h"

#if defined(_WIN32)
#include <windows.h>
typedef CRITICAL_SECTION _ss_mutex;
static _ss_mutex _ss_lock;
static int _ss_lock_ready = 0;
static void _ss_lock_acquire(void) {
    if (!_ss_lock_ready) { InitializeCriticalSection(&_ss_lock); _ss_lock_ready = 1; }
    EnterCriticalSection(&_ss_lock);
}
static void _ss_lock_release(void) { LeaveCriticalSection(&_ss_lock); }
#else
#include <pthread.h>
static pthread_mutex_t _ss_lock = PTHREAD_MUTEX_INITIALIZER;
static void _ss_lock_acquire(void) { pthread_mutex_lock(&_ss_lock); }
static void _ss_lock_release(void) { pthread_mutex_unlock(&_ss_lock); }
#endif

#define SOKOLSPEECH_QUEUE_CAP 32

static sokolspeech_event _ss_ring[SOKOLSPEECH_QUEUE_CAP];
static int _ss_head = 0;   /* next pop */
static int _ss_count = 0;

/* Internal, called by the platform shims. Drops the event if the ring is full
   (32 pending speech events means the app stopped polling). */
void sokolspeech__emit(int type, int id, int code)
{
    _ss_lock_acquire();
    if (_ss_count < SOKOLSPEECH_QUEUE_CAP) {
        sokolspeech_event* e = &_ss_ring[(_ss_head + _ss_count) % SOKOLSPEECH_QUEUE_CAP];
        e->type = type;
        e->id   = id;
        e->code = code;
        _ss_count++;
    }
    _ss_lock_release();
}

bool sokolspeech_poll_event(sokolspeech_event* out)
{
    if (!out) return false;
    _ss_lock_acquire();
    if (_ss_count == 0) {
        _ss_lock_release();
        return false;
    }
    *out = _ss_ring[_ss_head];
    _ss_head = (_ss_head + 1) % SOKOLSPEECH_QUEUE_CAP;
    _ss_count--;
    _ss_lock_release();
    return true;
}
