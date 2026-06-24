# Unity Dedicated Server Image

This Dockerfile packages the Paper Legends Unity headless build so the API can spawn match containers with `ROOM_DOCKER_IMAGE`.

Default image tag:

```bash
paperlegends/unity-dedicated:latest
```

## Prepare Build Output

1. Build the Linux dedicated/headless server from Unity.
2. Copy the whole output into `docker/unity-server/build/`.
3. The default executable name is `PaperLegendServer.x86_64`.

If your Unity build executable uses another name, pass it at build time:

```bash
SERVER_EXECUTABLE=YourServer.x86_64 ./deploy_paper_legends_unity.sh
```

## Build Image

```bash
./deploy_paper_legends_unity.sh
```

The API reads the same image from `ROOM_DOCKER_IMAGE`.
