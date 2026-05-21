#!/bin/bash
# Share plugin — Linux build script.
#
# No native library is needed on Linux.
# SharePlugin.cs uses Process.Start (xdg-open) and does not call any P/Invoke
# functions on desktop platforms.
#
# Usage: ./plugins/Share/scripts/build-linux.sh

echo "========================================="
echo "sokol_share — Linux"
echo "No native library needed on Linux."
echo "The Share plugin uses Process.Start (xdg-open) for desktop platforms."
echo "========================================="
exit 0
