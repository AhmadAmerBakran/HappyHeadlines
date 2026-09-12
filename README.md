# HappyHeadlines

Semester project for Development of Large Systems (E2026).

## Week 35 - architecture

The first week contains the C4 system context and container diagrams in the `docs` folder. They are kept in the repository because the architecture is extended during the semester.

## Week 36 - scalability cube

Week 36 added the ArticleService and the first scalable deployment of the article part of the system.

The service is a REST API with CRUD operations. Article data is split by scope across separate PostgreSQL databases, while the service itself runs with three replicas in Docker Swarm.

### Scaling used

**X-axis:** three identical ArticleService replicas.

**Z-axis:** article data is split into Africa, Antarctica, Asia, Australia, Europe, North America, South America and Global databases.

The article endpoints are:

```text
POST   /api/articles
GET    /api/articles/{scope}/{id}
GET    /api/articles/{scope}
PUT    /api/articles/{scope}/{id}
DELETE /api/articles/{scope}/{id}
```

## Week 37 - fault isolation

This week adds comments and profanity filtering.

`CommentService` owns the comment data and `ProfanityService` owns the profanity word list. Each service has its own PostgreSQL database and its own data network. The only network shared by the two services is the `moderation` network used for the direct service-to-service call.

```text
Client -> CommentService -> ProfanityService
              |                  |
         CommentDatabase    ProfanityDatabase
```

CommentService calls ProfanityService directly by its Docker service name. Nginx and the UI are not involved in that call.

### Circuit breaker

CommentService uses Polly around calls to ProfanityService. After three failed calls the circuit opens for 30 seconds, which stops CommentService from repeatedly calling an unavailable dependency.

When profanity filtering is unavailable, the comment is saved with `pending` status and is not returned by the public comment query. Once the profanity service is available again, the moderation endpoint can be used to retry that comment.

The CommentService health endpoint shows the current circuit state.

### Comment API

CommentService is exposed on port `8081` for this course setup.

```text
POST /api/comments
GET  /api/comments/article/{articleId}
POST /api/comments/{id}/moderate
GET  /health
```

Example:

```json
{
  "articleId": "11111111-1111-1111-1111-111111111111",
  "author": "Ahmad",
  "content": "This is a great article."
}
```

ProfanityService exposes `POST /api/profanity/filter` internally. It is not published on a host port; CommentService reaches it through the `moderation` network.

## Running the project

Build the three application images:

```bash
docker build -t happyheadlines/article-service:week36 -f src/ArticleService/Dockerfile .
docker build -t happyheadlines/comment-service:week37 -f src/CommentService/Dockerfile .
docker build -t happyheadlines/profanity-service:week37 -f src/ProfanityService/Dockerfile .
```

Start Docker Swarm if needed:

```bash
docker swarm init
```

Deploy the stack:

```bash
docker stack deploy -c docker-stack.yml happyheadlines
```

Check the services:

```bash
docker stack services happyheadlines
```

ArticleService is available through the existing Nginx entry point:

```text
http://localhost:8080
```

CommentService is available at:

```text
http://localhost:8081
```

### Testing the circuit breaker

Stop ProfanityService:

```bash
docker service scale happyheadlines_profanity-service=0
```

Send at least three comment requests to:

```text
POST http://localhost:8081/api/comments
```

The comments are accepted as `pending`. After the failure threshold is reached, `GET http://localhost:8081/health` shows the circuit state as open.

Start ProfanityService again:

```bash
docker service scale happyheadlines_profanity-service=1
```

Wait for the 30 second break period and retry a pending comment:

```text
POST http://localhost:8081/api/comments/{commentId}/moderate
```

A successful retry filters the text and changes the comment status to `published`.

Remove the stack when finished:

```bash
docker stack rm happyheadlines
```

The database passwords in `docker-stack.yml` are only for the local course environment.

## Contributors

- Ahmad Amer Bakran
- Mahmoud (`Hozaneybo`)
