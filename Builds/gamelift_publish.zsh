BUILD_ROOT="Release/Linux/Mono/Server"

[ -d $BUILD_ROOT ] && aws gamelift upload-build \
--name voxelfield \
--build-version "$1" \
--build-root $BUILD_ROOT \
--operating-system AMAZON_LINUX_2