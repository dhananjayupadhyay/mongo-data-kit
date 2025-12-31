# MongoDB Replica Set Cluster

This docker-compose creates a 3-node MongoDB replica set for testing transaction support.

## Quick Start

```bash
docker-compose up -d
```

## Services

| Service       | Port  | Description    |
| ------------- | ----- | -------------- |
| mongo1        | 27018 | Primary node   |
| mongo2        | 27019 | Secondary node |
| mongo3        | 27020 | Secondary node |
| mongo-express | 8082  | Web UI admin   |

## Connection String

```
mongodb://localhost:27018,localhost:27019,localhost:27020/?replicaSet=mongo-replica-set
```

## Notes

- Cluster initialization takes ~30 seconds
- If mongo-express fails to connect, wait and retry
- Check logs: `docker logs mongodatakit_mongo1`

## Cleanup

```bash
docker-compose down -v
```
