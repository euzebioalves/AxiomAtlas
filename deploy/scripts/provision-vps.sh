#!/usr/bin/env bash
set -Eeuo pipefail

[[ "$(id -u)" -eq 0 ]] || { echo 'Execute como root durante o provisionamento inicial.' >&2; exit 1; }
. /etc/os-release
[[ "${ID:-}" == "ubuntu" && "${VERSION_ID:-}" == "24.04" ]] || { echo 'Este script requer Ubuntu Server 24.04.' >&2; exit 1; }
id atlasadmin >/dev/null 2>&1 || { echo 'Crie e valide o usuário atlasadmin antes de executar este script.' >&2; exit 1; }

apt-get update
apt-get install -y ca-certificates curl gnupg lsb-release ufw fail2ban unattended-upgrades age rclone jq
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
chmod a+r /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" > /etc/apt/sources.list.d/docker.list
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker fail2ban
timedatectl set-timezone UTC

install -d -m 0750 -o atlasadmin -g atlasadmin /opt/axiom-atlas /opt/axiom-atlas/state /opt/axiom-atlas/backups
if ! swapon --show | grep -q .; then
  fallocate -l 2G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

ufw allow OpenSSH
ufw limit OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable
if [[ -f /opt/axiom-atlas/deploy/systemd/axiom-atlas-backup.service ]]; then
  install -m 0644 /opt/axiom-atlas/deploy/systemd/axiom-atlas-backup.service /etc/systemd/system/axiom-atlas-backup.service
  install -m 0644 /opt/axiom-atlas/deploy/systemd/axiom-atlas-backup.timer /etc/systemd/system/axiom-atlas-backup.timer
  systemctl daemon-reload
  systemctl enable --now axiom-atlas-backup.timer
fi
printf 'Provisionamento básico concluído. Valide uma segunda sessão SSH por chave antes de alterar sshd_config.\n'
