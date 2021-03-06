#!/bin/bash

VERSION_PATH="./version.txt"
EXEC_PATH="./Server/Raven.Server"

ASSEMBLY_VERSION=$(eval $EXEC_PATH --version)
VERSION=""

if [[ -f "$VERSION_PATH" ]]; then
    VERSION=$(cat $VERSION_PATH);
fi

if [[ "$ASSEMBLY_VERSION" != "$VERSION" ]]; then
    echo "$ASSEMBLY_VERSION" > "$VERSION_PATH"
    xdg-open "http://ravendb.net/first-run?type=start&ver=$ASSEMBLY_VERSION";
fi

sleep 2 # avoid Firefox already open warning preventing from launching the Studio tab
eval "$EXEC_PATH --browser";
