# How Redis Pub/Sub Could Be Leveraged Instead of Kafka

> **Context:** The current reference design already uses Redis as the SignalR backplane.
> Redis Pub/Sub is therefore available without adding infrastructure, but it overlaps with only
> part of what Kafka would provide. This document explains where that boundary sits.

## The one-sentence version

Redis Pub/Sub can replace Kafka for low-latency delivery to currently connected consumers, but
not for durable buffering, replay, or consumers that need to fall behind and catch up later.

## Where it would plug in

In the committed design, the ingestion worker writes to Couchbase and publishes live updates
through Redis. That is already the safest use of Pub/Sub in this solution:

1. **MQTT → worker** — The ingestion worker receives and validates each telemetry message.
2. **Worker → Couchbase** — The worker writes the point directly because Pub/Sub cannot hold it
   while a storage subscriber is unavailable.
3. **Worker → Redis Pub/Sub → SignalR** — The worker publishes the point through the existing
   backplane so every API instance and connected dashboard receives the live update.

## What it buys

- **No new infrastructure** — Redis is already required, so L1-008 remains satisfied.
- **Low-latency fan-out** — Every active subscriber receives each published point immediately.
- **Decoupling** — Publishers know only the channel; subscribers can be added or removed without
  changing the ingestion worker.
- **Simple routing** — Channels and pattern subscriptions can separate telemetry by purpose,
  device, or metric when that becomes useful.
- **Scale-out** — Redis Cluster offers sharded Pub/Sub when ordinary channel propagation becomes
  a bottleneck.

## What it costs

- **No durable backlog** — Messages are not stored, even when Redis persistence is enabled. A
  disconnected subscriber misses them and cannot replay them later.
- **No independent pacing** — Subscribers must keep up with the live stream; there is no retained
  backlog from which a slow consumer can recover.
- **No consumer groups** — Pub/Sub broadcasts to every subscriber and provides no acknowledgements,
  offsets, pending-message tracking, or built-in work sharing.
- **Outage gaps** — A Redis outage interrupts live delivery until newly published messages resume.

## When to adopt it

Use Redis Pub/Sub instead of Kafka when these remain true:

- Telemetry is needed live, while history continues to come from Couchbase.
- Every connected consumer should receive the same message, and missed updates are tolerable or
  healed by querying Couchbase after reconnecting.
- Consumers can keep up with the incoming rate.
- Avoiding another infrastructure dependency matters more than retaining the event stream.

The overlap is narrow: both distribute events and decouple active components, but Pub/Sub is not
a durable log. If buffering, acknowledgements, replay, or recovery are required, Redis Streams
— a different Redis feature — is the closer alternative to Kafka.
