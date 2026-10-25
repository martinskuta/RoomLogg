# Remote Docker deployment

RoomLogg images are published to GitHub Container Registry at [
`ghcr.io/martinskuta/roomlogg`](https://github.com/martinskuta/RoomLogg/pkgs/container/roomlogg). The image supports
`linux/amd64` and `linux/arm64`.

## Choose an image tag

The example Compose file defaults to `ghcr.io/martinskuta/roomlogg:latest`. The publishing workflow also creates
branch-name tags, semantic-version tags (for example, `1.2.3` and `1.2` for a `v1.2.3` release), and
`sha-<short-commit>` tags. `latest` follows pushes to the repository's default branch. Pull requests build the image for
validation but do not publish it.

To pin a release or commit, set `ROOMLOGG_IMAGE` before running Compose:

```sh
export ROOMLOGG_IMAGE=ghcr.io/martinskuta/roomlogg:1.2.3
# Or pin a published commit image:
# export ROOMLOGG_IMAGE=ghcr.io/martinskuta/roomlogg:sha-0123456
```

Copy `compose.example.yaml` to the Docker host. Set `ROOMLOGG_HID_GID` to the host group ID that owns the sensor device.
`ROOMLOGG_DEVICE` defaults to `/dev/hidraw0`; set it if the device has a different path. Keep any MQTT settings in the
runtime environment as described by the Compose example. For example:

```sh
export ROOMLOGG_HID_GID=123
export ROOMLOGG_DEVICE=/dev/hidraw0
```

## Pull and start

For a public package, Docker can pull anonymously. From a host with Docker Compose installed, configure the image and
hardware variables, then run:

```sh
docker compose -f compose.example.yaml pull
docker compose -f compose.example.yaml up -d
```

For a private package, create a GitHub personal access token with `read:packages` access and authenticate
interactively/non-interactively with Docker before pulling:

```sh
printf '%s' "$GHCR_TOKEN" | docker login ghcr.io --username martinskuta --password-stdin
docker compose -f compose.example.yaml pull
docker compose -f compose.example.yaml up -d
```

Keep the token in a secure secret store or shell environment; do not put it in the Compose file or commit it. Log out
when appropriate with `docker logout ghcr.io`.

To use a locally built image instead, set `ROOMLOGG_IMAGE=roomlogg:local`; that image must already exist on the Docker
host.

## Validate Compose image configuration

These commands confirm the published default and local override are resolved as intended. Supply the host's HID group ID
in place of `123`.

```sh
ROOMLOGG_HID_GID=123 docker compose -f compose.example.yaml config
ROOMLOGG_IMAGE=roomlogg:local ROOMLOGG_HID_GID=123 docker compose -f compose.example.yaml config
```