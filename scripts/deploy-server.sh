#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: deploy-server.sh <commit-sha> <archive-path>" >&2
  exit 2
fi

commit_sha="$1"
archive_path="$2"
app_root="/srv/easygame"
release_dir="$app_root/releases/$commit_sha"
current_link="$app_root/current"
previous_release=""

if [[ ! "$commit_sha" =~ ^[0-9a-f]{40}$ ]]; then
  echo "Invalid commit SHA" >&2
  exit 2
fi

if [[ "$archive_path" != "/tmp/easygame-$commit_sha.tar.gz" ]]; then
  echo "Unexpected archive path" >&2
  exit 2
fi

if [[ -L "$current_link" ]]; then
  previous_release="$(readlink -f "$current_link")"
fi

mkdir -p "$release_dir"
tar -xzf "$archive_path" -C "$release_dir"
rm -f -- "$archive_path"

cd "$release_dir"
/usr/local/bin/npm ci --omit=dev --ignore-scripts

ln -sfn "$release_dir" "$current_link"

if sudo /bin/systemctl restart easygame.service; then
  for _ in {1..20}; do
    if /usr/bin/curl --fail --silent --show-error \
      http://127.0.0.1:3001/health >/dev/null; then
      echo "Deployment healthy: $commit_sha"
      exit 0
    fi
    sleep 1
  done
fi

echo "Deployment failed health check; rolling back" >&2
if [[ -n "$previous_release" && -d "$previous_release" ]]; then
  ln -sfn "$previous_release" "$current_link"
  sudo /bin/systemctl restart easygame.service
fi
exit 1
