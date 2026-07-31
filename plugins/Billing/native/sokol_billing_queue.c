/* sokol_billing_queue.c -- lock-protected event ring shared by the Android and
   iOS implementations. Platform code emits with sokolbilling__emit() (any
   thread); the game thread drains with sokolbilling_poll_event().

   Strings are copied on emit and owned by the queue; the strings handed out by
   poll stay valid until the next poll (freed lazily, nearnet-style). */
#include "sokol_billing.h"

#include <pthread.h>
#include <stdlib.h>
#include <string.h>

#define SOKOLBILLING_QUEUE_CAP 16

typedef struct {
    int   type;
    int   code;
    char* sku;
    char* price;
    char* proof;
    char* signature;
} _sb_event;

static pthread_mutex_t _sb_lock = PTHREAD_MUTEX_INITIALIZER;
static _sb_event _sb_ring[SOKOLBILLING_QUEUE_CAP];
static int _sb_head = 0;   /* next pop  */
static int _sb_count = 0;
static _sb_event _sb_last; /* strings handed to the caller by the last poll */

static char* _sb_dup(const char* s) {
    if (!s) return NULL;
    size_t n = strlen(s) + 1;
    char* d = (char*)malloc(n);
    if (d) memcpy(d, s, n);
    return d;
}

static void _sb_free_event(_sb_event* e) {
    free(e->sku);       e->sku = NULL;
    free(e->price);     e->price = NULL;
    free(e->proof);     e->proof = NULL;
    free(e->signature); e->signature = NULL;
    e->type = 0;
    e->code = 0;
}

/* Internal, called by the platform shims. Drops the event if the ring is full
   (16 pending store events means the app stopped polling — nothing better to do). */
void sokolbilling__emit(int type, int code, const char* sku, const char* price,
                        const char* proof, const char* signature)
{
    pthread_mutex_lock(&_sb_lock);
    if (_sb_count < SOKOLBILLING_QUEUE_CAP) {
        _sb_event* e = &_sb_ring[(_sb_head + _sb_count) % SOKOLBILLING_QUEUE_CAP];
        e->type      = type;
        e->code      = code;
        e->sku       = _sb_dup(sku);
        e->price     = _sb_dup(price);
        e->proof     = _sb_dup(proof);
        e->signature = _sb_dup(signature);
        _sb_count++;
    }
    pthread_mutex_unlock(&_sb_lock);
}

bool sokolbilling_poll_event(sokolbilling_event* out)
{
    if (!out) return false;
    pthread_mutex_lock(&_sb_lock);
    _sb_free_event(&_sb_last);
    if (_sb_count == 0) {
        pthread_mutex_unlock(&_sb_lock);
        return false;
    }
    _sb_last = _sb_ring[_sb_head];
    memset(&_sb_ring[_sb_head], 0, sizeof(_sb_event));
    _sb_head = (_sb_head + 1) % SOKOLBILLING_QUEUE_CAP;
    _sb_count--;
    pthread_mutex_unlock(&_sb_lock);

    out->type      = _sb_last.type;
    out->code      = _sb_last.code;
    out->sku       = _sb_last.sku;
    out->price     = _sb_last.price;
    out->proof     = _sb_last.proof;
    out->signature = _sb_last.signature;
    return true;
}
