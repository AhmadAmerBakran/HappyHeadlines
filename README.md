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

Logging and tracing are shared through `src/Shared/Observability`. Telemetry is sent to the local Grafana observability stack.

## Week 39 - distributed tracing

Added a publishing flow with `WebApp`, `PublisherService`, RabbitMQ and `NewsletterService`. Published articles are sent to both `ArticleService` and `NewsletterService`.

Trace context is copied into the RabbitMQ message headers and restored by each consumer, so the trace continues when a request crosses the queue boundary. The Grafana overview from the learning activity is kept for incident investigation.

### Local endpoints

```text
ArticleService      http://localhost:8080
CommentService      http://localhost:8081
DraftService        http://localhost:8082
PublisherService    http://localhost:8083
NewsletterService   http://localhost:8084
WebApp              http://localhost:8085
Grafana             http://localhost:3000
RabbitMQ            http://localhost:15672
```

### Build and run

```bash
docker build -t happyheadlines/article-service:week39 -f src/ArticleService/Dockerfile .
docker build -t happyheadlines/comment-service:week39 -f src/CommentService/Dockerfile .
docker build -t happyheadlines/profanity-service:week39 -f src/ProfanityService/Dockerfile .
docker build -t happyheadlines/draft-service:week39 -f src/DraftService/Dockerfile .
docker build -t happyheadlines/publisher-service:week39 -f src/PublisherService/Dockerfile .
docker build -t happyheadlines/newsletter-service:week39 -f src/NewsletterService/Dockerfile .
docker build -t happyheadlines/webapp:week39 -f src/WebApp/Dockerfile .

docker swarm init
docker stack deploy -c docker-stack.yml -c docker-stack.week39.yml happyheadlines
```

If Swarm is already enabled, skip `docker swarm init`.

## Contributors

- Ahmad Amer Bakran
- Mahmoud (`Hozaneybo`)
