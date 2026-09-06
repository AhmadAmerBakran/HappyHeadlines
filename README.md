# HappyHeadlines

Semester project for Development of Large Systems (E2026).

## Week 36 - scalability cube

This week adds the ArticleService and the first scalable deployment of the article part of the system.

The ArticleService is a REST API with CRUD operations. Articles are stored in separate PostgreSQL databases depending on where the article is relevant. The service itself runs with three replicas in Docker Swarm.

### Scaling used

**X-axis**

The ArticleService runs as three identical replicas. Nginx is the public entry point and Docker Swarm distributes requests between the service replicas.

**Z-axis**

Article data is split into eight databases:

- Africa
- Antarctica
- Asia
- Australia
- Europe
- North America
- South America
- Global

The `scope` on an article decides which database is used. `global` is used for articles that are relevant worldwide. `oceania` is accepted as an alias for `australia`.

### API

The main endpoints are:

```text
POST   /api/articles
GET    /api/articles/{scope}/{id}
GET    /api/articles/{scope}
PUT    /api/articles/{scope}/{id}
DELETE /api/articles/{scope}/{id}
```

Example request body:

```json
{
  "title": "New wind farm opens in Denmark",
  "content": "Example article content.",
  "source": "Happy Headlines",
  "scope": "europe"
}
```

### Run with Docker Swarm

Build the ArticleService image:

```bash
docker build -t happyheadlines/article-service:week36 -f src/ArticleService/Dockerfile .
```

Start Swarm if it is not already running:

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

The API is available through the load balancer at:

```text
http://localhost:8080
```

Swagger is available at:

```text
http://localhost:8080/swagger
```

To see the three ArticleService replicas:

```bash
docker service ps happyheadlines_article-service
```

Calling `GET /health` several times also returns an `X-ArticleService-Instance` response header, which can be used to see which replica handled the request.

Remove the stack when finished:

```bash
docker stack rm happyheadlines
```

The password in `docker-stack.yml` is only for the local course environment and should not be reused for a deployed system.

## Contributors

- Ahmad Amer Bakran
- Mahmoud (`Hozaneybo`)
