#!/bin/bash
# One-shot initializer: cluster, indexer storage mode, telemetry bucket, primary index.
# Idempotent — safe to re-run on every `docker compose up`.
set -u

HOST=couchbase
USER=Administrator
PASS=password

echo "waiting for Couchbase REST API..."
until curl -s "http://$HOST:8091/pools" > /dev/null; do sleep 2; done

if couchbase-cli server-list -c "$HOST" -u "$USER" -p "$PASS" > /dev/null 2>&1; then
  echo "cluster already initialized"
else
  for _ in $(seq 1 30); do
    couchbase-cli cluster-init -c "$HOST" \
      --cluster-username "$USER" --cluster-password "$PASS" \
      --services data,index,query \
      --cluster-ramsize 512 --cluster-index-ramsize 256 && break
    sleep 2
  done
fi

# Enterprise Edition requires an indexer storage mode before any index can be created.
curl -s -u "$USER:$PASS" -X POST "http://$HOST:8091/settings/indexes" -d storageMode=plasma > /dev/null

if couchbase-cli bucket-list -c "$HOST" -u "$USER" -p "$PASS" | grep -q '^telemetry$'; then
  echo "telemetry bucket already exists"
else
  couchbase-cli bucket-create -c "$HOST" -u "$USER" -p "$PASS" \
    --bucket telemetry --bucket-type couchbase --bucket-ramsize 256 --enable-flush 1 --wait
fi

echo "creating primary index..."
for _ in $(seq 1 30); do
  cbq -e "http://$HOST:8093" -u "$USER" -p "$PASS" \
    --script='CREATE PRIMARY INDEX IF NOT EXISTS ON `telemetry`;' 2>/dev/null | grep -q '"status": "success"' && break
  sleep 2
done

echo "Couchbase initialized"
