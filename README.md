# HappyHeadlines

Semester project for Development of Large Systems (E2026).

## Week 35 - architecture

Started with the C4 system context and container diagrams. The diagrams are kept in `docs` because the architecture changes during the semester.

## Week 36 - scalability

Added `ArticleService`.

- 3 service replicas for X-axis scaling
- article data split into geographic PostgreSQL shards for Z-axis scaling
- Nginx used as the entry point

## Week 37 - fault isolation

Added comments and profanity filtering.

- `CommentService` has its own database
- `ProfanityService` has its own database
- the services only share the moderation network
- CommentService uses a Polly circuit breaker
- comments stay pending if moderation is temporarily unavailable

## Week 38 - logging and tracing

Added `DraftService` and `DraftDatabase`.

DraftService supports creating, reading, updating and deleting article drafts. The database is PostgreSQL and is isolated on its own network.

Logging and tracing are configured in `src/Shared/Observability` so the same setup can be reused by the services. Logs are structured and requests are traced with OpenTelemetry. Draft database operations also create their own spans. Telemetry is sent to the local observability service and can be viewed in Grafana.

We log IDs and useful operational details, but not the draft body itself.

The updated monitoring C4 diagrams are in `docs`.

### Local endpoints

```text
ArticleService   http://localhost:8080
CommentService   http://localhost:8081
DraftService     http://localhost:8082
Grafana          http://localhost:3000
```

### Build and run

```bash
docker build -t happyheadlines/article-service:week38 -f src/ArticleService/Dockerfile .
docker build -t happyheadlines/comment-service:week38 -f src/CommentService/Dockerfile .
docker build -t happyheadlines/profanity-service:week38 -f src/ProfanityService/Dockerfile .
docker build -t happyheadlines/draft-service:week38 -f src/DraftService/Dockerfile .

docker swarm init
docker stack deploy -c docker-stack.yml happyheadlines
```

If Swarm is already enabled, skip `docker swarm init`.

## Contributors

- Ahmad Amer Bakran
- Mahmoud (`Hozaneybo`)
