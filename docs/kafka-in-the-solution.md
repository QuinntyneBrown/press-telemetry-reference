# How Kafka Could Be Leveraged in This Solution

> **Context:** The current reference design (see `docs/specs/L1.md`) deliberately uses three
> infrastructure pieces: an MQTT broker, Couchbase, and Redis. Kafka is **not** part of the
> committed design — this document explains where and why it *could* fit if requirements grow.

## The one-sentence version

Kafka would sit between "telemetry arrives" and "telemetry is stored/served," giving downstream
consumers a durable, replayable buffer so they can briefly fall behind the live stream.

## Where it would plug in

In the committed design, the ingestion worker reads MQTT messages and does two things at once:
writes to Couchbase and pushes live updates through Redis. With Kafka in the middle:

1. **MQTT → Kafka** — The ingestion worker validates each MQTT message and publishes it to a
   Kafka topic instead of writing it to the destinations inline.
2. **Kafka → Couchbase** — A consumer writes points to Couchbase, then commits its Kafka offset.
   Retained messages can wait while Couchbase is unavailable; writes must tolerate redelivery.
3. **Kafka → live fan-out** — A separate consumer group publishes each point through the existing
   Redis backplane so every API instance can deliver it through SignalR.

## What it buys

- **Durability** — Once Kafka acknowledges a record, it survives consumer crashes when replication
  is configured appropriately. MQTT QoS 0 can still lose messages before Kafka receives them.
- **Replay** — Need to backfill Couchbase after a bug fix, or build a new analytics view?
  Re-read retained history from an earlier offset.
- **Decoupling** — Storage writers, live fan-out, and future consumers (alerting, data lake
  export, ML features) use separate consumer groups and run at their own pace.
- **Spike absorption** — A burst builds lag within Kafka's configured storage and retention;
  consumers catch up when the burst passes.
- **Independent scaling** — Add consumers to a group up to the topic's partition count.

## What it costs

- **Another infrastructure dependency** — directly violating L1-008 (radical simplicity); any
  additional consumer deployables would violate its two-backend limit too.
- **More moving parts to run locally** — Docker Compose gets heavier; onboarding gets slower.
- **Operational complexity** — partitions, retention, consumer lag, duplicate handling, and upgrades.
- **Latency** — a small hop added between ingestion and live display.

## When to adopt it

Introduce Kafka when one of these becomes true:

- Short bursts exceed what the worker can handle synchronously.
- Downstream consumers must recover without losing Kafka-acknowledged records.
- Multiple downstream consumers want the same telemetry stream.
- You need replay or backfill within the configured retention window.

Until then, the current MQTT → worker → (Couchbase + Redis) path is simpler and requirement-compliant.
