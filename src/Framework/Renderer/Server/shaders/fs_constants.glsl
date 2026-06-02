const float M_PI = 3.141592653589793;

// Must match RenderingConstants.MAX_LIGHTS (32).
const int MAX_LIGHTS = 32;

const int LIGHT_TYPE_DIRECTIONAL = 0;
const int LIGHT_TYPE_POINT       = 1;
const int LIGHT_TYPE_SPOT        = 2;

// CSM cascade count (compile-time; one variant per count).
// Defined by --defines "CSM4", "CSM2", or "CSM1" at build time.
// Must be a #define (not const int) so it can be used in #if preprocessor checks.
#ifndef CSM_CASCADES
  #if defined(CSM4)
    #define CSM_CASCADES 4
  #elif defined(CSM2)
    #define CSM_CASCADES 2
  #elif defined(CSM1)
    #define CSM_CASCADES 1
  #else
    #define CSM_CASCADES 0
  #endif
#endif
