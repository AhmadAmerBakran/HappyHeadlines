# Week 39 - distributed tracing

## Automated analysis

Zipkin and Jaeger are useful for finding and visualising traces, but they still leave most of the analysis to the operator. In a large system there can be too many traces to inspect manually, so answering a simple question such as "is there an incident?" can take too long.

The paper suggests reducing that search space by extracting useful metrics from tracing data and analysing them automatically. Request counts, response times and changes in service behaviour can be used to point to the service and time period that looks unusual. The operator can then inspect the relevant traces instead of searching through everything.

## Dashboard changes

The Grafana overview now focuses on the signals that are useful when looking for an incident:

- requests per minute for each service
- average response time
- failed requests
- 95th percentile response time

The dashboard is meant to show where to start looking. The trace view is still used afterwards to follow a request across services and find the cause.

## Running it

Build the four service images with the `week39` tag, then deploy the normal stack together with `docker-stack.week39.yml`.

```bash
docker stack deploy -c docker-stack.yml -c docker-stack.week39.yml happyheadlines
```

Open Grafana at `http://localhost:3000`. The **Happy Headlines overview** dashboard is provisioned automatically. Generate a few requests against the services and use a recent time range before taking the screenshot for Moodle.
