# syntax=docker/dockerfile:1

FROM dhi.io/python:3.13-alpine

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

COPY services/ai/planning-coordination/planning_coordination /app/planning_coordination

EXPOSE 8080

ENTRYPOINT ["python3", "-m", "planning_coordination"]
