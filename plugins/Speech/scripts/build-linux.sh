#!/bin/bash
# Speech plugin — Linux build script.
#
# No native library is needed on Linux: Speech.cs drives speech-dispatcher's
# `spd-say` as a process (probed at runtime, never linked — espeak-ng behind it is GPL).
#
# Usage: ./plugins/Speech/scripts/build-linux.sh

echo "========================================="
echo "sokol_speech — Linux"
echo "No native library needed on Linux (managed spd-say backend)."
echo "========================================="
exit 0
