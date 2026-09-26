# Week 39 - distributed tracing

## Automated analysis

Zipkin and Jaeger are useful for finding and visualising traces, but they still leave most of the analysis to the operator. In a large system there can be too many traces to inspect manually, so answering a simple question such as "is there an incident?" can take too long.

The paper suggests reducing that search space by extracting useful metrics from tracing data and analysing them automatically. Request counts, response times and changes in service behaviour can be used to point to the service and time period that looks unusual. The operator can then inspect the relevant traces instead of searching through everything.

## Dashboard changes

The Grafana overview shows requests per minute, average response time, failed requests and p95 response time for each service. It is meant to show where to start looking before opening an individual trace.

## Project tracing

The publishing flow uses RabbitMQ. The current trace context is added to each published message and restored by `ArticleService` and `NewsletterService`, so queue processing stays connected to the original request trace.

All services export telemetry to the same local observability stack.
