# HappyHeadlines

Semester project for Development of Large Systems (E2026).

## Week 35: Architecture

The project started with the C4 system context and container diagrams. The diagrams are kept in `docs` because the architecture changes during the semester.

## Week 36: Scalability

`ArticleService` was added with three replicas, geographic PostgreSQL shards and Nginx as the entry point.

## Week 37: Fault isolation

Comments and profanity filtering were separated into `CommentService` and `ProfanityService`. Each service owns its database, and the comment flow uses a circuit breaker when profanity filtering is unavailable.

## Week 38: Logging and tracing

`DraftService` and `DraftDatabase` were added. Shared logging and tracing live in `src/Shared/Observability` and telemetry is sent to the local Grafana stack.

## Week 39: Distributed tracing

The publishing flow uses `WebApp`, `PublisherService`, RabbitMQ and `NewsletterService`. Trace context is copied into RabbitMQ message headers and restored by the consumers so the trace continues across the queue.

## Week 40: Caching

Global articles are cached in Redis between `ArticleService` and the global article database. A background process refreshes the cache every minute with articles from the latest 14 days. Cache entries expire after three minutes so the service falls back to PostgreSQL if the refresh process stops. Changes to global articles invalidate the affected cache data and the next scheduled refresh fills it again.

Published comments are cached in a separate Redis instance. A missing article entry is loaded from PostgreSQL and stored in the cache. The cache keeps comments for the 30 most recently accessed articles. Access time is tracked in Redis and the least recently used article is removed when the limit is exceeded.

Both services record cache hits and misses through the existing OpenTelemetry setup. Grafana provisions a dashboard called `Happy Headlines cache` with the hit ratio and lookup rate for both cache layers. Cached GET requests also return an `X-Cache` response header with `HIT`, `MISS` or `BYPASS` so the behaviour can be shown directly during the presentation.

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

### Build and run week 40

```bash
docker build -t happyheadlines/article-service:week40 -f src/ArticleService/Dockerfile .
docker build -t happyheadlines/comment-service:week40 -f src/CommentService/Dockerfile .

docker swarm init
docker stack deploy -c docker-stack.yml -c docker-stack.week40.yml happyheadlines
```

If Swarm is already enabled, `docker swarm init` is not needed.

### Cache check

Use `curl.exe -i` when calling the two cached GET endpoints. The response header shows whether the request was served by Redis or PostgreSQL.

```text
GET http://localhost:8080/api/articles/global?limit=1
GET http://localhost:8081/api/comments/article/{articleId}
```

For the comment endpoint, the first request for an article should be a miss. Repeating the same request should be a hit. Article cache content is refreshed in the background, so a recent global article becomes a hit after the next refresh.

## Week 43: CI/CD

GitHub Actions builds the .NET solution on pull requests and pushes to `master` or `main`. After a successful push, it builds and publishes Docker images for all seven application services to GitHub Container Registry.

Images have a version tag (`1.0.<run-number>`) and a commit tag (`sha-<commit-sha>`). For example:

```bash
docker pull ghcr.io/ahmadamerbakran/happyheadlines/article-service:1.0.25
```

Replace `25` with the actual run number shown in GitHub Actions. The workflow publishes images but does not deploy the stack.

## Week 44: Subscriber recovery

The subscriber API, PostgreSQL database and RabbitMQ queue have separate networks. NewsletterService reads subscribers through an internal API and consumes welcome messages from the subscriber queue. If the subscriber stack is down, the existing newsletter endpoints remain available.

The release toggle is stored in PostgreSQL and starts disabled. An admin can enable it for 100% of readers or gradually release it to a fixed percentage without redeploying. The same email always gets the same rollout decision.

Run the stack with `docker-stack.yml`, `docker-stack.week40.yml` and `docker-stack.week44.yml`. Export `SUBSCRIBER_ADMIN_KEY`, `SUBSCRIBER_INTERNAL_KEY` and `NEWSLETTER_ADMIN_KEY` in your shell first. Build `happyheadlines/subscriber-service:week44` before deployment.

SubscriberService: `http://localhost:8086`. To change the rollout, PUT `{"enabled":true,"percentage":100}` to `/api/subscribers/rollout` with header `X-Subscriber-Admin-Key`. POST `{"email":"reader@example.com"}` to `/api/subscribers` to sign up. DELETE `/api/subscribers/reader%40example.com` to unsubscribe.

Mailpit receives welcome emails at `http://localhost:8025`. NewsletterService sends a newsletter through POST `/api/newsletters/send` with a subject and body and header `X-Newsletter-Admin-Key`. Failed welcome messages are routed to the subscriber failed queue.

## Contributors

Ahmad Amer Bakran

Mahmoud, GitHub username `Hozaneybo`
