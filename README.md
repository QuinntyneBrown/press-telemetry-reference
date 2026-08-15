# Press Telemetry Reference

A radically simple telemetry dashboard reference solution. An external service (out of scope)
publishes telemetry over MQTT; a .NET ingestion worker validates each point, appends it to
Couchbase time series documents, and fans it out live through Redis; an ASP.NET Core API
serves historical queries, a first-render snapshot, and a SignalR live stream.

The solution consists of exactly two backend deployables, one frontend workspace, and three
external infrastructure services (MQTT broker, Couchbase, Redis).

| Part | Path |
|------|------|
| Ingestion worker (sole telemetry writer) | `backend/src/Telemetry.Ingestion.Worker` |
| API (REST + SignalR + `/health`) | `backend/src/Telemetry.Api` |
| Integration tests (ATDD acceptance suite) | `backend/tests/Telemetry.IntegrationTests` |
| Frontend workspace | not yet implemented |
| Requirements and detailed designs | `docs/` |

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) with Docker Compose

## From clean clone to running system

1. **Start the infrastructure** (MQTT broker, Couchbase 7.6 Enterprise, Redis):

   ```sh
   docker compose up -d
   ```

   The one-shot `couchbase-init` service initializes the cluster and creates the `telemetry`
   bucket with a primary index. Wait until `docker compose ps` shows the three services
   healthy and `couchbase-init` has exited.

2. **Start the ingestion worker** (terminal 1):

   ```sh
   dotnet run --project backend/src/Telemetry.Ingestion.Worker
   ```

3. **Start the API** (terminal 2):

   ```sh
   dotnet run --project backend/src/Telemetry.Api
   ```

4. **Verify health**:

   ```sh
   curl http://localhost:5000/health
   ```

   Returns `200` with per-dependency status when Couchbase and Redis are reachable, `503`
   naming the unhealthy dependency otherwise.

5. **Publish a sample telemetry point** (the external publisher is out of scope; this
   simulates it):

   ```sh
   docker compose exec mosquitto mosquitto_pub -t telemetry/press-01 -m "{\"deviceId\":\"press-01\",\"metric\":\"temperature\",\"value\":87.4,\"timestamp\":\"2026-08-15T12:00:00Z\"}"
   ```

6. **Read it back**:

   ```sh
   curl http://localhost:5000/api/telemetry/latest
   curl "http://localhost:5000/api/telemetry/press-01/temperature?from=2026-08-15T12:00:00Z&to=2026-08-15T13:00:00Z"
   ```

   Live updates stream from the SignalR hub at `http://localhost:5000/hubs/telemetry` as
   `telemetry` messages.

## API

| Endpoint | Purpose |
|----------|---------|
| `GET /api/telemetry/latest` | Newest stored point for every known series (dashboard first render) |
| `GET /api/telemetry/{deviceId}/{metric}?from={iso}&to={iso}` | Stored points in `[from, to)`, ascending; ranges wider than 24 hours are rejected |
| `/hubs/telemetry` (SignalR) | Broadcasts every ingested point to all connected clients |
| `GET /health` | Readiness of the Couchbase and Redis dependencies |

Invalid parameters return RFC 7807 ProblemDetails. Cross-origin access is restricted to the
configured `Api:CorsOrigins` allowlist.

## Configuration

All connection settings come from configuration; environment variables override the committed
defaults (which reference only local development infrastructure with the well-known
development credentials `Administrator`/`password` for Couchbase):

- Worker: `Worker:MqttBroker`, `Worker:MqttTopicFilter`, `Worker:CouchbaseConnectionString`,
  `Worker:CouchbaseUsername`, `Worker:CouchbasePassword`, `Worker:CouchbaseBucket`,
  `Worker:RedisConnectionString` (env form: `Worker__MqttBroker`, ...)
- API: `Api:CouchbaseConnectionString`, `Api:CouchbaseUsername`, `Api:CouchbasePassword`,
  `Api:CouchbaseBucket`, `Api:RedisConnectionString`, `Api:CorsOrigins` (env form:
  `Api__CouchbaseConnectionString`, ...)

A missing required setting fails startup with a log naming the setting. Log verbosity is
configurable via `Logging:LogLevel:Default`; both processes emit structured JSON logs,
including connection state changes for MQTT, Couchbase, and Redis. The repository contains no
real secrets; verify with:

```sh
gitleaks git .
```

## Accepted trade-offs

- **QoS 0 (at-most-once) MQTT delivery.** A missed dashboard sample is tolerable, and QoS 0
  removes any need for duplicate-delivery handling. Messages published while the worker is
  disconnected are lost.
- **No live replay after a Redis outage.** Points ingested while Redis is down are persisted
  to Couchbase but are not retroactively delivered to already-connected clients; live
  delivery resumes for newly ingested points. Clients heal the gap by refetching over REST on
  reconnect.
- **Persistence failure drops the point after 3 attempts.** A Couchbase write is retried up
  to 3 attempts with backoff (1 s doubling), then dropped with an error log naming the point.

## Security boundary

**End-user authentication and authorization are out of scope for this reference.** The API is
unauthenticated by design. A production consumer must add an authentication scheme (e.g.
OpenID Connect bearer tokens) to the REST endpoints and the SignalR hub, plus authorization
appropriate to its tenancy model, and serve all traffic over TLS. Baseline hardening that IS
included: all external input (MQTT payloads, HTTP parameters) is validated at the boundary,
secrets never live in source control, and CORS is restricted to configured origins.

## Running the tests

The acceptance suite provisions its own Couchbase, Redis, and Mosquitto containers via
Testcontainers — only Docker is required:

```sh
dotnet test backend
```
