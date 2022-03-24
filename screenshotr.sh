#!/bin/bash

if [ ! -f "./publish/screenshotr/screenshotr" ]; then
    ./build.sh
fi

pushd publish/screenshotr
./screenshotr "$@"
popd
