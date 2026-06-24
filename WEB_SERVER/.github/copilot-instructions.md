# BanCuLi Server – AI Coding Instructions

## Architecture Overview

**BanCuLi** is a multiplayer game backend with three main tiers:

1. **HTTP API** (Express.js, port 5000): REST endpoints for game state, inventory, matchmaking, and admin
2. **WebSocket Server** (ws, port 5001): Real-time game events and player communication
3. **Dedicated Game Servers** (Docker containers): Unity headless game instances spawned per match via `docker run`

**Key Pattern**: Node cluster (sticky sessions by IP) routes API requests to CPU-bound workers; WebSocket upgrade is handled separately.

## Essential Data Model

**Core entities** (PostgreSQL via Prisma):
- `Player`: Game account, inventory, achievements, balance history
- `Room`: Match queue, contains multiple RoomUsers  
- `Item`: Equippable gear with physics/element properties
- `EquipPlayer`/`PlayerItem`: Player inventory/equipped items with sequencing
- `EffectPlayer`: Active/passive player effects (attributes like power, spin)
- `History`: Match results with timestamps
- `FriendRequest`/`Friendship`/`FriendMessage`: Social graph
- `ServerPortPool`: Tracks available container ports (busy/free state)

**Critical Relations**: Player has many PlayerItems (inventory), many EquipPlayers (equipped gear per slot), many EffectPlayers (active effects).

## Dev Workflows

### Database Schema & Migrations
```bash
# After modifying schema.prisma:
npx prisma generate          # Regenerate Prisma Client
npx prisma migrate dev --name "description"  # Create & apply migration
npx prisma migrate reset --force  # Nuke DB and reseed (dev only!)
npx prisma db push           # Push schema changes without migration file
```
- Migrations live in `prisma/migrations/`; always commit them
- Seed logic in `prisma/seed.ts` (runs after reset)

### Build & Run
```bash
npm run build      # TypeScript → dist/
npm start          # Runs dist/server.js (requires NODE_ENV, DATABASE_URL)
npm test           # Node test runner on ts-node (currently friendService.test.ts)
```

### Docker & Container Management
```bash
# Build Unity dedicated server image (run after rebuilding Unity headless):
docker build -f docker/unity-server/Dockerfile -t banculi/unity-dedicated:latest .

# The API spawns containers from this image per match via orchestrator.ts
docker run -p <port>:27015 banculi/unity-dedicated:latest
```

## Project-Specific Patterns

### 1. Routes → Controllers → Services Pattern
- **Routes** (`src/routes/*Routes.ts`): Attach `authMiddleware` where needed, delegate to controllers
- **Controllers** (`src/controllers/*Controller.ts`): Extract request body/params, call services, send responses
- **Services** (`src/services/*Service.ts`): Business logic, Prisma queries, external API calls

**Example**: `playerRoutes.get('/players/:id', getPlayerController)` → `playerService.getPlayer(id)`

### 2. Authentication & Middleware
- Bearer token in `Authorization` header (set by `authMiddleware.ts`)
- `VerifiedAccessToken` interface attached to `req.auth` for use in controllers
- UGS→Firebase token bridge at `/auth/ugs-to-firebase` (Unity Gaming Services)
- Admin routes protected by `adminAuth.ts` (checks admin flag in token)

### 3. Prisma Client Pattern
- Singleton instance in `src/models/prismaClient.ts` imported everywhere
- Use Prisma query relations (`.include`, `.select`) to avoid N+1 queries
- Indexes on frequently filtered fields (`IsActive`, `IdAccount`, `typeGid`, etc.)

### 4. Item & Effect Equipping
- Items have `typeGid` (weapon/armor/passive type) and `isLevelUp` flag
- `EquipPlayer` stores equipped items per player + location slot
- `BALL_SLOT_LOCATION_ID = 2` is the special ball/weapon equip slot
- Effects use flags: `IsActive` (can be used), `IsEquiped` (equipped), `isPassive` (passive bonus)
- Equipping requires level checks (`Item.Levelrequired`) and material consumption

### 5. Match Lifecycle & Orchestration
- `matchmakingService.ts` finds/creates rooms, validates player counts
- `orchestrator.ts` spins up Docker containers for matches
- `containerRuntime.ts` manages container state (spawn, monitor, cleanup)
- Ports tracked in `ServerPortPool` table (busy flag, containerId reference)
- Match results stored in `History` table after container exits

### 6. WebSocket Handler Registration
- Message types dispatched in `src/websocket/handlers.ts`
- Players registry (`src/websocket/registry.ts`) maintains connected player→WebSocket map
- New message types: add to `handleMessage()` switch and implement handler function
- Always validate `context.playerId` is set before sending player-specific events

### 7. Friend & Social Graph
- `FriendRequest`: pending invites (sender/receiver)
- `Friendship`: mutual connection (bidirectional via `PlayerFriendships`/`FriendPlayerShips` relations)
- `FriendMessage`: chat messages with delete flags
- Queries must use correct relation alias (e.g., `sentFriendRequests` vs `receivedFriendRequests`)

### 8. Config & Type Mappings
- `src/config/typeMatchGid.ts`: Game mode/match type IDs
- `src/config/generalCategory.ts`: System category lookups
- Environment variables: `DATABASE_URL`, `API_PORT` (5000), `WS_PORT` (5001), `FIREBASE_SERVICE_ACCOUNT_KEY`, `ROOM_DOCKER_IMAGE`

## Common Tasks

### Adding a New Game Feature
1. Define/update Prisma model in `schema.prisma`
2. Run `npx prisma migrate dev --name "feature_name"`
3. Create `src/services/featureService.ts` with Prisma queries
4. Create `src/controllers/featureController.ts` wrapping service calls
5. Add routes in `src/routes/featureRoutes.ts`
6. Import route in `src/app.ts` and attach to `/api` prefix

### Debugging
- Database: Use `npx prisma studio` to inspect tables and relations
- API: Log request/response in controllers or use Network tab (admin UI at `/api`)
- WebSocket: Check browser console; events logged to stdout
- Container spawn: Check `docker logs <container_id>` for Unity server logs

### Querying Relations
```typescript
// Don't: N+1 query
const player = await prisma.player.findUnique({ where: { id: 1 } });
const items = await prisma.playerItem.findMany({ where: { playerId: player.id } });

// Do: Include relations
const player = await prisma.player.findUnique({
  where: { id: 1 },
  include: { playerItems: true, equipPlayers: true }
});
```

## Critical Files

| File | Purpose |
|------|---------|
| [prisma/schema.prisma](prisma/schema.prisma) | Data model & indexes |
| [src/server.ts](src/server.ts) | Entry point: cluster + WebSocket init |
| [src/cluster.ts](src/cluster.ts) | Sticky-session load balancing by IP |
| [src/app.ts](src/app.ts) | Express app + route mounting |
| [src/middleware/authMiddleware.ts](src/middleware/authMiddleware.ts) | Bearer token validation |
| [src/services/playerService.ts](src/services/playerService.ts) | Player CRUD, friend code gen |
| [src/services/matchmakingService.ts](src/services/matchmakingService.ts) | Room creation, queue logic |
| [src/services/orchestrator.ts](src/services/orchestrator.ts) | Docker container spawn/manage |
| [src/websocket/handlers.ts](src/websocket/handlers.ts) | Real-time message dispatch |

---

**Last updated**: Jan 2026 | Uses: TypeScript 5.8, Express 5.1, Prisma 6.6, PostgreSQL
