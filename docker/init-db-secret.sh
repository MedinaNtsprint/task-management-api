#!/bin/sh
set -eu
umask 077

volume=$1
secret="$volume/password"
# Only the initializer can write or list this dedicated mount. The consumers
# run as different UIDs, so they can traverse it to the known read-only file.
chmod 0711 "$volume"
if [ -f "$secret" ]; then
    exit 0
fi

entropy=$(od -An -v -tx1 -N32 /dev/urandom)
password=$(printf '%s' "$entropy" | tr -d '[:space:]')
if [ "${#password}" -ne 64 ]; then
    printf '%s\n' 'Could not generate the database password.' >&2
    exit 1
fi

temporary=$(mktemp "$volume/.password.XXXXXX")
trap 'rm -f "$temporary"' EXIT
printf '%s\n' "$password" > "$temporary"
chmod 0444 "$temporary"
# Publish the complete file atomically, without replacing an existing password.
ln "$temporary" "$secret"
