# FlappyBrain Kiosk

Trade-show booth web app for the FlappyBrain BCI-adapted Flappy Bird arcade.
A Next.js 15 PWA that runs full-screen on a kiosk: visitors get their conference badge
photographed, an Anthropic vision call extracts the name, the staff start the game,
and scores land on a live leaderboard.

## How it works

1. **Idle screen** (`/`) — leaderboard with top 10 scores plus the most recent player,
   and a START button that names the next queued player.
2. **Badge scanner** (`/scan`) — camera capture, Claude `claude-3-5-haiku-20241022`
   reads the printed name, staff confirms, name is queued.
3. **Game** (`/game`) — full-screen canvas Flappy Bird. On death, the score posts to
   the leaderboard and the queued player is cleared.

## Stack

- Next.js 15 (App Router, standalone build)
- React 19
- Tailwind CSS
- better-sqlite3 (file-backed, mounted volume in production)
- Anthropic SDK for vision
- PWA: manifest + minimal service worker

## Local development

```bash
cd web/flappybrain-kiosk
cp .env.example .env.local
# edit .env.local — set ANTHROPIC_API_KEY
# DATA_DIR is optional locally (defaults to /data — set to ./data for local dev)
echo "DATA_DIR=./data" >> .env.local

npm install
npm run dev
```

Visit <http://localhost:3000>.

The badge scanner requires camera permission and HTTPS (or localhost).
For testing on another device on the LAN, you'll need to expose the dev server
over HTTPS — `next dev --experimental-https` works, or use a tunnel.

## Production build

```bash
npm run build
npm start
```

The build emits a `standalone` server at `.next/standalone/server.js`.

## Docker

```bash
docker build -t flappybrain-kiosk .
docker run -p 3000:3000 \
  -e ANTHROPIC_API_KEY=sk-ant-… \
  -v flappybrain_data:/data \
  flappybrain-kiosk
```

Or via compose:

```bash
ANTHROPIC_API_KEY=sk-ant-… docker compose up --build
```

The SQLite DB lives at `/data/flappybrain.db`. Mount a persistent volume there.

## Deploying to Azure Container Apps

These are the rough steps — replace names/regions/resource groups as needed.

1. **Create the resource group + container app environment**

   ```bash
   az group create -n flappybrain-rg -l australiaeast
   az containerapp env create \
     -n flappybrain-env \
     -g flappybrain-rg \
     -l australiaeast
   ```

2. **Create an Azure Files share for persistent state**

   ```bash
   az storage account create \
     -n flappybrainstg -g flappybrain-rg -l australiaeast --sku Standard_LRS
   az storage share create \
     --account-name flappybrainstg --name kiosk-data
   ```

   Bind the share to the Container App environment as a managed storage:

   ```bash
   STORAGE_KEY=$(az storage account keys list \
     -n flappybrainstg -g flappybrain-rg --query "[0].value" -o tsv)
   az containerapp env storage set \
     -g flappybrain-rg -n flappybrain-env \
     --storage-name kiosk-data \
     --azure-file-account-name flappybrainstg \
     --azure-file-account-key "$STORAGE_KEY" \
     --azure-file-share-name kiosk-data \
     --access-mode ReadWrite
   ```

3. **Push the image** (e.g. to ACR):

   ```bash
   az acr create -n flappybrainacr -g flappybrain-rg --sku Basic
   az acr login -n flappybrainacr
   docker build -t flappybrainacr.azurecr.io/kiosk:latest .
   docker push flappybrainacr.azurecr.io/kiosk:latest
   ```

4. **Create the container app with the volume mounted at `/data`**

   ```bash
   az containerapp create \
     -n flappybrain-kiosk -g flappybrain-rg \
     --environment flappybrain-env \
     --image flappybrainacr.azurecr.io/kiosk:latest \
     --target-port 3000 --ingress external \
     --secrets anthropic-api-key="sk-ant-…" \
     --env-vars ANTHROPIC_API_KEY=secretref:anthropic-api-key DATA_DIR=/data \
     --cpu 0.5 --memory 1.0Gi --min-replicas 1 --max-replicas 1
   ```

   Then add the volume mount via YAML (`az containerapp show ... > app.yaml`,
   edit, `az containerapp update --yaml app.yaml`):

   ```yaml
   properties:
     template:
       containers:
         - name: kiosk
           volumeMounts:
             - volumeName: kiosk-data
               mountPath: /data
       volumes:
         - name: kiosk-data
           storageType: AzureFile
           storageName: kiosk-data
   ```

5. **Test** — hit the public FQDN, scan a badge, play a round.

> Use `--min-replicas 1 --max-replicas 1` so SQLite has a single writer.

## Environment variables

| Var | Required | Default | Notes |
| --- | --- | --- | --- |
| `ANTHROPIC_API_KEY` | yes | — | Used by `/api/scan` for the vision call. |
| `DATA_DIR` | no | `/data` | Directory for `flappybrain.db`. |
| `PORT` | no | `3000` | Standard Next.js. |

## File layout

```
src/
  app/
    layout.tsx            root layout, fonts, SW registration
    page.tsx              kiosk home (server) — leaderboard + start
    game/page.tsx         game host (client)
    scan/page.tsx         badge scanner host (client)
    api/
      scan/route.ts       POST multipart image → Claude vision → name
      scores/route.ts     GET top10+recent · POST new score
      current-player/route.ts  GET · PUT queued player
  components/
    FlappyGame.tsx        canvas game (client)
    Leaderboard.tsx       ranked list (server)
    BadgeScanner.tsx      camera + capture (client)
    StartButton.tsx       pulsing start CTA (client)
    AutoRefresh.tsx       10s router.refresh() (client)
    ServiceWorkerRegistration.tsx
  lib/
    db.ts                 better-sqlite3, lazy init
    vision.ts             Anthropic client wrapper
public/
  manifest.json
  sw.js
  icon-192.png            generated by scripts/gen-icons.cjs
  icon-512.png            generated by scripts/gen-icons.cjs
```
