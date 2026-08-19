#!/usr/bin/env bash
set -euo pipefail

node_version="v24.15.0"
node_archive="node-${node_version}-linux-x64.tar.xz"
node_url="https://nodejs.org/dist/${node_version}"

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y --no-install-recommends ca-certificates curl xz-utils

if [[ ! -x "/opt/node-${node_version}-linux-x64/bin/node" ]]; then
  temp_dir="$(mktemp -d)"
  trap 'rm -rf -- "$temp_dir"' EXIT
  cd "$temp_dir"
  curl --fail --silent --show-error --location \
    --output "$node_archive" "$node_url/$node_archive"
  curl --fail --silent --show-error --location \
    --output SHASUMS256.txt "$node_url/SHASUMS256.txt"
  grep "  $node_archive\$" SHASUMS256.txt | sha256sum --check --strict
  tar -xJf "$node_archive" -C /opt
fi

ln -sfn "/opt/node-${node_version}-linux-x64" /opt/node
ln -sfn /opt/node/bin/node /usr/local/bin/node
ln -sfn /opt/node/bin/npm /usr/local/bin/npm
ln -sfn /opt/node/bin/npx /usr/local/bin/npx
ln -sfn /opt/node/bin/corepack /usr/local/bin/corepack

if ! id easygame >/dev/null 2>&1; then
  useradd --create-home --shell /bin/bash easygame
fi

install -d -m 700 -o easygame -g easygame /home/easygame/.ssh
install -m 600 -o easygame -g easygame \
  /tmp/easygame_actions.pub /home/easygame/.ssh/authorized_keys

install -d -m 755 -o easygame -g easygame /srv/easygame
install -d -m 755 -o easygame -g easygame /srv/easygame/releases
install -d -m 700 -o easygame -g easygame /var/lib/easygame

cat >/etc/sudoers.d/easygame-deploy <<'EOF'
easygame ALL=(root) NOPASSWD: /bin/systemctl restart easygame.service
easygame ALL=(root) NOPASSWD: /bin/systemctl is-active easygame.service
easygame ALL=(root) NOPASSWD: /bin/systemctl status easygame.service
easygame ALL=(root) NOPASSWD: /bin/systemctl restart easygame-side-scroller.service
easygame ALL=(root) NOPASSWD: /bin/systemctl is-active easygame-side-scroller.service
easygame ALL=(root) NOPASSWD: /bin/systemctl status easygame-side-scroller.service
EOF
chmod 440 /etc/sudoers.d/easygame-deploy
visudo --check --file=/etc/sudoers.d/easygame-deploy

install -m 644 /tmp/easygame.service /etc/systemd/system/easygame.service
install -m 644 /tmp/easygame-side-scroller.service \
  /etc/systemd/system/easygame-side-scroller.service
install -m 644 /tmp/easygame.nginx.conf \
  /www/server/panel/vhost/nginx/easygame.conf

systemctl daemon-reload
systemctl enable easygame.service
systemctl enable easygame-side-scroller.service
nginx -t
systemctl reload nginx

echo "SERVER_BOOTSTRAP_OK"
node --version
npm --version
